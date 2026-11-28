using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class StudioScheduling(LmsData data, LmsAccess access, StudioTemplates templates,
    StudioMaterializer writer, StudioFlags flags, LmsOfferings offerings) : IStudioScheduleQueries, IStudioScheduleCommands
{
    private sealed record Computed(StudioContent Content, StudioSchedulePreview View, string TermVersion, string? OriginVersion);
    private sealed record DraftPayload(int Format, StudioPlan Plan, StudioSchedulePreview Preview);

    public async Task<StudioTarget[]> Targets(Guid courseId, Guid? termId, CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        await access.AsTeacher(LmsPermissions.Content, ct);
        var user = access.ActorId;
        var entries = await offerings.Infos().Where(i => i.Offering.LeadId == user && i.Course.Id == courseId &&
                (termId == null || i.Term.Id == termId))
            .OrderByDescending(i => i.Term.StartsUtc).ThenBy(i => i.Offering.GroupCode).Take(100).ToArrayAsync(ct);
        if (entries.Length == 0) return [];
        var ids = entries.Select(i => i.Offering.Id).ToArray();
        var cohortFilter = data.Read<OfferingCohort>();
        var cohorts = (await cohortFilter.Where(c => ids.Contains(c.OfferingId)).ToArrayAsync(ct)).ToLookup(x => x.OfferingId, x => x.CohortId);
        var rows = data.Read<Offering>().Where(o => ids.Contains(o.Id));
        var academicCohorts = access.Cohorts();
        var eligible = await (from offer in rows
                              join course in data.Read<CatalogCourse>() on offer.CourseId equals course.Id
                              from cohort in academicCohorts
                              join study in data.Read<StudyRecord>() on cohort.Id equals study.CohortId
                              join student in data.Read<StudentProfile>() on study.StudentId equals student.Id
                              join account in data.Read<ApplicationUser>() on student.Id equals account.Id
                              where offer.Active && course.Active && cohort.Active && student.Active && account.IsActive && study.EndedUtc == null &&
                                    (offer.Audience == OfferingAudience.General || offer.Audience == OfferingAudience.Common && course.FacultyId == cohort.FacultyId ||
                                     offer.Audience == OfferingAudience.Specialized && offer.OwnerMajorId == cohort.MajorId) &&
                                    (!cohortFilter.Any(x => x.OfferingId == offer.Id) || cohortFilter.Any(x => x.OfferingId == offer.Id && x.CohortId == cohort.Id))
                              group study by offer.Id into g
                              select new { Id = g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Total, ct);
        var enrollmentRows = data.Read<Enrollment>();
        var enrolled = await enrollmentRows.Where(e => ids.Contains(e.OfferingId) && e.Status == EnrollmentStatus.Active)
            .GroupBy(e => e.OfferingId).Select(g => new { Id = g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Total, ct);
        var occupied = (await data.Read<Chapter>().Where(c => ids.Contains(c.OfferingId)).Select(c => c.OfferingId)
            .Union(enrollmentRows.Where(e => ids.Contains(e.OfferingId)).Select(e => e.OfferingId)).ToArrayAsync(ct)).ToHashSet();
        var drafts = await data.Read<OfferingScheduleDraft>().Where(d => ids.Contains(d.OfferingId) && d.UpdatedBy == user)
            .ToDictionaryAsync(d => d.OfferingId, d => d.State, ct);
        var canPublish = access.HasTeacher(LmsPermissions.Publish);
        var now = data.Now;
        return entries.Select(x =>
        {
            var o = x.Offering; var term = x.Term;
            var canPrepare = flags.ScheduleEnabled && o.Active && x.Course.Active && o.PublishedUtc == null &&
                             !AcademicRules.IsClosed(term, now) && !occupied.Contains(o.Id);
            return new StudioTarget(o.Id, term.Id, term.Year, term.Season, x.Course.Title, o.GroupCode, o.Audience,
                cohorts[o.Id].ToArray(), eligible.GetValueOrDefault(o.Id), enrolled.GetValueOrDefault(o.Id), o.Revision,
                drafts.TryGetValue(o.Id, out var state) ? state : null, canPrepare, canPrepare && canPublish && now < term.EnrollClosesUtc,
                o.VisibleUtc, o.PublishedUtc, DateOnly.FromDateTime(JalaliTime.InTehran(term.StartsUtc)));
        }).ToArray();
    }

    public async Task<StudioSource[]> Sources(Guid offeringId, Guid versionId, CancellationToken ct)
    {
        flags.RequireScheduler();
        var target = await Target(offeringId, ct);
        await RequireEmpty(target, ct);
        var (template, _) = await templates.Version(versionId, target.Course.Id, ct);
        var rows = await Origins(target, template.Id).Take(100).ToArrayAsync(ct);
        return rows.Select(o => o.Source).ToArray();
    }

    public async Task<StudioSchedulePreview> Preview(Guid offeringId, StudioPlan input, CancellationToken ct)
    {
        flags.RequireScheduler();
        var target = await Target(offeringId, ct);
        await RequireEmpty(target, ct);
        return (await Calculate(target, input, ct)).View;
    }

    public async Task<StudioDraftResult> Draft(Guid offeringId, CancellationToken ct)
    {
        flags.RequireScheduler();
        _ = await Target(offeringId, ct);
        var row = await data.Read<OfferingScheduleDraft>().SingleOrDefaultAsync(d => d.OfferingId == offeringId, ct);
        return new(row is null ? null : View(row, row.UpdatedBy == access.ActorId && row.State != ScheduleDraftState.Discarded ? Deserialize(row) : null));
    }

    public async Task<StudioReadSchedule> Read(Guid offeringId, CancellationToken ct)
    {
        var info = await access.Offering(offeringId, ct);
        await access.AsTeaching(info, LmsPermissions.Teach, ct); // shared read-only history for authorized co-teachers
        return await Actual(info, false, ct);
    }

    public async Task<StudioDraftView> Save(Guid offeringId, StudioStoreDraft input, CancellationToken ct)
    {
        flags.RequireScheduler();
        await using var tx = await data.Transaction(ct);
        var target = await Target(offeringId, ct);
        await RequireEmpty(target, ct);
        StudioTemplates.RequireRevision(target.Offering.Revision, input.ExpectedOfferingRevision);
        var row = await data.Db.Set<OfferingScheduleDraft>().FindAsync([offeringId], ct);
        if (row?.Revision != input.ExpectedDraftRevision || row?.State == ScheduleDraftState.Applied)
            throw LmsError.Conflict("وضعیت پیش‌نویس عوض شده است؛ پیش از ادامه دادهٔ تازه بگیرید.");
        var computed = await Calculate(target, input.Plan, ct);
        if (!string.Equals(input.PreviewStamp, computed.View.Stamp, StringComparison.Ordinal))
            throw LmsError.Conflict("ترم، مبدأ یا اصلاح‌های جدول عوض شده‌اند؛ پیش‌نمایش را دوباره بررسی کنید.");
        var payload = new DraftPayload(1, Normalize(input.Plan), computed.View);
        if (row is null)
        {
            row = new() { OfferingId = offeringId };
            data.Db.Add(row);
        }
        row.State = ScheduleDraftState.Draft;
        row.TemplateVersionId = input.Plan.TemplateVersionId;
        row.OriginOfferingId = input.Plan.OriginOfferingId;
        row.ClassesStartDate = input.Plan.ClassesStartDate;
        row.Mode = input.Plan.Mode;
        row.OfferingRevision = target.Offering.Revision;
        row.TermVersion = computed.TermVersion;
        row.OriginVersion = computed.OriginVersion;
        row.Revision = Guid.NewGuid();
        row.UpdatedBy = access.ActorId;
        row.UpdatedUtc = data.Now;
        row.Payload = JsonSerializer.Serialize(payload);
        return await data.Finish(tx, View(row, payload), offeringId, ct);
    }

    public async Task<bool> Discard(Guid offeringId, StudioRevision input, CancellationToken ct)
    {
        flags.RequireScheduler();
        await using var tx = await data.Transaction(ct);
        _ = await Target(offeringId, ct);
        var row = await data.Find<OfferingScheduleDraft>(offeringId, ct);
        StudioTemplates.RequireRevision(row.Revision, input.ExpectedRevision);
        if (row.State == ScheduleDraftState.Applied) throw LmsError.Conflict("برنامهٔ اعمال‌شده از این مسیر حذف نمی‌شود.");
        row.State = ScheduleDraftState.Discarded;
        row.Revision = Guid.NewGuid();
        row.OriginOfferingId = null;
        row.OriginVersion = null;
        row.Payload = "";
        row.UpdatedBy = access.ActorId;
        row.UpdatedUtc = data.Now;
        return await data.Finish(tx, true, offeringId, ct);
    }

    public async Task<StudioReceipt> Apply(Guid offeringId, StudioApplyDraft input, bool publish, CancellationToken ct)
    {
        flags.RequireScheduler();
        await using var tx = await data.Transaction(ct);
        var target = await Target(offeringId, ct);
        if (publish) await access.AsTeaching(target, LmsPermissions.Publish, ct);
        var receipt = await data.Find<OfferingScheduleDraft>(offeringId, ct);
        if (receipt.UpdatedBy != access.ActorId) throw LmsError.Missing();
        StudioTemplates.RequireRevision(receipt.Revision, input.ExpectedDraftRevision);
        StudioTemplates.RequireRevision(receipt.OfferingRevision, input.ExpectedOfferingRevision);
        if (receipt.State == ScheduleDraftState.Applied)
        {
            if (receipt.PublishedUtc.HasValue != publish)
                throw LmsError.Conflict("این برنامه قبلاً با عمل دیگری اعمال شده است؛ از نمای ارائه ادامه دهید.");
            return Receipt(receipt);
        }
        if (receipt.State != ScheduleDraftState.Draft) throw LmsError.Conflict("پیش‌نویس قابل اعمال نیست.");
        await RequireEmpty(target, ct);
        StudioTemplates.RequireRevision(target.Offering.Revision, receipt.OfferingRevision);
        var saved = Deserialize(receipt);
        var computed = await Calculate(target, saved.Plan, ct);
        if (computed.TermVersion != receipt.TermVersion || computed.OriginVersion != receipt.OriginVersion || computed.View.Stamp != saved.Preview.Stamp)
            throw LmsError.Conflict("زمان‌بندی مبدأ یا تقویم رسمی تغییر کرده است؛ دوباره پیش‌نمایش و ذخیره کنید.");
        if (computed.View.Issues.Length > 0) throw LmsError.Conflict("پیش از اعمال برنامه همهٔ خطاهای تقویم را اصلاح کنید.");
        var now = data.Now;
        if (publish && now >= target.Term.EnrollClosesUtc)
            throw LmsError.Conflict("بعد از مهلت اخذ، انتشار اولیه ممکن نیست.");
        if (publish) await offerings.ValidateInstructor(target.Offering.LeadId, target.Course.FacultyId, target.Offering.OwnerMajorId, true, ct);
        var (header, _) = await templates.Version(receipt.TemplateVersionId, target.Course.Id, ct);
        await templates.VerifyMedia(header, computed.Content, ct);
        var offering = await data.Find<Offering>(offeringId, ct);
        var times = computed.View.Rows.ToDictionary(x => x.Item.Key, x => x.Times);
        DateTimeOffset? publishedUtc = publish ? now : null;
        writer.Add(offeringId, computed.Content, times, publishedUtc);
        offering.VisibleUtc = times[Guid.Empty].Start;
        offering.PublishedUtc = publishedUtc;
        offering.Revision = Guid.NewGuid();
        receipt.AppliedOfferingRevision = offering.Revision;
        receipt.AppliedUtc = now;
        receipt.PublishedUtc = publishedUtc;
        receipt.State = ScheduleDraftState.Applied;
        receipt.UpdatedUtc = now;
        return await data.Finish(tx, Receipt(receipt), offeringId, ct);
    }

    private async Task<OfferingInfo> Target(Guid id, CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        var info = await access.Offering(id, ct);
        if (info.Offering.LeadId != access.ActorId) throw LmsError.Missing();
        await access.AsTeaching(info, LmsPermissions.Content, ct);
        return info;
    }

    private async Task RequireEmpty(OfferingInfo info, CancellationToken ct)
    {
        var id = info.Offering.Id;
        LmsData.Open(info.Term, data.Now);
        if (!info.Course.Active || !await data.Read<Faculty>().AnyAsync(f => f.Id == info.Course.FacultyId && f.Active, ct) ||
            !info.Offering.Active || info.Offering.PublishedUtc is not null ||
            await data.Read<Chapter>().AnyAsync(c => c.OfferingId == id, ct) ||
            await data.Read<Enrollment>().AnyAsync(e => e.OfferingId == id, ct))
            throw LmsError.Conflict("کارگاه فقط روی ارائهٔ واگذارشده، فعال، خالی و منتشرنشده قابل اعمال است.");
    }

    private sealed record OriginRow(StudioSource Source, Guid OfferingRevision);
    private IQueryable<OriginRow> Origins(OfferingInfo dest, Guid templateId, Guid? sourceId = null)
    {
        var actor = access.ActorId; var courseId = dest.Course.Id; var before = dest.Term.StartsUtc;
        var publishedChapters = data.Read<Chapter>().Where(c => c.Active && !c.Retired && c.PendingBaseId == null && c.TemplateItemKey != null && c.PublishedUtc != null);
        return from d in data.Read<OfferingScheduleDraft>()
               join v in data.Read<TemplateVersion>() on d.TemplateVersionId equals v.Id
               join o in data.Read<Offering>() on d.OfferingId equals o.Id
               join t in data.Read<Term>() on o.TermId equals t.Id
               where v.TemplateId == templateId && (sourceId == null || o.Id == sourceId) &&
                     o.LeadId == actor && d.UpdatedBy == actor && o.CourseId == courseId &&
                     t.StartsUtc < before && d.State == ScheduleDraftState.Applied && o.PublishedUtc != null &&
                     publishedChapters.Any(c => c.OfferingId == o.Id)
               orderby t.StartsUtc descending, o.GroupCode, o.Id
               select new OriginRow(new StudioSource(o.Id, t.Year, t.Season, o.GroupCode, d.ClassesStartDate, v.Id), o.Revision);
    }

    private async Task<Computed> Calculate(OfferingInfo target, StudioPlan input, CancellationToken ct)
    {
        if (input is null || input.Overrides is null || input.Overrides.Any(x => x is null) || input.Overrides.Length > AcademicSchedule.MaxItems * 3 + 1 ||
            !Enum.IsDefined(input.Mode) || (input.Mode == ScheduleMode.Shift) != input.OriginOfferingId.HasValue ||
            input.ClassesStartDate.Year is < 1900 or > 2250)
            throw LmsError.Invalid("روش برنامه، مبدأ، روز شروع یا اصلاح‌های دستی معتبر نیست.");
        var (template, version) = await templates.Version(input.TemplateVersionId, target.Course.Id, ct);
        var (_, content, _, _) = StudioDocuments.Read(version);
        if (content.Chapters.Length == 0 || content.Chapters.Any(ch => ch.Lessons.Length == 0 || ch.Lessons.Any(l => string.IsNullOrWhiteSpace(l.Body) && l.MediaIds.Length == 0 && l.Materials is not { Length: > 0 })))
            throw LmsError.Conflict("ابتدا نسخه‌ای با درسکِ دارای متن یا فایل در هر فصل ذخیره کنید.");
        // Old and extracted versions can have missing schedules. They remain usable once a teacher provides dates.
        var items = StudioDocuments.ToSchedule(content);
        string? originVersion = null;
        ScheduleOrigin? origin = null;
        if (input.OriginOfferingId is { } sourceId)
        {
            var row = await Origins(target, template.Id, sourceId).SingleOrDefaultAsync(ct)
                      ?? throw LmsError.Missing();
            var actual = await Actual(await access.Offering(sourceId, ct), true, ct);
            origin = new(row.Source.ClassesStartDate, actual.Rows.ToDictionary(x => x.Key, x => new ScheduleOriginRow(x.Kind, x.Times)));
            originVersion = Stamp(new { row.OfferingRevision, row.Source.ClassesStartDate, Rows = actual.Rows.OrderBy(x => x.Key).Select(x => new { x.Key, x.Kind, x.Times }) });
            if (origin.Rows.Count == 0) throw LmsError.Conflict("مبدأ اقلام قابل انتقال ندارد.");
        }
        ScheduleCalculation calc;
        try { calc = AcademicSchedule.Calculate(target.Term, input.ClassesStartDate, items, input.Overrides, origin, target.Offering.VisibleUtc); }
        catch (ArgumentException ex) { throw LmsError.Invalid(ex.Message); }
        var term = target.Term;
        var termVersion = Stamp(new { term.Id, term.Active, term.StartsUtc, term.EndsUtc, term.EnrollOpensUtc, term.EnrollClosesUtc,
            term.DropOpensUtc, term.DropClosesUtc, term.ClosedUtc });
        var stamp = Stamp(new { target.Offering.Id, target.Offering.Revision, Term = termVersion, Origin = originVersion, WorkspaceFormat = 3, Plan = Normalize(input) });
        return new(content, new(target.Offering.Revision, stamp, calc.Rows, calc.Issues), termVersion, originVersion);
    }

    private async Task<StudioReadSchedule> Actual(OfferingInfo info, bool mappedOnly, CancellationToken ct)
    {
        var id = info.Offering.Id;
        var start = await data.Read<OfferingScheduleDraft>().Where(x => x.OfferingId == id && x.State == ScheduleDraftState.Applied)
            .Select(x => (DateOnly?)x.ClassesStartDate).SingleOrDefaultAsync(ct);
        var chapters = await data.Read<Chapter>().Where(c => c.OfferingId == id && c.Active && !c.Retired && c.PendingBaseId == null && (!mappedOnly || c.PublishedUtc != null)).OrderBy(c => c.Position).ToArrayAsync(ct);
        var ids = chapters.Select(x => x.Id).ToArray();
        var lessons = (await data.Read<Lesson>().Where(l => ids.Contains(l.ChapterId) && l.Active && (!mappedOnly || !l.NeedsAvailabilityReview))
            .OrderBy(l => l.Position).ToArrayAsync(ct)).ToLookup(l => l.ChapterId);
        var rows = new List<StudioActualRow>();
        foreach (var c in chapters)
        {
            Add(c.TemplateItemKey, c.Id, ScheduleKind.Chapter, c.Title, new(c.VisibleFromUtc, c.VisibleUntilUtc, null));
            foreach (var l in lessons[c.Id])
                Add(l.TemplateItemKey, l.Id, ScheduleKind.Lesson, l.Title, new(l.VisibleFromUtc, l.VisibleUntilUtc, null));
        }
        return new(id, info.Course.Title, start, info.Offering.VisibleUtc, info.Offering.PublishedUtc, rows.ToArray());

        void Add(Guid? stable, Guid actual, ScheduleKind kind, string title, ScheduleTimes times)
        {
            if (stable.HasValue || !mappedOnly) rows.Add(new(stable ?? actual, kind, title, times));
        }
    }

    private static StudioPlan Normalize(StudioPlan plan) => plan with { Overrides = plan.Overrides.OrderBy(x => x.Key).ThenBy(x => x.Field)
        .Select(x => x with { AtUtc = x.AtUtc?.ToUniversalTime() }).ToArray() };
    private static string Stamp<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static StudioDraftView View(OfferingScheduleDraft row, DraftPayload? payload) =>
        new(row.Revision, row.State, payload?.Plan, payload?.Preview, row.UpdatedUtc, row.AppliedUtc, row.PublishedUtc);
    private static StudioReceipt Receipt(OfferingScheduleDraft row) =>
        new(row.OfferingId, row.Revision, row.AppliedOfferingRevision!.Value, row.AppliedUtc!.Value, row.PublishedUtc);
    private static DraftPayload Deserialize(OfferingScheduleDraft row)
    {
        try
        {
            var value = JsonSerializer.Deserialize<DraftPayload>(row.Payload);
            if (value is { Format: 1 } && value.Plan.TemplateVersionId == row.TemplateVersionId && value.Plan.ClassesStartDate == row.ClassesStartDate &&
                value.Plan.OriginOfferingId == row.OriginOfferingId && value.Plan.Mode == row.Mode) return value;
        }
        catch (JsonException) { }
        throw LmsError.Conflict("پیش‌نویس ذخیره‌شده قابل خواندن نیست.");
    }
}
