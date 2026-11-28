using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class StudioFlags(IConfiguration? config = null)
{
    internal bool ScheduleEnabled => bool.TryParse(config?["Lms:ScheduleStudioEnabled"], out var enabled) && enabled;
    internal void RequireScheduler()
    {
        if (!ScheduleEnabled) throw LmsError.Conflict("کارگاه زمان‌بندی در تنظیمات این محیط فعال نیست.");
    }
}

internal sealed class StudioTemplates(LmsData data, LmsAccess access, StudioFlags flags) : IStudioTemplateQueries, IStudioTemplateCommands
{
    private const int PageSize = 24;

    internal async Task<CourseTemplate> Own(Guid id, bool tracked, CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        var query = tracked ? data.Db.Set<CourseTemplate>().AsTracking() : data.Read<CourseTemplate>();
        return await query.SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == access.ActorId && t.Active, ct)
            ?? throw LmsError.Missing();
    }

    internal async Task<(CourseTemplate Template, TemplateVersion Version)> Version(Guid versionId, Guid courseId, CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        var match = await (from t in data.Read<CourseTemplate>()
                           join v in data.Read<TemplateVersion>() on t.Id equals v.TemplateId
                           where t.Active && t.OwnerId == access.ActorId && t.CourseId == courseId && v.Id == versionId
                           select new { t, v }).SingleOrDefaultAsync(ct) ?? throw LmsError.Missing();
        return (match.t, match.v);
    }

    private IQueryable<CatalogCourse> AllowedCourses()
    {
        var actor = access.ActorId;
        var ownMajor = from i in data.Read<InstructorProfile>()
                         join m in data.Read<Major>() on i.MajorId equals m.Id
                         where i.Id == actor && i.Active && m.Active
                         select m;
        return from c in data.Read<CatalogCourse>()
               join f in data.Read<Faculty>() on c.FacultyId equals f.Id
               where c.Active && f.Active && (ownMajor.Any(m => c.MajorId == m.Id || c.MajorId == null && c.FacultyId == m.FacultyId) ||
                   data.Read<Offering>().Any(o => o.CourseId == c.Id && o.LeadId == actor && o.Active))
               select c;
    }

    public async Task<CatalogRow[]> Courses(CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        return await AllowedCourses().OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Select(x => new CatalogRow(x.Id, x.Title, x.Code, x.MajorId, x.FacultyId, 0, TermSeason.Mehr, null, true, false))
            .Take(500).ToArrayAsync(ct);
    }

    private IQueryable<StudioTemplateCard> Cards(string search, Guid? templateId = null) =>
        from t in data.Read<CourseTemplate>()
        join c in data.Read<CatalogCourse>() on t.CourseId equals c.Id
        where t.Active && t.OwnerId == access.ActorId && (!templateId.HasValue || t.Id == templateId.Value)
            && (search == "" || t.Title.Contains(search) || c.Title.Contains(search) || c.Code.Contains(search))
        // Sort on database scalar fields before projecting to StudioTemplateCard.
        orderby (t.UpdatedUtc ?? data.Read<TemplateVersion>()
            .Where(v => v.TemplateId == t.Id)
            .OrderByDescending(v => v.Number)
            .Select(v => (DateTimeOffset?)v.CreatedUtc)
            .FirstOrDefault()) descending, t.Id
        select new StudioTemplateCard(t.Id, t.CourseId, c.Title, t.Title, t.Description, t.Revision,
            data.Read<TemplateVersion>()
                .Where(v => v.TemplateId == t.Id)
                .OrderByDescending(v => v.Number)
                .Select(v => (Guid?)v.Id)
                .FirstOrDefault(),
            data.Read<TemplateVersion>()
                .Where(v => v.TemplateId == t.Id)
                .OrderByDescending(v => v.Number)
                .Select(v => v.ChapterCount)
                .FirstOrDefault(),
            data.Read<TemplateVersion>()
                .Where(v => v.TemplateId == t.Id)
                .OrderByDescending(v => v.Number)
                .Select(v => v.LessonCount)
                .FirstOrDefault(),
            t.UpdatedUtc ?? data.Read<TemplateVersion>()
                .Where(v => v.TemplateId == t.Id)
                .OrderByDescending(v => v.Number)
                .Select(v => (DateTimeOffset?)v.CreatedUtc)
                .FirstOrDefault());

    public async Task<StudioBank> Bank(string? search, int page, CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        var q = LmsError.Text(search, 80, true);
        var rows = await Cards(q)
            .Skip(PageOffset(page)).Take(PageSize + 1).ToArrayAsync(ct);
        return new(rows.Take(PageSize).ToArray(), rows.Length > PageSize, flags.ScheduleEnabled);
    }

    public async Task<StudioVersionList> Versions(Guid templateId, int page, CancellationToken ct)
    {
        _ = await Own(templateId, false, ct);
        var rows = await data.Read<TemplateVersion>().Where(v => v.TemplateId == templateId).OrderByDescending(v => v.Number)
            .Skip(PageOffset(page)).Take(PageSize + 1).Select(v => new StudioVersionHeader(v.Id, v.Number, v.CreatedUtc)).ToArrayAsync(ct);
        return new(rows.Take(PageSize).ToArray(), rows.Length > PageSize);
    }

    public async Task<StudioTemplateDetails> Detail(Guid templateId, Guid? versionId, CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        // Filter by the entity key inside Cards, before the DTO projection, so EF can translate it.
        var card = await Cards("", templateId).SingleOrDefaultAsync(ct) ?? throw LmsError.Missing();
        if ((versionId ?? card.LatestVersion) is not { } key) return new(card, null, 2, new([]), []);
        var version = await data.Read<TemplateVersion>().SingleOrDefaultAsync(v => v.Id == key && v.TemplateId == templateId, ct) ?? throw LmsError.Missing();
        var (format, content, title, desc) = StudioDocuments.Read(version);
        var media = StudioDocuments.MediaIds(content); // V1 read may still have missing media. Report it through the empty metadata entries in UI.
        var files = await data.Read<Media>().Where(m => media.Contains(m.Id)).Select(m => new FileRow(m.Id, m.Name, m.Bytes, m.Extension)).ToArrayAsync(ct);
        if (versionId.HasValue && title is not null) card = card with { Title = title, Description = desc };
        return new(card, version.Id, format, content, files);
    }

    public async Task<Guid> Create(StudioCreateTemplate input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        await access.AsTeacher(LmsPermissions.Template, ct);
        if (!await AllowedCourses().AnyAsync(x => x.Id == input.CourseId, ct)) throw LmsError.Missing();
        var row = new CourseTemplate
        {
            CourseId = input.CourseId,
            OwnerId = access.ActorId,
            Active = true,
            Title = LmsError.Text(input.Title, 180),
            Description = LmsError.Text(input.Description, 180, true),
            UpdatedUtc = data.Now
        };
        data.Db.Add(row);
        return await data.Finish(tx, row.Id, row.Id, ct);
    }

    public async Task<StudioVersionSaved> Save(Guid templateId, StudioSaveVersion input, CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        var content = StudioDocuments.Validate(input.Content);
        await using var tx = await data.Transaction(ct);
        var template = await Own(templateId, true, ct);
        RequireRevision(template.Revision, input.ExpectedRevision);
        await VerifyMedia(template, content, ct);
        template.Title = LmsError.Text(input.Title, 180);
        template.Description = LmsError.Text(input.Description, 180, true);
        var version = await Append(template, content, ct);
        return await data.Finish(tx, new StudioVersionSaved(version.Id, version.Number, template.Revision), template.Id, ct);
    }

    public async Task<bool> Archive(Guid templateId, StudioRevision input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var row = await Own(templateId, true, ct);
        RequireRevision(row.Revision, input.ExpectedRevision);
        row.Active = false;
        row.Revision = Guid.NewGuid();
        row.UpdatedUtc = data.Now;
        return await data.Finish(tx, true, row.Id, ct);
    }

    internal async Task<TemplateVersion> Append(CourseTemplate template, StudioContent content, CancellationToken ct)
    {
        var last = await data.Read<TemplateVersion>().Where(v => v.TemplateId == template.Id).MaxAsync(v => (int?)v.Number, ct) ?? 0;
        var version = new TemplateVersion
        {
            TemplateId = template.Id,
            Number = last + 1,
            CreatedUtc = data.Now,
            ChapterCount = content.Chapters.Length,
            LessonCount = content.Chapters.Sum(c => c.Lessons.Length),
            Snapshot = StudioDocuments.Write(content, template.Title, template.Description)
        };
        data.Db.Add(version);
        data.Db.AddRange(StudioDocuments.MediaIds(content).Select(id => new TemplateVersionMedia { TemplateVersionId = version.Id, MediaId = id }));
        template.Revision = Guid.NewGuid();
        template.UpdatedUtc = version.CreatedUtc;
        return version;
    }

    internal async Task VerifyMedia(CourseTemplate template, StudioContent content, CancellationToken ct)
    {
        var ids = StudioDocuments.MediaIds(content);
        if (ids.Length == 0) return;
        var fromVersion = from p in data.Read<TemplateVersionMedia>()
                          join v in data.Read<TemplateVersion>() on p.TemplateVersionId equals v.Id
                          where v.TemplateId == template.Id
                          select p.MediaId;
        var media = await data.Read<Media>().Where(m => ids.Contains(m.Id) &&
            (fromVersion.Contains(m.Id) || m.OwnerId == template.OwnerId && m.TargetId == template.Id &&
                (m.Purpose == MediaPurpose.TemplateMaterial || m.Purpose == MediaPurpose.TemplateCover))).ToDictionaryAsync(m => m.Id, ct);
        foreach (var lesson in content.Chapters.SelectMany(c => c.Lessons))
        {
            if (lesson.MediaIds.Any(id => !media.TryGetValue(id, out var m) || m.Purpose is not (MediaPurpose.TemplateMaterial or MediaPurpose.LessonMaterial)))
                throw LmsError.Invalid("دست‌کم یک پیوست ذخیره یا به این قالب اختصاص نیافته است.");
            foreach (var material in lesson.Materials ?? [])
            {
                if (material.MediaId is { } file && (!media.TryGetValue(file, out var row) ||
                    row.Purpose is not (MediaPurpose.CurriculumFile or MediaPurpose.TemplateMaterial or MediaPurpose.LessonMaterial) ||
                    LearningKinds.FromExtension(row.Extension) != material.Kind))
                    throw LmsError.Invalid("فایل اصلی یکی از منابع نوع‌دار به قالب اختصاص نیافته است.");
                if (material.CoverId is { } thumb && (!media.TryGetValue(thumb, out var picture) ||
                    picture.Purpose is not (MediaPurpose.CurriculumCover or MediaPurpose.TemplateCover) || !LearningKinds.IsCover(picture.Extension)))
                    throw LmsError.Invalid("کاور یکی از منابع نوع‌دار به قالب اختصاص نیافته است.");
            }
            if (lesson.CoverId is { } cover && (!media.TryGetValue(cover, out var image) || image.Purpose != MediaPurpose.TemplateCover ||
                !lesson.MediaIds.Any(id => media[id].Extension == ".mp4")))
                throw LmsError.Invalid("کاور فقط برای درسک دارای ویدئو و از نوع کاور اختصاص‌یافته مجاز است.");
        }
    }

    internal static void RequireRevision(Guid actual, Guid expected)
    {
        if (actual != expected) throw LmsError.Conflict("نسخه تغییر کرده است؛ دادهٔ تازه را دریافت کنید. تغییر محلی را پیش از بارگیری ذخیره کنید.");
    }

    private static int PageOffset(int page) => page is >= 1 and <= 10000 ? (page - 1) * PageSize : throw LmsError.Invalid("شمارهٔ صفحه نامعتبر است.");
}
