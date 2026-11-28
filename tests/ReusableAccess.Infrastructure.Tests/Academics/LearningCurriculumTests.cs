using System.Security.Claims;
using System.IO.Compression;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
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
    public async Task LearningReleaseKeepsStableProgressAndDoesNotExposeDraftOrFutureMaterials()
    {
        var now = new DateTimeOffset(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);
        var clock = new Clock { Now = now };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider, EphemeralDataProtectionProvider>();
        services.AddSingleton<TimeProvider>(clock);
        var storage = Path.Combine(Path.GetTempPath(), "RA_Learning_" + Guid.NewGuid().ToString("N"));
        services.AddSingleton<IHostEnvironment>(new LearningTestEnvironment());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Lms:PrivateFilesPath"] = storage }).Build());
        services.AddApplication(); services.AddInfrastructure(Isolated()); services.AddUniversityLms();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        try
        {
            await db.Database.MigrateAsync();
            await host.SeedCompletionCatalogAsync(); await host.SeedUniversityLmsAsync();
            var lead = User(); lead.OrganizationalCode = "0001"; var pupil = User(); var visitor = User(); var admin = User(); admin.IsPlatformSuperAdmin = true;
            var faculty = new Faculty { Code = "LEARN", Title = "آزمون", Active = true };
            var major = new Major { FacultyId = faculty.Id, Code = "M", Title = "آزمون رشته", Active = true };
            var degree = await db.Set<Degree>().FirstAsync();
            var program = new AcademicProgram { MajorId = major.Id, DegreeId = degree.Id, Active = true };
            var cohort = new Cohort { ProgramId = program.Id, Year = 1405, Season = TermSeason.Mehr, Active = true };
            var baseCourse = new CatalogCourse { FacultyId = faculty.Id, MajorId = major.Id, Code = "01", Title = "آزمون محتوا", Active = true };
            var term = new Term { Year = 1405, Season = TermSeason.Mehr, StartsUtc = now.AddDays(-3), EndsUtc = now.AddDays(10),
                EnrollOpensUtc = now.AddDays(-3), EnrollClosesUtc = now.AddDays(2), DropOpensUtc = now, DropClosesUtc = now.AddDays(3), Active = true };
            var o = new Offering { CourseId = baseCourse.Id, LeadId = lead.Id, TermId = term.Id, OwnerMajorId = major.Id,
                GroupCode = "1", Audience = OfferingAudience.Specialized, Active = true, VisibleUtc = now.AddDays(-2), PublishedUtc = now.AddDays(-2) };
            var ch = new Chapter { OfferingId = o.Id, Title = "فصل قدیمی", Position = 1, PublishedUtc = now.AddDays(-2), Active = true };
            var old = new Lesson { ChapterId = ch.Id, Position = 1, Title = "درس قدیمی", Body = "متن قدیمی محفوظ", Active = true };
            var later = new Lesson { ChapterId = ch.Id, Position = 2, Title = "درس زمان‌دار", Body = "متن زمان‌دار", Summary = "خلاصهٔ زمان‌دار", Active = true,
                VisibleFromUtc = now.AddDays(1) };
            var unpublished = new Chapter { OfferingId = o.Id, Position = 2, Title = "فصل نامرئی", Active = true };
            db.AddRange(lead, pupil, visitor, admin, faculty, major, program, cohort, baseCourse, term, o, ch, old, later, unpublished,
                new InstructorProfile { Id = lead.Id, MajorId = major.Id, Active = true }, new StudentProfile { Id = pupil.Id, Active = true },
                new StudentProfile { Id = visitor.Id, Active = true },
                new StudyRecord { StudentId = pupil.Id, CohortId = cohort.Id, Number = "fixture-lrn-1", OpenedUtc = now.AddDays(-6) },
                new StudyRecord { StudentId = visitor.Id, CohortId = cohort.Id, Number = "fixture-lrn-2", OpenedUtc = now.AddDays(-6) });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Task<T> As<T>(Guid actor, IRequest<T> request) => InScope(actor, sp => sp.GetRequiredService<ISender>().Send(request));
            async Task<T> InScope<T>(Guid actor, Func<IServiceProvider, Task<T>> action)
            {
                http.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", actor.ToString())], "test")) };
                await using var requestScope = host.CreateAsyncScope();
                try { return await action(requestScope.ServiceProvider); }
                finally { http.HttpContext = null; }
            }
            async Task<FileRow> Upload(Guid who, Guid target, MediaPurpose purpose, string name, byte[] bytes)
            {
                var ticket = await As(who, new StartLmsUploadCommand(new(target, purpose, name, bytes.Length)));
                Assert.Equal(UploadState.Pending, ticket.State);
                using var stream = new MemoryStream(bytes);
                return await InScope(who, sp => sp.GetRequiredService<ILmsFileTransport>().ReceiveAsync(ticket.TrackingId, stream, bytes.Length, default));
            }
            async Task<string> ReadFile(Guid who, Guid id) => await InScope(who, async sp =>
            {
                var file = await sp.GetRequiredService<ILmsFileTransport>().DownloadAsync(id, default);
                await using var bytes = file.Body;
                return file.ContentType;
            });
            var enrollment = await As(pupil.Id, new JoinOfferingCommand(o.Id));
            var before = await As(pupil.Id, new GetLearningCurriculumQuery(o.Id, "student"));
            Assert.Equal(old.Id, Assert.Single(Assert.Single(before.Chapters).Lessons).Id);
            Assert.Null(Assert.Single(before.Chapters).Summary);
            Assert.Equal("متن قدیمی محفوظ", (await As(pupil.Id, new GetLearningMaterialQuery(o.Id, "student", old.Id, old.Id))).Body);
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => As(pupil.Id, new GetLearningLessonQuery(o.Id, "student", later.Id)))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => As(visitor.Id, new GetLearningLessonQuery(o.Id, "student", old.Id)))).StatusCode);
            await Assert.ThrowsAsync<UseCaseException>(() => As(pupil.Id, new GetLearningCurriculumQuery(o.Id, "draft")));
            Assert.True(await As(pupil.Id, new CompleteLearningLessonCommand(o.Id, old.Id, true)));
            Assert.True(await As(pupil.Id, new CompleteLearningLessonCommand(o.Id, old.Id, true)));
            var w = await As(lead.Id, new GetLearningTeacherQuery(o.Id));
            var rev = (await As(lead.Id, new SaveLearningCommand(o.Id, new(w.Revision, LearningEditKind.Introduce, Summary: "معرفی تازه")))).Revision;
            Assert.Equal(409, (await Assert.ThrowsAsync<UseCaseException>(() => As(lead.Id,
                new SaveLearningCommand(o.Id, new(w.Revision, LearningEditKind.Introduce, Summary: "تغییر هم‌زمان"))))).StatusCode);
            rev = (await As(lead.Id, new SaveLearningCommand(o.Id, new(rev, LearningEditKind.Chapter, ch.Id,
                Title: ch.Title, Summary: "خلاصه فصل")))).Revision;
            rev = (await As(lead.Id, new SaveLearningCommand(o.Id, new(rev, LearningEditKind.Lesson, ch.Id, old.Id,
                Title: "عنوان نسخهٔ جدید", Summary: "خلاصهٔ درس")))).Revision;
            Assert.Equal("درس قدیمی", (await As(pupil.Id, new GetLearningLessonQuery(o.Id, "student", old.Id))).Lesson.Title);
            var draft = await As(lead.Id, new GetLearningLessonQuery(o.Id, "draft", old.Id));
            Assert.Equal("عنوان نسخهٔ جدید", draft.Lesson.Title);
            Assert.False(draft.CanComplete);
            Assert.Equal(409, (await Assert.ThrowsAsync<UseCaseException>(() => As(lead.Id,
                new PublishLearningCommand(o.Id, new(rev, ch.Id))))).StatusCode);
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAFUlEQVR4nGO8d+b/fwYGBgYmBigAADiXA6zjAuvhAAAAAElFTkSuQmCC");
            var cover = await Upload(lead.Id, o.Id, MediaPurpose.CurriculumCover, "cover.png", png);
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => ReadFile(pupil.Id, cover.Id))).StatusCode);
            rev = (await As(lead.Id, new SaveLearningCommand(o.Id, new(rev, LearningEditKind.Material, ch.Id, old.Id,
                old.Id, "متن قدیمی", "شرح متن", "متن قدیمی محفوظ", MaterialKind: LearningKind.Text,
                CoverAction: LearningCoverAction.Replace, CoverId: cover.Id)))).Revision;
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => ReadFile(pupil.Id, cover.Id))).StatusCode);
            rev = (await As(lead.Id, new SaveLearningCommand(o.Id, new(rev, LearningEditKind.Material, ch.Id, later.Id,
                later.Id, "متن زمان‌دار", "شرح متن زمان‌دار", "متن زمان‌دار", MaterialKind: LearningKind.Text)))).Revision;
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => As(lead.Id,
                new SaveLearningCommand(o.Id, new(rev, LearningEditKind.Feature, ch.Id, later.Id, old.Id))))).StatusCode);
            var publishRequest = new PublishLearningCommand(o.Id, new(rev, ch.Id));
            var release = await As(lead.Id, publishRequest);
            Assert.Equal(1, release.Version);
            Assert.Equal(release, await As(lead.Id, publishRequest));
            var current = await As(pupil.Id, new GetLearningCurriculumQuery(o.Id, "student"));
            Assert.Equal(1, current.CompletedLessons);
            Assert.Equal(cover.Id, Assert.Single(Assert.Single(current.Chapters).Lessons).Thumbnail.CoverId);
            Assert.Equal("image/png", await ReadFile(pupil.Id, cover.Id));
            Assert.Equal(404, (await Assert.ThrowsAsync<UseCaseException>(() => ReadFile(visitor.Id, cover.Id))).StatusCode);
            Assert.Equal("عنوان نسخهٔ جدید", Assert.Single(Assert.Single(current.Chapters).Lessons).Title);
            Assert.Equal(old.Id, Assert.Single(current.Chapters).Lessons[0].Id);
            Assert.True(await db.Set<Chapter>().Where(x => x.Id == ch.Id).Select(x => x.Retired).SingleAsync());
            Assert.Equal("متن قدیمی محفوظ", await db.Set<Lesson>().Where(x => x.Id == old.Id).Select(x => x.Body).SingleAsync());
            Assert.False(await As(pupil.Id, new CompleteLearningLessonCommand(o.Id, old.Id, false)));
            Assert.NotNull((await db.Set<LessonProgress>().SingleAsync(x => x.EnrollmentId == enrollment.Id && x.LessonId == old.Id)).ClearedUtc);
            clock.Now = now.AddDays(1);
            var two = await As(pupil.Id, new GetLearningCurriculumQuery(o.Id, "student"));
            Assert.Equal(2, two.VisibleLessons);
            Assert.Equal(later.Id, (await As(pupil.Id, new GetLearningLessonQuery(o.Id, "student", old.Id))).NextId);

            var xlsx = Workbook("""
                <row r="1"><c r="A1" t="inlineStr"><is><t>course_code</t></is></c><c r="B1" t="inlineStr"><is><t>title</t></is></c></row>
                <row r="2"><c r="A2" t="inlineStr"><is><t>002</t></is></c><c r="B2" t="inlineStr"><is><t>درس واردشده</t></is></c></row>
                <row r="3"><c r="A3" t="inlineStr"><is><t>002</t></is></c><c r="B3" t="inlineStr"><is><t>تکراری</t></is></c></row>
                """);
            var courseFile = await Upload(admin.Id, major.Id, MediaPurpose.RosterExcel, "courses.xlsx", xlsx);
            var args = new AcademicWorkbookInput(courseFile.Id, major.Id);
            Assert.Equal("آماده", (await As(admin.Id, new PreviewAcademicWorkbookQuery("courses", args)))[0].Status);
            Assert.False(await db.Set<CatalogCourse>().AnyAsync(c => c.Code == "002"));
            var imported = await As(admin.Id, new ApplyAcademicWorkbookCommand("courses", args));
            Assert.Equal("ثبت شد", imported[0].Status); Assert.Equal("رد شد", imported[1].Status);
            Assert.Equal(major.Id, (await db.Set<CatalogCourse>().SingleAsync(c => c.Code == "002")).MajorId);
            var groups = Workbook("""
                <row r="1"><c r="A1" t="inlineStr"><is><t>course_code</t></is></c><c r="B1" t="inlineStr"><is><t>group_code</t></is></c><c r="C1" t="inlineStr"><is><t>instructor_code</t></is></c></row>
                <row r="2"><c r="A2" t="inlineStr"><is><t>002</t></is></c><c r="B2" t="inlineStr"><is><t>03</t></is></c><c r="C2" t="inlineStr"><is><t>0001</t></is></c></row>
                """);
            var upload = await Upload(admin.Id, major.Id, MediaPurpose.RosterExcel, "offerings.xlsx", groups);
            Assert.Equal("ثبت شد", Assert.Single(await As(admin.Id,
                new ApplyAcademicWorkbookCommand("offerings", new(upload.Id, major.Id, term.Id)))).Status);
            var adminPage = await As(admin.Id, new ReadAdminOfferingsQuery(new("admin", term.Id, Search: "002"), new()));
            Assert.Equal(15, adminPage.Size);
            Assert.Equal("03", Assert.Single(adminPage.Rows).Group);
        }
        finally
        {
            try { await db.Database.EnsureDeletedAsync(); }
            finally { if (Directory.Exists(storage)) Directory.Delete(storage, recursive: true); }
        }
    }

    [Theory]
    [InlineData(".mp4", LearningKind.Video)] [InlineData(".pdf", LearningKind.Pdf)] [InlineData(".ogg", LearningKind.Audio)]
    [InlineData(".webp", LearningKind.Image)] [InlineData(".xlsx", LearningKind.LegacyFile)]
    public void LearningMediaKeepsLegacyAttachmentTypes(string extension, LearningKind kind)
        => Assert.Equal(kind, LearningKinds.FromExtension(extension));
    private static byte[] Workbook(string rows)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\" />");
            Write("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" />");
            Write("xl/worksheets/sheet1.xml", $"<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>{rows}</sheetData></worksheet>");
            void Write(string name, string text)
            { using var body = zip.CreateEntry(name).Open(); body.Write(Encoding.UTF8.GetBytes(text)); }
        }
        return stream.ToArray();
    }

    private sealed class LearningTestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "LearningTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
