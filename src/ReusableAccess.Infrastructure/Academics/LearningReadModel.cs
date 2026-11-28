using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class LearningReadModel(LmsData data, LmsFiles files)
{
    internal sealed record Entry(Chapter Chapter, Lesson Lesson);
    internal sealed record Tree(Chapter[] Chapters, Entry[] Entries, ILookup<Guid, LearningMaterial> Materials);
    internal static Guid Key(Chapter row) => row.StableChapterId ?? row.Id;
    internal static Guid Key(Lesson row) => row.StableLessonId ?? row.Id;
    internal bool HasFile(Guid id) => files.Exists(id);
    // A published chapter is immutable. Its replacement, when any, is selected only in the editing view.
    internal IQueryable<Chapter> Chapters(Guid offering, bool draft)
    {
        var all = data.Read<Chapter>().Where(c => c.OfferingId == offering && !c.Retired);
        return draft
            ? all.Where(c => !all.Any(copy => copy.PendingBaseId == c.Id))
            : all.Where(c => c.PendingBaseId == null && c.Active && c.PublishedUtc != null);
    }

    internal async Task<Tree> Load(Guid offering, bool draft, DateTimeOffset? visibility, CancellationToken ct)
    {
        var chapters = await Chapters(offering, draft).OrderBy(c => c.Position).ThenBy(c => c.Id).ToArrayAsync(ct);
        if (visibility is { } now && !draft) chapters = chapters.Where(c => AcademicRules.CanReadChapter(now, c)).ToArray();
        var ids = chapters.Select(c => c.Id).ToArray();
        var lessons = await data.Read<Lesson>().Where(l => ids.Contains(l.ChapterId) && l.Active)
            .OrderBy(l => l.Position).ThenBy(l => l.Id)
            // No lesson text or resource description is selected by the curriculum query.
            .Select(l => new Lesson { Id = l.Id, StableLessonId = l.StableLessonId, ChapterId = l.ChapterId,
                Title = l.Title, Summary = l.Summary, Position = l.Position, Active = l.Active, FeaturedMaterialKey = l.FeaturedMaterialKey,
                NeedsAvailabilityReview = l.NeedsAvailabilityReview, VisibleFromUtc = l.VisibleFromUtc, VisibleUntilUtc = l.VisibleUntilUtc,
                CoverMediaId = l.CoverMediaId, Body = l.Body == "" ? "" : "…" }).ToArrayAsync(ct);
        var byChapter = lessons.ToLookup(l => l.ChapterId);
        var entries = chapters.Where(c => c.Active).SelectMany(c => byChapter[c.Id]
            .Where(l => draft || !visibility.HasValue || AcademicRules.CanReadLesson(visibility.Value, c, l)).Select(l => new Entry(c, l))).ToArray();
        return new(chapters, entries, await Materials(entries.Select(x => x.Lesson).ToArray(), false, false, ct));
    }

    internal async Task<ILookup<Guid, LearningMaterial>> Materials(Lesson[] lessons, bool details, bool body, CancellationToken ct)
    {
        var ids = lessons.Select(l => l.Id).ToArray();
        var saved = await data.Read<LearningMaterial>().Where(m => ids.Contains(m.LessonId) && !m.Removed)
            .Select(m => new LearningMaterial { Id = m.Id, Key = m.Key, LessonId = m.LessonId, Position = m.Position, Kind = m.Kind,
                Title = m.Title, Description = details ? m.Description : null, Body = body ? m.Body : null, Alt = details ? m.Alt : null,
                MediaId = m.MediaId, CoverId = m.CoverId }).ToListAsync(ct);
        var links = await (from link in data.Read<LessonFile>() join m in data.Read<Media>() on link.MediaId equals m.Id
                           where ids.Contains(link.LessonId)
                           orderby link.Position == null, link.Position, link.Id
                           select new { link.Id, link.LessonId, link.MediaId, m.Name, m.Extension }).ToArrayAsync(ct);
        var byLesson = links.ToLookup(x => x.LessonId);
        foreach (var lesson in lessons)
        {
            if (!string.IsNullOrWhiteSpace(lesson.Body))
                saved.Add(new LearningMaterial { Id = Key(lesson), Key = Key(lesson), LessonId = lesson.Id, Position = 0,
                    Kind = LearningKind.Text, Title = lesson.Title, Body = body ? lesson.Body : null });
            var i = 0;
            foreach (var link in byLesson[lesson.Id])
            {
                var kind = LearningKinds.FromExtension(link.Extension);
                saved.Add(new LearningMaterial { Id = link.Id, Key = link.Id, LessonId = lesson.Id, Position = ++i,
                    Kind = kind, Title = link.Name, MediaId = link.MediaId,
                    CoverId = kind == LearningKind.Video ? lesson.CoverMediaId : null });
            }
        }
        return saved.OrderBy(m => m.Position).ThenBy(m => m.Key).ToLookup(m => m.LessonId);
    }

    internal LearningThumbnail Thumbnail(LearningMaterial? material) => material is null ? new(LearningKind.Text) :
        new(material.Kind, material.CoverId is { } id && files.Exists(id) ? id : null);

    internal LearningLessonRow Row(Entry entry, ILookup<Guid, LearningMaterial> media, HashSet<Guid> done, bool draft = false)
    {
        var (c, l) = entry;
        var resources = media[l.Id];
        var featured = resources.FirstOrDefault(m => m.Key == l.FeaturedMaterialKey) ?? resources.FirstOrDefault();
        return new(Key(l), l.Position, l.Title, l.Summary, done.Contains(Key(l)), l.NeedsAvailabilityReview, resources.Count(),
            Thumbnail(featured), featured?.Key, draft ? l.VisibleFromUtc : AcademicRules.Later(c.VisibleFromUtc, l.VisibleFromUtc),
            draft ? l.VisibleUntilUtc : AcademicRules.Earlier(c.VisibleUntilUtc, l.VisibleUntilUtc));
    }

    internal LearningChapterRow[] Rows(Tree tree, HashSet<Guid> done, bool draft)
    {
        var entries = tree.Entries.ToLookup(x => x.Chapter.Id);
        return tree.Chapters.Where(c => draft || entries[c.Id].Any()).Select(c => new LearningChapterRow(Key(c), c.Position,
            c.Title, c.Summary, c.PublishedUtc != null, c.PublishedUtc == null, !c.Active, c.VisibleFromUtc, c.VisibleUntilUtc,
            entries[c.Id].Select(x => Row(x, tree.Materials, done, draft)).ToArray())).ToArray();
    }

    internal LearningMaterialRow MaterialRow(LearningMaterial m, bool edit = false) => new(m.Key, m.Position, m.Kind, m.Title,
        m.Description, Thumbnail(m), m.MediaId != null, m.Alt, edit ? m.Body : null);

    internal async Task<HashSet<Guid>> Done(Enrollment? enrollment, CancellationToken ct) => enrollment is null ? [] :
        (await data.Read<LessonProgress>().Where(p => p.EnrollmentId == enrollment.Id && p.ClearedUtc == null)
            .Select(p => p.LessonId).ToArrayAsync(ct)).ToHashSet();
}

