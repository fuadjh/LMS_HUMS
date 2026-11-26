using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Tests.Academics;

public sealed partial class LmsPersistenceTests
{
    [AcademicSqlFact]
    public Task UpgradeFromStudioKeepsLegacyLinksAndDoesNotChangeGrades() => WithDatabase(async db =>
    {
        await db.GetService<IMigrator>().MigrateAsync(AddStudio);
        var now = new Clock().Now;
        var teacher = User(); var student = User();
        var faculty = new Faculty { Code = "UPGRADE", Title = "Upgrade test", Active = true };
        var major = new Major { FacultyId = faculty.Id, Code = "UP", Title = "Upgrade test", Active = true };
        var degree = new Degree { Code = "D", Title = "Test", Active = true };
        var program = new AcademicProgram { MajorId = major.Id, DegreeId = degree.Id, Active = true };
        var cohort = new Cohort { ProgramId = program.Id, Year = 1404, Season = TermSeason.Mehr, Active = true };
        var study = new StudyRecord { StudentId = student.Id, CohortId = cohort.Id, Number = "workspace-fixture", OpenedUtc = now.AddDays(-15) };
        var course = new CatalogCourse { FacultyId = faculty.Id, Code = "UP-1", Title = "درس مهاجرت", Active = true };
        var term = new Term { Year = 1405, StartsUtc = now.AddDays(-2), EndsUtc = now.AddDays(20), Active = true,
            Season = TermSeason.Mehr, EnrollOpensUtc = now.AddDays(-3), EnrollClosesUtc = now.AddDays(1),
            DropOpensUtc = now.AddDays(-2), DropClosesUtc = now.AddDays(2) };
        var offer = new Offering { CourseId = course.Id, TermId = term.Id, LeadId = teacher.Id, GroupCode = "UP-1", Active = true,
            Audience = OfferingAudience.Specialized, OwnerMajorId = major.Id, PublishedUtc = now.AddDays(-1), VisibleUtc = now.AddDays(-1) };
        var enrollment = new Enrollment { OfferingId = offer.Id, StudyRecordId = study.Id, Status = EnrollmentStatus.Active,
            CreatedUtc = now, ChangedUtc = now };
        var chapter = new Chapter { OfferingId = offer.Id, Title = "فصل", Position = 1, Active = true,
            PublishedUtc = now.AddDays(-1), VisibleFromUtc = now.AddHours(-2), VisibleUntilUtc = now.AddDays(5) };
        var session = new LessonSession { OfferingId = offer.Id, Title = "جلسهٔ قدیم", OpensUtc = now.AddHours(1), Active = true };
        db.AddRange(teacher, student, faculty, major, degree, program, cohort, study, course, term, offer, enrollment, chapter, session,
            new StudentProfile { Id = student.Id, Active = true }, new InstructorProfile { Id = teacher.Id, MajorId = major.Id, Active = true });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var lesson = Guid.NewGuid(); var noSession = Guid.NewGuid(); var task = Guid.NewGuid();
        var submission = Guid.NewGuid(); var grade = Guid.NewGuid();
        // Write the V2 shape explicitly; the test assembly uses the current EF model and must not apply it to V2 tables.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO lms.Lesson (Id, ChapterId, SessionId, Title, Body, Position, Active, VisibleFromUtc)
                VALUES ({lesson}, {chapter.Id}, {session.Id}, N'درسک', N'متن', 1, 1, {now});
            INSERT INTO lms.Lesson (Id, ChapterId, Title, Body, Position, Active)
                VALUES ({noSession}, {chapter.Id}, N'درسک قفل', N'محتوای خصوصی', 2, 1);
            INSERT INTO lms.Assessment (Id, ChapterId, LessonId, Title, Prompt, Rubric, OpensUtc, DueUtc, CutoffUtc, MaxAttempts, MaxScore, Active)
                VALUES ({task}, {chapter.Id}, {lesson}, N'تکلیف', N'شرح', N'', {now.AddHours(-3)}, {now.AddDays(3)}, {now.AddDays(4)}, 3, 20, 1);
            INSERT INTO lms.Submission (Id, AssessmentId, EnrollmentId, Attempt, SentUtc, IsLate, Body)
                VALUES ({submission}, {task}, {enrollment.Id}, 1, {now}, 0, N'پاسخ قدیمی');
            INSERT INTO lms.AssessmentGrade (Id, SubmissionId, GraderId, Revision, Score, Feedback, Published, CreatedUtc)
                VALUES ({grade}, {submission}, {teacher.Id}, 1, 16, N'بدون تغییر', 1, {now});
            """);
        await db.Database.MigrateAsync();
        Assert.Contains(AddWorkspace, await db.Database.GetAppliedMigrationsAsync());
        var linked = await db.Set<Lesson>().SingleAsync(x => x.Id == lesson);
        Assert.Equal(session.OpensUtc, linked.VisibleFromUtc);
        Assert.Equal(session.Id, db.Entry(linked).Property<Guid?>("SessionId").CurrentValue);
        Assert.Equal(now, db.Entry(linked).Property<DateTimeOffset?>("LegacyVisibleFromUtc").CurrentValue);
        Assert.True((await db.Set<Lesson>().SingleAsync(x => x.Id == noSession)).NeedsAvailabilityReview);
        var migrated = await db.Set<Assessment>().SingleAsync(x => x.Id == task);
        Assert.Equal(offer.Id, migrated.OfferingId);
        Assert.Equal(chapter.VisibleFromUtc, migrated.OpensUtc);
        Assert.Equal(chapter.VisibleUntilUtc, migrated.VisibleUntilUtc);
        Assert.Equal(chapter.PublishedUtc, migrated.PublishedUtc);
        var attempt = await db.Set<Submission>().SingleAsync(x => x.Id == submission);
        Assert.Equal(enrollment.Id, attempt.EnrollmentId);
        Assert.Null(attempt.ScoreLimit);
        Assert.Equal(16m, (await db.Set<AssessmentGrade>().SingleAsync(x => x.Id == grade)).Score);
        Assert.Empty(await db.Set<AssignmentCalendarReceipt>().ToArrayAsync());
    });
}
