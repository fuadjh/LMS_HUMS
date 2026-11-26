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
            var content = new StudioContent([new(Guid.NewGuid(), "فصل", 1, 1, 2,
                [new(lessonKey, "درسک", "خلاصه", "بدنه", 1, 1, 2, [], null)], [])]);
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
            Assert.Single(await db.Set<Lesson>().ToArrayAsync());
            Assert.Empty(await db.Set<LessonSession>().ToArrayAsync());
            Assert.Empty(await db.Set<Assessment>().ToArrayAsync());
            Assert.Empty(await db.Set<Enrollment>().ToArrayAsync());
            _ = await As(student.Id, new JoinOfferingCommand(offer.Id));
            var early = await As(student.Id, new GetOfferingQuery(offer.Id, "student"));
            Assert.Null(Assert.Single(Assert.Single(early.Chapters).Lessons).Body);
            clock.Now = Day(1);
            var reading = await As(student.Id, new GetOfferingQuery(offer.Id, "student"));
            Assert.True(Assert.Single(Assert.Single(reading.Chapters).Lessons).CanRead);
            Assert.Equal("بدنه", Assert.Single(Assert.Single(reading.Chapters).Lessons).Body);
            clock.Now = Day(3, 0);
            var afterLesson = await As(student.Id, new GetOfferingQuery(offer.Id, "student"));
            Assert.False(Assert.Single(Assert.Single(afterLesson.Chapters).Lessons).CanRead);

            var template = await As(professor.Id, new SaveAssignmentTemplateCommand(new(null, "تکلیف", "پاسخ را بنویسید.", null, 4, 20)));
            Assert.Empty(await As(colleague.Id, new GetAssignmentTemplatesQuery(null, 1)));
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => As(professor.Id,
                new SaveOfferingAssignmentCommand(Guid.NewGuid(), new(null, null, null, null, null, Day(5), TemplateVersionId: template))))).StatusCode);
            var task = await As(professor.Id, new SaveOfferingAssignmentCommand(offer.Id,
                new(null, null, null, null, null, Day(5), TemplateVersionId: template)));
            var unpublished = await As(professor.Id, new GetOfferingAssignmentQuery(offer.Id, task, "teacher"));
            Assert.Empty((await As(student.Id, new GetOfferingAssignmentsQuery(offer.Id, "student"))).Rows);
            Assert.True(await As(professor.Id, new PublishOfferingAssignmentCommand(offer.Id, task, new(unpublished.Revision))));
            Assert.True((await As(student.Id, new GetOfferingAssignmentQuery(offer.Id, task, "student"))).CanSubmit);
            var onTime = await As(student.Id, new SubmitAssessmentCommand(task, new("پاسخ به‌موقع", null, RequestId: Guid.NewGuid())));
            _ = await As(professor.Id, new SaveAssessmentGradeCommand(onTime, new(20, null, true)));
            var published = await As(professor.Id, new GetOfferingAssignmentQuery(offer.Id, task, "teacher"));
            await Assert.ThrowsAsync<UseCaseException>(() => As(colleague.Id,
                new ExtendOfferingAssignmentCommand(offer.Id, task, new(published.Revision, Day(8), 2))));
            _ = await As(professor.Id, new ExtendOfferingAssignmentCommand(offer.Id, task, new(published.Revision, Day(8), 2)));
            clock.Now = Day(5);
            var token = Guid.NewGuid();
            var submit = new SubmitAssessmentCommand(task, new("پاسخ در مهلت تمدید", null, RequestId: token));
            var late = await As(student.Id, submit);
            Assert.Equal(late, await As(student.Id, submit));
            Assert.Equal(409, (await Assert.ThrowsAsync<UseCaseException>(() => As(student.Id,
                new SubmitAssessmentCommand(task, new("متن دیگر", null, RequestId: token))))).StatusCode);
            Assert.Equal(400, (await Assert.ThrowsAsync<UseCaseException>(() => As(professor.Id,
                new SaveAssessmentGradeCommand(late, new(19, null, true))))).StatusCode);
            var ownWork = await As(student.Id, new GetAssessmentWorkQuery(task, "student"));
            Assert.Equal(18m, ownWork.Single(x => x.Id == late).ScoreLimit);
            Assert.Equal(20m, ownWork.Single(x => x.Id == onTime).ScoreLimit);
            Assert.Equal(20m, Assert.Single(ownWork.Single(x => x.Id == onTime).Grades).Score);
            var cal = await As(professor.Id, new GetLmsCalendarQuery("teacher", Day(2), Day(9)));
            var receipt = Assert.Single(cal.Where(x => x.Kind == "assignment-received" && x.EventId == late));
            Assert.Equal("جواب تکلیف برنامه‌ریزی ۱۸", receipt.Title);
            Assert.DoesNotContain(await As(student.Id, new GetLmsCalendarQuery("student", Day(2), Day(9))), x => x.Kind == "assignment-received");
            Assert.DoesNotContain(await As(colleague.Id, new GetLmsCalendarQuery("teacher", Day(2), Day(9))), x => x.Kind == "assignment-received");
            Assert.Equal(2, await db.Set<AssignmentCalendarReceipt>().CountAsync());
            Assert.Equal(1, Assert.Single(await As(professor.Id, new GetAssignmentQueueQuery())).Pending);
            _ = await As(student.Id, new PostOfferingDiscussionCommand(offer.Id, "student", new("پیام همین درس")));
            Assert.Equal("پیام همین درس", Assert.Single((await As(professor.Id, new GetOfferingDiscussionQuery(offer.Id, "teacher", 1))).Rows).Text);
            await Assert.ThrowsAsync<UseCaseException>(() => As(colleague.Id, new GetOfferingDiscussionQuery(offer.Id, "teacher", 1)));
            clock.Now = Day(8);
            Assert.Equal(AssignmentStatus.Expired, (await As(student.Id, new GetOfferingAssignmentQuery(offer.Id, task, "student"))).Summary.Status);
            Assert.Equal(409, (await Assert.ThrowsAsync<UseCaseException>(() => As(student.Id, new SubmitAssessmentCommand(task,
                new("پاسخ بعد از پایان پذیرش", null, RequestId: Guid.NewGuid()))))).StatusCode);
            var expired = await As(professor.Id, new GetOfferingAssignmentQuery(offer.Id, task, "teacher"));
            _ = await As(professor.Id, new ExtendOfferingAssignmentCommand(offer.Id, task, new(expired.Revision, Day(10), 4)));
            var secondExtension = await As(student.Id, new SubmitAssessmentCommand(task, new("تمدید دوم", null, RequestId: Guid.NewGuid())));
            var frozen = await As(student.Id, new GetAssessmentWorkQuery(task, "student"));
            Assert.Equal(18m, frozen.Single(x => x.Id == late).ScoreLimit);
            Assert.Equal(16m, frozen.Single(x => x.Id == secondExtension).ScoreLimit);
            var future = new Term { Year = 1406, Season = TermSeason.Mehr, StartsUtc = Day(30, 0), EndsUtc = Day(60, 0),
                EnrollOpensUtc = Day(26), EnrollClosesUtc = Day(32), DropOpensUtc = Day(30), DropClosesUtc = Day(33), Active = true };
            var next = new Offering { CourseId = course.Id, TermId = future.Id, LeadId = professor.Id, GroupCode = "ST2", Active = true,
                Audience = OfferingAudience.Specialized, OwnerMajorId = major.Id };
            db.AddRange(future, next);
            await db.SaveChangesAsync();
            Assert.Equal(offer.Id, Assert.Single(await As(professor.Id, new GetScheduleOriginsQuery(next.Id, version.VersionId))).OfferingId);
            var shifted = await As(professor.Id, new PreviewOfferingScheduleQuery(next.Id,
                new(version.VersionId, start.AddDays(30), ScheduleMode.Shift, offer.Id, [])));
            Assert.Empty(shifted.Issues);
            Assert.Equal(Day(31), Assert.Single(shifted.Rows.Where(x => x.Item.Kind == ScheduleKind.Lesson)).Start.AtUtc);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
