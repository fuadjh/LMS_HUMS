using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class LearningValidation(LmsData data, LearningReadModel read, LmsFiles files)
{
    internal static bool Pending(Chapter c) => c.PublishedUtc == null && (c.Active || c.PendingBaseId != null);
    internal static bool HasChanges(Offering o, Chapter[] pending) => pending.Length > 0 || o.DraftIntroduction != o.PublishedIntroduction;

    internal async Task<Chapter[]> Changes(Chapter[] outline, CancellationToken ct)
    {
        var pending = outline.Where(Pending).ToArray();
        var copies = pending.Where(c => c.PendingBaseId.HasValue && c.Active).ToArray();
        if (copies.Length == 0) return pending;
        var oldIds = copies.Select(c => c.PendingBaseId!.Value).ToArray();
        var ids = copies.Select(c => c.Id).Concat(oldIds).ToArray();
        var before = await data.Read<Chapter>().Where(c => oldIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var lessonRows = await data.Read<Lesson>().Where(l => ids.Contains(l.ChapterId) && l.Active).OrderBy(l => l.Position).ThenBy(l => l.Id).ToArrayAsync(ct);
        var lessons = lessonRows.ToLookup(l => l.ChapterId);
        var materials = await read.Materials(lessonRows, true, true, ct);
        var same = copies.Where(c => before.TryGetValue(c.PendingBaseId!.Value, out var prior) &&
            !prior.Retired && prior.Active && c.Title == prior.Title && c.Summary == prior.Summary && c.Position == prior.Position &&
            c.VisibleFromUtc == prior.VisibleFromUtc && c.VisibleUntilUtc == prior.VisibleUntilUtc &&
            lessons[c.Id].Select(LessonIdentity).SequenceEqual(lessons[prior.Id].Select(LessonIdentity)) &&
            lessons[c.Id].Zip(lessons[prior.Id]).All(p => materials[p.First.Id].Select(MaterialIdentity).SequenceEqual(materials[p.Second.Id].Select(MaterialIdentity))))
            .Select(c => c.Id).ToHashSet();
        return pending.Where(c => !same.Contains(c.Id)).ToArray();

        static object LessonIdentity(Lesson l) => (LearningReadModel.Key(l), l.Title, l.Summary, l.Position,
            l.VisibleFromUtc, l.VisibleUntilUtc, l.FeaturedMaterialKey, l.NeedsAvailabilityReview);
        static object MaterialIdentity(LearningMaterial m) => (m.Key, m.Title, m.Description, m.Kind, m.Body, m.Alt, m.MediaId, m.CoverId);
    }

    internal async Task<LearningIssue[]> Check(OfferingInfo info, Guid? chapter, Chapter[] pending, CancellationToken ct)
    {
        var o = info.Offering;
        var issues = new List<LearningIssue>();
        if (string.IsNullOrWhiteSpace(o.DraftIntroduction ?? o.PublishedIntroduction))
            issues.Add(new("introduction", "معرفی کوتاه دوره را کامل کنید."));
        var selected = pending.Where(c => chapter == null || LearningReadModel.Key(c) == chapter).ToArray();
        if (chapter.HasValue && selected.Length == 0) issues.Add(new("nothing-to-publish", "تغییری برای انتشار این فصل وجود ندارد.", chapter));
        var released = await read.Chapters(o.Id, false).ToArrayAsync(ct);
        if (!selected.Any(c => c.Active) && !released.Any(c => selected.All(s => s.PendingBaseId != c.Id)))
            issues.Add(new("empty-course", "نسخهٔ قابل مشاهده باید دست‌کم یک فصل فعال داشته باشد."));
        var ids = selected.Where(c => c.Active).Select(c => c.Id).ToArray();
        var lessons = await data.Read<Lesson>().Where(l => ids.Contains(l.ChapterId) && l.Active).OrderBy(l => l.Position).ThenBy(l => l.Id).ToArrayAsync(ct);
        var materials = await read.Materials(lessons, true, true, ct);
        var mediaIds = materials.SelectMany(x => x).Where(m => m.MediaId.HasValue).Select(m => m.MediaId!.Value).Distinct().ToArray();
        var actual = (await data.Read<Media>().Where(m => mediaIds.Contains(m.Id)).Select(m => new { m.Id, m.Extension }).ToArrayAsync(ct))
            .ToDictionary(x => x.Id, x => x.Extension);
        foreach (var c in selected.Where(c => c.Active))
        {
            var cid = LearningReadModel.Key(c);
            var entries = lessons.Where(l => l.ChapterId == c.Id).ToArray();
            try { LmsOfferings.GuardWindow(info.Term, c.VisibleFromUtc, c.VisibleUntilUtc); }
            catch (UseCaseException) { issues.Add(new("chapter-availability", $"بازهٔ نمایش فصل «{c.Title}» معتبر نیست.", cid)); }
            if (string.IsNullOrWhiteSpace(c.Title) || string.IsNullOrWhiteSpace(c.Summary))
                issues.Add(new("chapter-outline", $"عنوان و خلاصهٔ فصل «{c.Title}» را تکمیل کنید.", cid));
            if (entries.Length == 0) issues.Add(new("empty-chapter", $"برای فصل «{c.Title}» دست‌کم یک درس اضافه کنید.", cid));
            foreach (var l in entries)
            {
                var lid = LearningReadModel.Key(l);
                if (string.IsNullOrWhiteSpace(l.Title) || string.IsNullOrWhiteSpace(l.Summary))
                    issues.Add(new("lesson-outline", $"عنوان و خلاصهٔ درس «{l.Title}» را تکمیل کنید.", cid, lid));
                var invalidWindow = l.NeedsAvailabilityReview;
                try
                {
                    LmsOfferings.GuardWindow(info.Term, l.VisibleFromUtc, l.VisibleUntilUtc);
                    LmsOfferings.GuardChildWindow(c.VisibleFromUtc, c.VisibleUntilUtc, l.VisibleFromUtc, l.VisibleUntilUtc);
                }
                catch (UseCaseException) { invalidWindow = true; }
                if (invalidWindow)
                    issues.Add(new("lesson-availability", $"بازهٔ دسترسی درس «{l.Title}» را بازبینی و ذخیره کنید.", cid, lid));
                if (!materials[l.Id].Any()) issues.Add(new("empty-lesson", $"درس «{l.Title}» به دست‌کم یک محتوا نیاز دارد.", cid, lid));
                foreach (var m in materials[l.Id])
                {
                    var ready = m.Kind == LearningKind.Text ? !string.IsNullOrWhiteSpace(m.Body)
                        : m.MediaId is { } id && actual.TryGetValue(id, out var ext) && LearningKinds.FromExtension(ext) == m.Kind && files.Exists(id);
                    if (string.IsNullOrWhiteSpace(m.Title) || string.IsNullOrWhiteSpace(m.Description) || !ready)
                        issues.Add(new("material-incomplete", $"عنوان، توضیح و فایل آماده یا متن «{m.Title}» را بررسی کنید.", cid, lid, m.Key));
                }
            }
        }
        return issues.ToArray();
    }
}

internal sealed class LearningCommands(LmsData data, LmsAccess access, LmsOfferings offerings, LmsFiles files,
    LearningReadModel read, LearningAccess readers, LearningValidation validation) : ILearningCommands
{
    public async Task<LearningSaved> Save(Guid offering, LearningEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await offerings.Editable(offering, LmsPermissions.Content, ct);
        var o = await data.Find<Offering>(offering, ct);
        StudioTemplates.RequireRevision(o.Revision, input.ExpectedRevision);
        Guid? chapterKey = input.ChapterId, lessonKey = input.LessonId, materialKey = input.MaterialKey;
        if (input.Kind == LearningEditKind.Introduce)
        {
            var intro = LmsError.Text(input.Summary, 320);
            if (intro == (o.DraftIntroduction ?? o.PublishedIntroduction)) return new(o.Revision);
            o.DraftIntroduction = intro;
        }
        else if (input.Kind == LearningEditKind.Chapter && chapterKey is null)
        {
            var positions = read.Chapters(offering, true);
            var last = await positions.MaxAsync(c => (int?)c.Position, ct) ?? 0;
            if (await positions.CountAsync(c => c.Active, ct) >= 60) throw LmsError.Invalid("حداکثر ۶۰ فصل فعال مجاز است.");
            var chapter = new Chapter { OfferingId = offering, Position = LmsOfferings.Position(last + 1), Active = true, WorkspaceManaged = true };
            EditChapter(chapter, info.Term, input);
            data.Db.Add(chapter);
            chapterKey = chapter.Id;
        }
        else
        {
            var current = await read.Chapters(offering, true)
                .SingleOrDefaultAsync(candidate => (candidate.StableChapterId ?? candidate.Id) == chapterKey, ct) ?? throw LmsError.Missing();
            if (!current.Active) throw LmsError.Missing();
            if (input.Kind == LearningEditKind.Chapter)
            {
                var change = new Chapter(); EditChapter(change, info.Term, input);
                if (current.Title == change.Title && current.Summary == change.Summary && current.VisibleFromUtc == change.VisibleFromUtc &&
                    current.VisibleUntilUtc == change.VisibleUntilUtc) return new(o.Revision, chapterKey);
            }
            var oldLessons = await data.Read<Lesson>().Where(lesson => lesson.ChapterId == current.Id).OrderBy(lesson => lesson.Position).ToArrayAsync(ct);
            var original = lessonKey is { } key ? oldLessons.SingleOrDefault(lesson => lesson.Active && LearningReadModel.Key(lesson) == key) ?? throw LmsError.Missing() : null;
            if (input.Kind == LearningEditKind.Lesson && original is not null)
            {
                var change = new Lesson(); EditLesson(change, current, info.Term, input);
                if (!original.NeedsAvailabilityReview && original.Title == change.Title && original.Summary == change.Summary &&
                    original.VisibleFromUtc == change.VisibleFromUtc && original.VisibleUntilUtc == change.VisibleUntilUtc)
                    return new(o.Revision, chapterKey, lessonKey);
            }
            var originalMaterials = await read.Materials(oldLessons, true, true, ct);
            if (input.Kind == LearningEditKind.Feature && original is not null && original.FeaturedMaterialKey == materialKey &&
                originalMaterials[original.Id].Any(m => m.Key == materialKey)) return new(o.Revision, chapterKey, lessonKey, materialKey);
            var draft = await OpenDraft(current, oldLessons, originalMaterials, ct);
            var c = draft.Chapter;
            var l = original is null ? null : draft.Lessons.Single(x => LearningReadModel.Key(x) == LearningReadModel.Key(original));
            switch (input.Kind)
            {
                case LearningEditKind.Chapter:
                    EditChapter(c, info.Term, input);
                    foreach (var child in draft.Lessons.Where(x => x.Active))
                        LmsOfferings.GuardChildWindow(c.VisibleFromUtc, c.VisibleUntilUtc, child.VisibleFromUtc, child.VisibleUntilUtc);
                    break;
                case LearningEditKind.RemoveChapter:
                    c.Active = false;
                    if (c.PendingBaseId == null) c.Retired = true; // a never-published chapter is excluded without deleting history
                    break;
                case LearningEditKind.Lesson:
                    if (l is null)
                    {
                        if (draft.Lessons.Count(x => x.Active) >= 100) throw LmsError.Invalid("حداکثر ۱۰۰ درس فعال برای فصل مجاز است.");
                        l = new Lesson { ChapterId = c.Id, Active = true, Position = LmsOfferings.Position((draft.Lessons.Select(x => (int?)x.Position).Max() ?? 0) + 1) };
                        data.Db.Add(l);
                    }
                    EditLesson(l, c, info.Term, input);
                    lessonKey = LearningReadModel.Key(l);
                    break;
                case LearningEditKind.RemoveLesson:
                    (l ?? throw LmsError.Missing()).Active = false;
                    break;
                case LearningEditKind.Material:
                case LearningEditKind.RemoveMaterial:
                case LearningEditKind.Feature:
                    if (l is null || original is null) throw LmsError.Missing();
                    var items = draft.Copied is { } copied ? copied[l.Id].ToArray() :
                        await MaterializeOldLesson(l, originalMaterials[original.Id].ToArray(), ct);
                    var item = materialKey is { } k ? items.SingleOrDefault(x => x.Key == k) ?? throw LmsError.Missing() : null;
                    if (input.Kind == LearningEditKind.Feature)
                        l.FeaturedMaterialKey = (item ?? throw LmsError.Missing()).Key;
                    else if (input.Kind == LearningEditKind.RemoveMaterial)
                    {
                        (item ?? throw LmsError.Missing()).Removed = true;
                        if (l.FeaturedMaterialKey == item.Key) l.FeaturedMaterialKey = null;
                    }
                    else
                    {
                        if (item is null)
                        {
                            if (items.Length >= 16) throw LmsError.Invalid("حداکثر ۱۶ محتوا برای هر درس مجاز است.");
                            var last = await data.Read<LearningMaterial>().Where(m => m.LessonId == l.Id).MaxAsync(m => (int?)m.Position, ct);
                            item = new() { LessonId = l.Id, Position = Math.Max(items.Select(m => (int?)m.Position).Max() ?? 0, last ?? 0) + 1,
                                Kind = input.MaterialKind ?? throw LmsError.Invalid("نوع محتوا را انتخاب کنید.") };
                            if (!Enum.IsDefined(item.Kind) || item.Kind == LearningKind.LegacyFile) throw LmsError.Invalid("نوع محتوا معتبر نیست.");
                            data.Db.Add(item);
                        }
                        await EditMaterial(offering, info, item, input, ct);
                        materialKey = item.Key;
                    }
                    break;
                default: throw LmsError.Missing();
            }
            c.WorkspaceManaged = true;
        }
        o.Revision = Guid.NewGuid();
        return await data.Finish(tx, new LearningSaved(o.Revision, chapterKey, lessonKey, materialKey), offering, ct);
    }

    private sealed record Draft(Chapter Chapter, Lesson[] Lessons, ILookup<Guid, LearningMaterial>? Copied);
    private async Task<Draft> OpenDraft(Chapter current, Lesson[] oldLessons, ILookup<Guid, LearningMaterial> resources, CancellationToken ct)
    {
        if (current.PublishedUtc is null)
            return new(await data.Find<Chapter>(current.Id, ct), await data.Db.Set<Lesson>().Where(x => x.ChapterId == current.Id).ToArrayAsync(ct), null);
        var chapter = new Chapter { OfferingId = current.OfferingId, StableChapterId = LearningReadModel.Key(current), PendingBaseId = current.Id,
            Title = current.Title, Summary = current.Summary, Position = current.Position, Active = current.Active, WorkspaceManaged = true,
            TemplateItemKey = current.TemplateItemKey, VisibleFromUtc = current.VisibleFromUtc, VisibleUntilUtc = current.VisibleUntilUtc };
        data.Db.Add(chapter);
        var copies = new List<LearningMaterial>();
        var lessons = oldLessons.Select(l =>
        {
            var copy = new Lesson { ChapterId = chapter.Id, StableLessonId = LearningReadModel.Key(l), Title = l.Title, Summary = l.Summary,
                Position = l.Position, Active = l.Active, TemplateItemKey = l.TemplateItemKey, VisibleFromUtc = l.VisibleFromUtc,
                VisibleUntilUtc = l.VisibleUntilUtc, NeedsAvailabilityReview = l.NeedsAvailabilityReview, FeaturedMaterialKey = l.FeaturedMaterialKey };
            copies.AddRange(CopyMaterials(copy.Id, resources[l.Id]));
            return copy;
        }).ToArray();
        data.Db.AddRange(lessons);
        data.Db.AddRange(copies);
        return new(chapter, lessons, copies.ToLookup(x => x.LessonId));
    }

    private static IEnumerable<LearningMaterial> CopyMaterials(Guid lessonId, IEnumerable<LearningMaterial> sources) => sources.Select((m, i) =>
        new LearningMaterial { Key = m.Key, LessonId = lessonId, Position = i + 1, Kind = m.Kind, Title = m.Title,
            Description = m.Description, Body = m.Body, Alt = m.Alt, MediaId = m.MediaId, CoverId = m.CoverId });

    private async Task<LearningMaterial[]> MaterializeOldLesson(Lesson lesson, LearningMaterial[] active, CancellationToken ct)
    {
        var stored = await data.Db.Set<LearningMaterial>().Where(x => x.LessonId == lesson.Id && !x.Removed).ToArrayAsync(ct);
        if (stored.Length == active.Length) return stored;
        var existing = stored.Select(x => x.Key).ToHashSet();
        var add = CopyMaterials(lesson.Id, active.Where(x => !existing.Contains(x.Key))).ToArray();
        data.Db.AddRange(add);
        // Only unpublished physical rows are changed; assets (and published editions) are never deleted here.
        lesson.Body = "";
        lesson.CoverMediaId = null;
        data.Db.RemoveRange(await data.Db.Set<LessonFile>().Where(x => x.LessonId == lesson.Id).ToArrayAsync(ct));
        return stored.Concat(add).ToArray();
    }

    private async Task EditMaterial(Guid offering, OfferingInfo info, LearningMaterial m, LearningEdit input, CancellationToken ct)
    {
        if (input.MaterialKind is { } kind && kind != m.Kind) throw LmsError.Invalid("نوع محتوای موجود تغییر نمی‌کند.");
        m.Title = LmsError.Text(input.Title, 90);
        m.Description = LmsError.Text(input.Summary, 450);
        m.Alt = m.Kind == LearningKind.Image ? LmsError.Text(input.Alt, 250, true) : null;
        if (m.Kind == LearningKind.Text)
        {
            if (input.MediaId.HasValue) throw LmsError.Invalid("محتوای متنی فایل اصلی ندارد.");
            m.Body = LmsError.Text(input.Body, 12000);
        }
        else
        {
            if (input.MediaId is { } add)
            {
                var file = await StagedMedia(add, MediaPurpose.CurriculumFile);
                if (LearningKinds.FromExtension(file.Extension) != m.Kind) throw LmsError.Invalid("نوع فایل با نوع محتوا سازگار نیست.");
                m.MediaId = add;
            }
            if (m.MediaId is not { } id || !files.Exists(id)) throw LmsError.Invalid("فایل معتبر را ابتدا بارگذاری کنید.");
        }
        switch (input.CoverAction)
        {
            case LearningCoverAction.Keep when input.CoverId == null: break;
            case LearningCoverAction.Remove when input.CoverId == null: m.CoverId = null; break;
            case LearningCoverAction.Replace when input.CoverId is { } cover:
                var image = await StagedMedia(cover, MediaPurpose.CurriculumCover);
                if (!LearningKinds.IsCover(image.Extension)) throw LmsError.Invalid("قالب کاور مجاز نیست.");
                m.CoverId = cover; break;
            default: throw LmsError.Invalid("اقدام کاور معتبر نیست.");
        }
        async Task<Media> StagedMedia(Guid id, MediaPurpose purpose)
        {
            await access.AsTeaching(info, LmsPermissions.LessonUpload, ct);
            var file = await data.Read<Media>().SingleOrDefaultAsync(x => x.Id == id && x.Purpose == purpose && x.TargetId == offering && x.OwnerId == access.ActorId, ct);
            if (file is null || !files.Exists(id)) throw LmsError.Missing();
            return file;
        }
    }

    private static void EditChapter(Chapter c, Term term, LearningEdit input)
    {
        c.Title = LmsError.Text(input.Title, 90); c.Summary = LmsError.Text(input.Summary, 320);
        (c.VisibleFromUtc, c.VisibleUntilUtc) = Window(term, input);
    }
    private static void EditLesson(Lesson l, Chapter c, Term term, LearningEdit input)
    {
        l.Title = LmsError.Text(input.Title, 90); l.Summary = LmsError.Text(input.Summary, 320);
        (l.VisibleFromUtc, l.VisibleUntilUtc) = Window(term, input);
        LmsOfferings.GuardChildWindow(c.VisibleFromUtc, c.VisibleUntilUtc, l.VisibleFromUtc, l.VisibleUntilUtc);
        l.NeedsAvailabilityReview = false;
    }
    private static (DateTimeOffset?, DateTimeOffset?) Window(Term term, LearningEdit input)
    {
        DateTimeOffset? start = input.OpensUtc is { } a ? LmsError.Instant(a) : null;
        DateTimeOffset? end = input.ClosesUtc is { } b ? LmsError.Instant(b) : null;
        LmsOfferings.GuardWindow(term, start, end);
        return (start, end);
    }

    public async Task<LearningPublished> Publish(Guid offering, LearningPublish input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await offerings.Editable(offering, LmsPermissions.Publish, ct);
        var o = await data.Find<Offering>(offering, ct);
        if (input.ExpectedRevision != Guid.Empty && o.PublishedCurriculumToken == input.ExpectedRevision && o.PublishedCurriculumScope == input.ChapterId)
            return new(o.Revision, o.CurriculumVersion);
        StudioTemplates.RequireRevision(o.Revision, input.ExpectedRevision);
        var outline = await read.Chapters(offering, true).ToArrayAsync(ct);
        var pending = await validation.Changes(outline, ct);
        if (!LearningValidation.HasChanges(o, pending)) return new(o.Revision, o.CurriculumVersion);
        var issues = await validation.Check(info, input.ChapterId, pending, ct);
        if (issues.Length > 0) throw LmsError.Conflict(issues[0].Message);
        var now = data.Now;
        var selected = pending.Where(c => input.ChapterId == null || LearningReadModel.Key(c) == input.ChapterId).ToArray();
        foreach (var chapter in selected.Where(c => c.Active))
        {
            LmsOfferings.GuardWindow(info.Term, chapter.VisibleFromUtc, chapter.VisibleUntilUtc);
            var lessons = await data.Read<Lesson>().Where(l => l.ChapterId == chapter.Id && l.Active).ToArrayAsync(ct);
            foreach (var l in lessons)
            {
                LmsOfferings.GuardWindow(info.Term, l.VisibleFromUtc, l.VisibleUntilUtc);
                LmsOfferings.GuardChildWindow(chapter.VisibleFromUtc, chapter.VisibleUntilUtc, l.VisibleFromUtc, l.VisibleUntilUtc);
            }
        }
        // The filtered slot indexes reserve a chapter's position. Retire first, activate second, both inside one transaction.
        foreach (var id in selected.Where(c => c.PendingBaseId.HasValue).Select(c => c.PendingBaseId!.Value))
        {
            var previous = await offerings.FindChapter(offering, id, ct);
            if (previous.Retired || previous.PublishedUtc is null) throw LmsError.Conflict("فصل مبنا تغییر کرده است.");
            previous.Retired = true;
        }
        await data.Save(ct);
        foreach (var item in selected)
        {
            var c = await offerings.FindChapter(offering, item.Id, ct);
            c.PendingBaseId = null;
            c.PublishedUtc = now;
            c.Retired = !c.Active;
        }
        o.PublishedIntroduction = o.DraftIntroduction ?? o.PublishedIntroduction;
        o.CurriculumVersion++;
        o.PublishedCurriculumToken = input.ExpectedRevision;
        o.PublishedCurriculumScope = input.ChapterId;
        o.Revision = Guid.NewGuid();
        return await data.Finish(tx, new LearningPublished(o.Revision, o.CurriculumVersion), offering, ct);
    }

    public async Task<bool> Complete(Guid offering, Guid lesson, bool completed, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var ctx = await readers.Read(offering, "student", ct);
        if (!ctx.CanComplete || ctx.Enrollment is not { } own) throw LmsError.Denied();
        var query = from l in data.Read<Lesson>() join c in read.Chapters(offering, false) on l.ChapterId equals c.Id
                    where (l.StableLessonId ?? l.Id) == lesson select new { l, c };
        var row = await query.SingleOrDefaultAsync(ct) ?? throw LmsError.Missing();
        if (!AcademicRules.CanReadLesson(data.Now, row.c, row.l)) throw LmsError.Missing();
        await LmsEnrollments.SetCompletion(data, own.Id, lesson, completed, ct);
        return await data.Finish(tx, completed, lesson, ct);
    }
}
