using Microsoft.EntityFrameworkCore;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed partial class LmsOfferings
{
    public async Task<TemplateRow[]> TemplatesAsync(CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        return await (from t in data.Read<CourseTemplate>() join v in data.Read<TemplateVersion>() on t.Id equals v.TemplateId
                      where t.OwnerId == access.ActorId && t.Active orderby v.CreatedUtc descending
                      select new TemplateRow(t.Id, t.CourseId, v.Id, v.Number, t.Title, v.CreatedUtc)).Take(500).ToArrayAsync(ct);
    }

    public async Task<Guid> SaveTemplateAsync(Guid offeringId, TemplateSave input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await access.Offering(offeringId, ct);
        await access.AsTeaching(info, LmsPermissions.Template, ct); // source is readable even after the term closes
        var title = LmsError.Text(input.Title, 180);
        var template = input.TemplateId is { } id ? await studioTemplates.Own(id, true, ct)
            : new CourseTemplate { OwnerId = access.ActorId, CourseId = info.Course.Id, Title = title, Active = true };
        if (template.CourseId != info.Course.Id) throw LmsError.Missing();
        var content = await ChapterRows(offeringId, null, true, data.Now, ct);
        if (!content.Any(x => x.Active)) throw LmsError.Conflict("ارائه فصل فعال برای ذخیره ندارد.");
        // Only reuse item keys when the offering's receipt proves it belongs to this template family.
        var hasLineage = await (from d in data.Read<OfferingScheduleDraft>()
                                join v in data.Read<TemplateVersion>() on d.TemplateVersionId equals v.Id
                                where d.OfferingId == offeringId && d.State == ScheduleDraftState.Applied && v.TemplateId == template.Id
                                select d.OfferingId).AnyAsync(ct);
        StudioContent? prior = null;
        if (hasLineage)
        {
            var copy = await (from d in data.Read<OfferingScheduleDraft>() join v in data.Read<TemplateVersion>() on d.TemplateVersionId equals v.Id
                              where d.OfferingId == offeringId select v).SingleAsync(ct);
            prior = StudioDocuments.Read(copy).Content;
        }
        var lessonIds = content.SelectMany(c => c.Lessons).Select(l => l.Id).ToArray();
        var resources = (await data.Read<LearningMaterial>().Where(m => lessonIds.Contains(m.LessonId) && !m.Removed)
            .OrderBy(m => m.Position).ThenBy(m => m.Key).ToArrayAsync(ct)).ToLookup(m => m.LessonId);
        var featured = await data.Read<Lesson>().Where(l => lessonIds.Contains(l.Id) && l.FeaturedMaterialKey != null)
            .ToDictionaryAsync(l => l.Id, l => l.FeaturedMaterialKey, ct);
        StudioChapter Convert(ChapterRow chapter)
        {
            var stored = hasLineage ? prior?.Chapters.FirstOrDefault(c => c.Key == chapter.TemplateItemKey) : null;
            var lessons = chapter.Lessons.Where(l => l.Active).Select(l =>
            {
                var previous = stored?.Lessons.FirstOrDefault(x => x.Key == l.TemplateItemKey);
                var key = previous?.Key ?? Guid.NewGuid();
                return new StudioLesson(key, l.Title, l.Summary, l.Body, l.Position, previous?.StartDay, previous?.EndDay,
                    l.Files.Select(f => f.Id).ToArray(), l.CoverMediaId,
                    resources[l.Id].Select(m => new StudioResource(m.Key, m.Kind, m.Title, m.Description, m.Body, m.Alt, m.MediaId, m.CoverId)).ToArray(),
                    featured.GetValueOrDefault(l.Id));
            }).ToArray();
            return new(stored?.Key ?? Guid.NewGuid(), chapter.Title, chapter.Position, stored?.StartDay, stored?.EndDay, lessons, [], chapter.Summary);
        }

        if (input.TemplateId is null) data.Db.Add(template);
        var copyContent = new StudioContent(content.Where(c => c.Active).Select(Convert).ToArray());
        var version = await studioTemplates.Append(template, copyContent, ct);
        return await data.Finish(tx, version.Id, version.Id, ct);
    }

    public async Task<bool> ApplyTemplateAsync(Guid offeringId, TemplateApply input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await Editable(offeringId, LmsPermissions.Template, ct);
        await access.AsTeacher(LmsPermissions.Content, ct);
        var (template, version) = await studioTemplates.Version(input.VersionId, info.Course.Id, ct);
        if (info.Offering.PublishedUtc is not null || await data.Read<Chapter>().AnyAsync(x => x.OfferingId == offeringId, ct))
            throw LmsError.Conflict("استفاده از قالب فقط برای ارائهٔ خالی و منتشرنشده مجاز است.");
        var (_, draft, _, _) = StudioDocuments.Read(version);
        await studioTemplates.VerifyMedia(template, draft, ct);
        materializer.Add(offeringId, draft, null, null); // Legacy operation copies structure only; no dates or publication.
        var o = await data.Find<Offering>(offeringId, ct);
        o.Revision = Guid.NewGuid();
        return await data.Finish(tx, true, offeringId, ct);
    }
}