internal sealed class LearningAccess(LmsData data, LmsAccess access)
{
    internal sealed record View(OfferingInfo Info, Enrollment? Enrollment, bool Visible, bool Draft, bool CanComplete);
    internal async Task<View> Read(Guid id, string view, CancellationToken ct)
    {
        await access.Load(ct);
        var info = await access.Offering(id, ct);
        var o = info.Offering;
        var published = o.Active && AcademicRules.CanSee(data.Now, o.PublishedUtc, o.VisibleUtc);
        if (view is "preview" or "draft")
        {
            await access.AsTeaching(info, LmsPermissions.Teach, ct);
            if (view == "draft" && !await CanSeeDraft(info, ct)) throw LmsError.Denied();
            return new(info, null, view == "draft" || published, view == "draft", false);
        }
        if (view != "student") throw LmsError.Missing();
        await access.AsStudent(LmsPermissions.Learn, ct);
        var own = await access.OwnEnrollment(id, ct);
        var current = await access.ActiveStudy(ct);
        var closed = AcademicRules.IsClosed(info.Term, data.Now);
        if (own is null && (!published || closed || !await access.Eligible(info, current, ct))) throw LmsError.Missing();
        var readable = published && own?.Status == EnrollmentStatus.Active && (closed || current?.Record.Id == own.StudyRecordId);
        return new(info, readable ? own : null, readable, false,
            readable && !closed && current?.Cohort.Active == true && access.HasStudent(LmsPermissions.Progress));
    }

    internal async Task<bool> CanSeeDraft(OfferingInfo info, CancellationToken ct) =>
        info.Offering.LeadId == access.ActorId || access.HasTeacher(LmsPermissions.Publish) &&
            (await access.CoAssignment(info.Offering.Id, ct))?.CanPublish == true;
}

internal sealed class LearningQueries(LmsData data, LmsAccess access, LearningAccess readers, LearningReadModel read, LearningValidation validation) : ILearningQueries
{
    public async Task<LearningTeacherView> Teacher(Guid offering, CancellationToken ct)
    {
        await access.Load(ct);
        var info = await access.Offering(offering, ct);
        await access.AsTeaching(info, LmsPermissions.Teach, ct);
        var draft = await readers.CanSeeDraft(info, ct);
        var writable = info.Offering.Active && !AcademicRules.IsClosed(info.Term, data.Now);
        var canEdit = writable && info.Offering.LeadId == access.ActorId && access.HasTeacher(LmsPermissions.Content);
        var canPublish = writable && draft && access.HasTeacher(LmsPermissions.Publish);
        var tree = await read.Load(offering, draft, null, ct);
        var o = info.Offering;
        var pending = draft ? await validation.Changes(tree.Chapters, ct) : [];
        var changed = draft && LearningValidation.HasChanges(o, pending);
        var keys = pending.Select(LearningReadModel.Key).ToHashSet();
        var rows = read.Rows(tree, [], draft).Select(c => c with { Pending = keys.Contains(c.Id) }).ToArray();
        var issues = canPublish && changed ? await validation.Check(info, null, pending, ct) : [];
        return new(info.Course.Title, draft ? o.DraftIntroduction ?? o.PublishedIntroduction : o.PublishedIntroduction,
            o.Revision, o.CurriculumVersion, changed, canEdit, canPublish,
            canEdit && access.HasTeacher(LmsPermissions.LessonUpload), draft, rows, issues);
    }

