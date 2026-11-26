using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Application;
using ReusableAccess.Application.Academics;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Academics;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Tests.Academics;

public sealed partial class LmsPersistenceTests
{
    private const string Baseline = "20261103062810_CompleteAdminIdentity";
    private const string AddLms = "20261121203400_AddUniversityLms";
    private const string AddStudio = "20261125090000_AddLmsStudio";
    private const string AddWorkspace = "20261126093000_SeparateOfferingWorkspace";
    private static string? Sql => Environment.GetEnvironmentVariable("RA_SQLSERVER_TEST_CONNECTION") ??
        Environment.GetEnvironmentVariable("REUSABLEACCESS_TEST_SQLSERVER");

    [Fact]
    public void FrozenMigrationMatchesRuntimeAndAllAcademicFksAreNoAction()
    {
        const string noConnect = "Server=localhost;Database=ModelOnly;Integrated Security=True;Encrypt=True";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.UseSqlServer(noConnect).UseOpenIddict<Guid>();
        using var db = new ApplicationDbContext(options.Options);
        Assert.Contains(AddLms, db.Database.GetMigrations());
        Assert.Contains(AddStudio, db.Database.GetMigrations());
        Assert.Contains(AddWorkspace, db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
        foreach (var type in db.Model.GetEntityTypes().Where(t => t.GetSchema() == "lms"))
        foreach (var fk in type.GetForeignKeys()) Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        Assert.Contains(db.Model.FindEntityType(typeof(StudyRecord))!.GetIndexes(), x => x.IsUnique &&
            x.Properties.Count == 1 && x.Properties[0].Name == nameof(StudyRecord.StudentId) && x.GetFilter() == "[EndedUtc] IS NULL");
    }

    [AcademicSqlFact]
    public Task FreshInstall() => WithDatabase(async db =>
    {
        await db.Database.MigrateAsync();
        Assert.Contains(AddLms, await db.Database.GetAppliedMigrationsAsync());
        Assert.Contains(AddWorkspace, await db.Database.GetAppliedMigrationsAsync());
        Assert.False(await db.Set<Enrollment>().AnyAsync());
    });

    [AcademicSqlFact]
    public Task UpgradeKeepsBaselineIdentity() => WithDatabase(async db =>
    {
        await db.GetService<IMigrator>().MigrateAsync(Baseline);
        var marker = User(); db.Users.Add(marker); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await db.Database.MigrateAsync();
        Assert.Equal(marker.Id, (await db.Users.SingleAsync(x => x.Id == marker.Id)).Id);
        Assert.False(await db.Set<StudentProfile>().AnyAsync());
    });

    [AcademicSqlFact]
    public async Task ExitsAreSeparateAndNeverDeleteAssignmentsProgressOrGrades()
    {
        var cs = Isolated();
        var clock = new Clock();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton<TimeProvider>(clock);
        services.AddApplication(); services.AddInfrastructure(cs); services.AddUniversityLms();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        try
        {
            await db.Database.MigrateAsync();
            await host.SeedCompletionCatalogAsync(); await host.SeedUniversityLmsAsync();
            var actor = User(); var stranger = User(); var teacher = User(); var admin = User(); var scoped = User(); admin.IsPlatformSuperAdmin = true;
            var degree = await db.Set<Degree>().FirstAsync();
            var f = new Faculty { Code = "DEPT", Title = "Test", Active = true };
            var outside = new Faculty { Code = "OUTSIDE", Title = "Outside", Active = true };
            var m = new Major { FacultyId = f.Id, Code = "MED", Title = "Test", Active = true };
            var p = new AcademicProgram { MajorId = m.Id, DegreeId = degree.Id, Active = true };
            var c = new Cohort { ProgramId = p.Id, Year = 1404, Season = TermSeason.Mehr, Active = true };
            var t = new Term { Year = 1405, Season = TermSeason.Mehr, StartsUtc = clock.Now.AddDays(-2), EndsUtc = clock.Now.AddDays(15),
                EnrollOpensUtc = clock.Now.AddDays(-3), EnrollClosesUtc = clock.Now.AddDays(1), DropOpensUtc = clock.Now, DropClosesUtc = clock.Now.AddHours(1), Active = true };
            var profile = new InstructorProfile { Id = teacher.Id, MajorId = m.Id, Active = true };
            var course = new CatalogCourse { FacultyId = f.Id, Code = "P", Title = "فیزیولوژی", Active = true };
            var offer = new Offering { CourseId = course.Id, LeadId = teacher.Id, TermId = t.Id, Active = true, GroupCode = "1", OwnerMajorId = m.Id,
                Audience = OfferingAudience.Specialized, PublishedUtc = clock.Now.AddDays(-1), VisibleUtc = clock.Now.AddDays(-1) };
            var chapter = new Chapter { OfferingId = offer.Id, Title = "فصل", Position = 1, PublishedUtc = clock.Now, Active = true };
            var lesson = new Lesson { ChapterId = chapter.Id, Position = 1, Title = "درسک", Active = true };
            var task = new Assessment { OfferingId = offer.Id, PublishedUtc = clock.Now, Title = "تکلیف", Prompt = "متن", MaxScore = 20, MaxAttempts = 2,
                DueUtc = clock.Now.AddHours(3), CutoffUtc = clock.Now.AddHours(5), Active = true };
            var study = new StudyRecord { StudentId = actor.Id, CohortId = c.Id, Number = "fixture-1", OpenedUtc = clock.Now.AddDays(-10) };
            var otherStudy = new StudyRecord { StudentId = stranger.Id, CohortId = c.Id, Number = "fixture-2", OpenedUtc = clock.Now.AddDays(-10) };
            db.AddRange(actor, stranger, teacher, admin, scoped, f, outside, m, p, c, t, profile, course, offer, chapter, lesson, task,
                new StudentProfile { Id = actor.Id, Active = true }, new StudentProfile { Id = stranger.Id, Active = true }, study, otherStudy,
                new AcademicGrant { UserId = scoped.Id, FacultyId = outside.Id, Active = true });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            // An HTTP request has a fresh authorization scope; reuse of SQL data is intentional, reuse of permissions is not.
            async Task<TResult> As<TResult>(Guid user, IRequest<TResult> request)
            {
                http.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", user.ToString())], "test")) };
                using var requestScope = host.CreateScope();
                try { return await requestScope.ServiceProvider.GetRequiredService<ISender>().Send(request); }
                finally { http.HttpContext = null; }
            }
            var joined = await As(actor.Id, new JoinOfferingCommand(offer.Id));
            var joinedOther = await As(stranger.Id, new JoinOfferingCommand(offer.Id));
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => As(stranger.Id, new WithdrawMyEnrollmentCommand(joined.Id)))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => As(scoped.Id, new RemoveEnrollmentAsAdminCommand(joined.Id, new("wrong scope"))))).StatusCode);
            Assert.True(await As(actor.Id, new MarkLessonDoneCommand(lesson.Id)));
            var first = await As(actor.Id, new SubmitAssessmentCommand(task.Id, new("جواب", null)));
            _ = await As(teacher.Id, new SaveAssessmentGradeCommand(first, new(16, "بازخورد", true)));
            _ = await As(teacher.Id, new SaveFinalGradeCommand(joined.Id, new(17, "پایانی", true)));
            Assert.Equal(16m, Assert.Single(Assert.Single(await As(actor.Id, new GetAssessmentWorkQuery(task.Id, "student"))).Grades).Score);
            clock.Now = clock.Now.AddMinutes(10);
            Assert.Equal(EnrollmentStatus.WithdrawnByStudent, (await As(actor.Id, new WithdrawMyEnrollmentCommand(joined.Id))).Status);
            Assert.False((await As(actor.Id, new WithdrawMyEnrollmentCommand(joined.Id))).Changed);
            clock.Now = clock.Now.AddHours(2);
            Assert.Equal(409, (await Assert.ThrowsAsync<UseCaseException>(() => As(stranger.Id, new WithdrawMyEnrollmentCommand(joinedOther.Id)))).StatusCode);
            Assert.Equal(EnrollmentStatus.RemovedByAdmin, (await As(admin.Id, new RemoveEnrollmentAsAdminCommand(joinedOther.Id, new("case review")))).Status);
            Assert.Equal(joined.Id, (await db.Set<Submission>().AsNoTracking().SingleAsync()).EnrollmentId);
            Assert.Equal(joined.Id, (await db.Set<LessonProgress>().AsNoTracking().SingleAsync()).EnrollmentId);
            Assert.Equal(17m, Assert.Single(await As(actor.Id, new GetTranscriptQuery())).UnofficialGrade!.Score);
            Assert.Equal(2, await db.Set<EnrollmentEvent>().CountAsync(x => x.EnrollmentId == joined.Id));
            Assert.Equal(2, await db.Set<EnrollmentEvent>().CountAsync(x => x.EnrollmentId == joinedOther.Id));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static ApplicationUser User()
    {
        var id = Guid.NewGuid(); var name = id.ToString("N");
        return new() { Id = id, UserName = name, NormalizedUserName = name.ToUpperInvariant(), NationalId = name, DisplayName = "Test", IsActive = true };
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static string Isolated() => new SqlConnectionStringBuilder(Sql!) { InitialCatalog = "RA_LmsTest_" + Guid.NewGuid().ToString("N"), Pooling = false }.ConnectionString;
    private static async Task WithDatabase(Func<ApplicationDbContext, Task> exercise)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.UseSqlServer(Isolated()).UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options);
        try { await exercise(db); }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
    public sealed class AcademicSqlFactAttribute : FactAttribute
    {
        public AcademicSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Sql)) Skip = "An isolated SQL Server test instance is required; set RA_SQLSERVER_TEST_CONNECTION.";
        }
    }
}
