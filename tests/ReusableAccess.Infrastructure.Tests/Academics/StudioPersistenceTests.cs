using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Application;
using ReusableAccess.Application.Academics;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Academics;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Tests.Academics;

public sealed partial class LmsPersistenceTests
{
    [AcademicSqlFact]
    public async Task StudioPublishesOnceDoesNotEnrollAndKeepsDraftsPrivate()
    {
        var services = new ServiceCollection();
        var start = new DateOnly(2027, 3, 20);
        DateTimeOffset Day(int offset, int hour = 8) => JalaliTime.FromDate(start.AddDays(offset).ToDateTime(new(hour, 0)));
        var clock = new Clock { Now = Day(0) };
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Lms:ScheduleStudioEnabled"] = "true" }).Build());
        services.AddApplication(); services.AddInfrastructure(Isolated()); services.AddUniversityLms();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        try
        {
            await db.Database.MigrateAsync();
            await host.SeedCompletionCatalogAsync(); await host.SeedUniversityLmsAsync();
            var professor = User(); var colleague = User(); var student = User();
            var faculty = new Faculty { Code = "STUDIO", Title = "آزمون", Active = true };
            var major = new Major { Code = "ST", Title = "آزمون", FacultyId = faculty.Id, Active = true };
            var degree = await db.Set<Degree>().FirstAsync();
            var program = new AcademicProgram { MajorId = major.Id, DegreeId = degree.Id, Active = true };
            var cohort = new Cohort { ProgramId = program.Id, Year = 1404, Season = TermSeason.Mehr, Active = true };
            var course = new CatalogCourse { FacultyId = faculty.Id, Code = "101", Title = "برنامه‌ریزی", Active = true };
            var term = new Term { Year = 1405, Season = TermSeason.Bahman, StartsUtc = Day(0, 0), EndsUtc = Day(20, 0),
                EnrollOpensUtc = Day(-4), EnrollClosesUtc = Day(4), DropOpensUtc = Day(0), DropClosesUtc = Day(6), Active = true };
            var offer = new Offering { CourseId = course.Id, TermId = term.Id, LeadId = professor.Id, GroupCode = "ST", Active = true,
                Audience = OfferingAudience.Specialized, OwnerMajorId = major.Id };
            db.AddRange(professor, colleague, student, faculty, major, program, cohort, course, term, offer,
                new InstructorProfile { Id = professor.Id, MajorId = major.Id, Active = true },
                new InstructorProfile { Id = colleague.Id, MajorId = major.Id, Active = true },
                new StudentProfile { Id = student.Id, Active = true },
                new StudyRecord { StudentId = student.Id, CohortId = cohort.Id, Number = "fixture-studio", OpenedUtc = Day(-10) });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            async Task<T> As<T>(Guid user, IRequest<T> command)
            {
                http.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", user.ToString())], "test")) };
                await using var request = host.CreateAsyncScope();
                try { return await request.ServiceProvider.GetRequiredService<ISender>().Send(command); }
                finally { http.HttpContext = null; }
            }

            var id = await As(professor.Id, new CreateCourseTemplateCommand(new(course.Id, "الگوی خصوصی", null)));
            var header = await As(professor.Id, new GetCourseTemplateVersionQuery(id, null));
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => As(colleague.Id, new GetCourseTemplateVersionQuery(id, null)))).StatusCode);
            var lessonKey = Guid.NewGuid();
            var content = new StudioContent([new(Guid.NewGuid(), "فصل", 1, 1, 8,
                [new(lessonKey, "درسک", "خلاصه", "بدنه", 1, 1, 2, [], null)],
                [new(Guid.NewGuid(), lessonKey, "تکلیف", "پاسخ را بنویسید.", null, 1, 20, false, null, 1, 5, 7)])]);
            var version = await As(professor.Id, new SaveCourseTemplateVersionCommand(id,
                new(header.Template.Revision, header.Template.Title, null, content)));
            Assert.Equal(409, (await Assert.ThrowsAsync<UseCaseException>(() => As(professor.Id,
                new SaveCourseTemplateVersionCommand(id, new(header.Template.Revision, header.Template.Title, null, content))))).StatusCode);
            var targets = await As(professor.Id, new GetTeachingScheduleTargetsQuery(course.Id, term.Id));
            Assert.Equal(1, Assert.Single(targets).EligibleStudents);
            var plan = new StudioPlan(version.VersionId, start, ScheduleMode.Relative, null, []);
            var page = await As(professor.Id, new PreviewOfferingScheduleQuery(offer.Id, plan));
            Assert.Empty(page.Issues);
            Assert.False(await db.Set<OfferingScheduleDraft>().AnyAsync());
            var draft = await As(professor.Id, new SaveOfferingScheduleDraftCommand(offer.Id, new(null, page.OfferingRevision, page.Stamp, plan)));
            Assert.False(await db.Set<Chapter>().AnyAsync());
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => As(colleague.Id, new GetOfferingScheduleDraftQuery(offer.Id)))).StatusCode);
            var request = new PublishScheduledOfferingCommand(offer.Id, new(draft.Revision, page.OfferingRevision));
            var first = await As(professor.Id, request);
            Assert.Equal(first, await As(professor.Id, request));
            Assert.True(first.PublishedUtc.HasValue);
            Assert.Single(await db.Set<Chapter>().Where(c => c.OfferingId == offer.Id).ToArrayAsync());
            var lesson = Assert.Single(await db.Set<Lesson>().ToArrayAsync());
            var task = Assert.Single(await db.Set<Assessment>().ToArrayAsync());
            Assert.Equal(lesson.Id, task.LessonId);
            Assert.False(await db.Set<Enrollment>().AnyAsync());
            _ = await As(student.Id, new JoinOfferingCommand(offer.Id));
            var early = await As(student.Id, new GetOfferingQuery(offer.Id, "student"));
            Assert.Null(Assert.Single(Assert.Single(early.Chapters).Lessons).Body);
            Assert.Null(Assert.Single(Assert.Single(early.Chapters).Assessments).Prompt);
            clock.Now = Day(3, 0);
            var afterLesson = await As(student.Id, new GetOfferingQuery(offer.Id, "student"));
            Assert.False(Assert.Single(Assert.Single(afterLesson.Chapters).Lessons).CanRead);
            Assert.True(Assert.Single(Assert.Single(afterLesson.Chapters).Assessments).CanRead);
            _ = await As(student.Id, new SubmitAssessmentCommand(task.Id, new("پاسخ", null)));
            Assert.Single(await db.Set<Submission>().ToArrayAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