    public async Task<LearningCurriculum> Curriculum(Guid offering, string view, CancellationToken ct)
    {
        var ctx = await readers.Read(offering, view, ct);
        var o = ctx.Info.Offering;
        if (!ctx.Visible) return new(ctx.Info.Course.Title, null, o.CurriculumVersion, [], 0, 0);
        var tree = await read.Load(offering, ctx.Draft, data.Now, ct);
        var done = await read.Done(ctx.Enrollment, ct);
        return new(ctx.Info.Course.Title, ctx.Draft ? o.DraftIntroduction ?? o.PublishedIntroduction : o.PublishedIntroduction,
            o.CurriculumVersion, read.Rows(tree, done, false), tree.Entries.Length, tree.Entries.Count(x => done.Contains(LearningReadModel.Key(x.Lesson))));
    }

    public async Task<LearningLessonView> Lesson(Guid offering, string view, Guid lesson, CancellationToken ct)
    {
        var ctx = await readers.Read(offering, view, ct);
        if (!ctx.Visible) throw LmsError.Missing();
        var t = await read.Load(offering, ctx.Draft, data.Now, ct);
        var i = Array.FindIndex(t.Entries, e => LearningReadModel.Key(e.Lesson) == lesson);
        if (i < 0) throw LmsError.Missing();
        var e = t.Entries[i];
        var full = ctx.Draft ? await data.Find<Lesson>(e.Lesson.Id, ct) : e.Lesson;
        var items = await read.Materials([full], true, ctx.Draft, ct);
        var visibleChapters = t.Entries.Select(x => x.Chapter.Id).Distinct().ToArray();
        var edit = ctx.Draft && ctx.Info.Offering.LeadId == access.ActorId && access.HasTeacher(LmsPermissions.Content);
        return new(read.Row(e, t.Materials, await read.Done(ctx.Enrollment, ct), ctx.Draft), e.Chapter.Title,
            Array.IndexOf(visibleChapters, e.Chapter.Id), t.Entries.Take(i).Count(x => x.Chapter.Id == e.Chapter.Id),
            i == 0 ? null : LearningReadModel.Key(t.Entries[i - 1].Lesson),
            i == t.Entries.Length - 1 ? null : LearningReadModel.Key(t.Entries[i + 1].Lesson), ctx.CanComplete,
            items[e.Lesson.Id].Select(m => read.MaterialRow(m, edit)).ToArray());
    }

    public async Task<LearningReadMaterial> Material(Guid offering, string view, Guid lesson, Guid key, CancellationToken ct)
    {
        var ctx = await readers.Read(offering, view, ct);
        if (!ctx.Visible) throw LmsError.Missing();
        var query = from l in data.Read<Lesson>() join c in read.Chapters(offering, ctx.Draft) on l.ChapterId equals c.Id
                    where (l.StableLessonId ?? l.Id) == lesson && l.Active && c.Active select new { l, c };
        var item = await query.SingleOrDefaultAsync(ct) ?? throw LmsError.Missing();
        if (!ctx.Draft && !AcademicRules.CanReadLesson(data.Now, item.c, item.l)) throw LmsError.Missing();
        var m = await data.Read<LearningMaterial>().SingleOrDefaultAsync(r => r.LessonId == item.l.Id && r.Key == key && !r.Removed, ct);
        if (m is null && key == LearningReadModel.Key(item.l) && !string.IsNullOrWhiteSpace(item.l.Body))
            m = new() { Key = key, LessonId = item.l.Id, Title = item.l.Title, Kind = LearningKind.Text, Body = item.l.Body };
        if (m is null)
        {
            var legacy = await (from link in data.Read<LessonFile>() join asset in data.Read<Media>() on link.MediaId equals asset.Id
                                where link.LessonId == item.l.Id && link.Id == key select new { link.MediaId, link.Position, asset.Name, asset.Extension }).SingleOrDefaultAsync(ct);
            if (legacy is null) throw LmsError.Missing();
            var k = LearningKinds.FromExtension(legacy.Extension);
            m = new() { Key = key, LessonId = item.l.Id, Title = legacy.Name, Position = legacy.Position ?? 1,
                Kind = k, MediaId = legacy.MediaId, CoverId = k == LearningKind.Video ? item.l.CoverMediaId : null };
        }
        Guid? mediaId = m.MediaId;
        string? name = null;

        if (mediaId is Guid id && read.HasFile(id))
        {
            name = await data.Read<Media>()
                .Where(x => x.Id == id)
                .Select(x => x.Name)
                .SingleOrDefaultAsync(ct);
        }

        return new(
            read.MaterialRow(m),
            m.Body,
            name is null ? null : mediaId,
            name);
    }
}
