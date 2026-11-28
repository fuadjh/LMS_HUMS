using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed record LessonInfo(Lesson Lesson, Chapter Chapter, OfferingInfo Info);

internal sealed class LmsEnrollments(LmsData data, LmsAccess access, LmsOfferings offerings) : ILmsEnrollment
{
    public async Task<EnrollmentChange> JoinAsync(Guid offeringId, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        await access.AsStudent(LmsPermissions.Enroll, ct);
        var o = await access.Offering(offeringId, ct);
        var study = await access.ActiveStudy(ct) ?? throw LmsError.Conflict("پروندهٔ فعال لازم است.");
        var targets = await offerings.TargetIds(offeringId, ct);
        var existing = await data.Read<Enrollment>().SingleOrDefaultAsync(x => x.OfferingId == offeringId && x.StudyRecordId == study.Record.Id, ct);
        if (existing is { Status: EnrollmentStatus.Active }) return new(existing.Id, existing.Status, false, existing.ChangedUtc);
        var now = data.Now;
        if (!await access.Eligible(o, study, ct) || !AcademicRules.CanSee(now, o.Offering.PublishedUtc, o.Offering.VisibleUtc))
            throw LmsError.Missing();
        if (!offerings.CanJoin(o, study, targets, existing, now))
            throw LmsError.Conflict("شرط مخاطب، مهلت یا وضعیت ثبت‌نام برقرار نیست؛ ثبت مجددِ خروج مجاز نیست.");
        var e = new Enrollment { OfferingId = offeringId, StudyRecordId = study.Record.Id, Status = EnrollmentStatus.Active, CreatedUtc = now, ChangedUtc = now };
        data.Db.Add(e);
        AddEvent(e, now, null);
        return await data.Finish(tx, new EnrollmentChange(e.Id, e.Status, true, e.ChangedUtc), e.Id, ct);
    }
    public Task<EnrollmentChange> WithdrawAsync(Guid enrollmentId, CancellationToken ct) => Exit(enrollmentId, false, null, ct);
    public Task<EnrollmentChange> RemoveAsync(Guid enrollmentId, RemoveEnrollmentInput input, CancellationToken ct) => Exit(enrollmentId, true, input.Reason, ct);

    private async Task<EnrollmentChange> Exit(Guid id, bool admin, string? reason, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        if (admin) await access.AsEducation(LmsPermissions.AdminRemove, ct);
        else await access.AsStudent(LmsPermissions.Withdraw, ct);
        var e = await data.Find<Enrollment>(id, ct);
        var study = await data.Read<StudyRecord>().SingleAsync(x => x.Id == e.StudyRecordId, ct);
        var o = await access.Offering(e.OfferingId, ct);
        if (admin)
        {
            var c = await access.Cohorts().SingleAsync(x => x.Id == study.CohortId, ct);
            if (!access.CanAdminOffering(LmsPermissions.AdminRemove, o)) throw LmsError.Missing();
            access.DemandScope(LmsPermissions.AdminRemove, c.FacultyId, c.MajorId);
        }
        else if (study.StudentId != access.ActorId) throw LmsError.Missing();
        var now = data.Now;
        switch (AcademicRules.CheckExit(e.Status, admin, o.Term, now, reason))
        {
            case EnrollmentExitCheck.AlreadyApplied: return new(e.Id, e.Status, false, e.ChangedUtc);
            case EnrollmentExitCheck.InvalidReason: throw LmsError.Invalid("دلیل مدیریتی ۱ تا ۵۰۰ کاراکتر الزامی است.");
            case EnrollmentExitCheck.NotActive: throw LmsError.Conflict("ثبت‌نام پیش‌تر از مسیر دیگری بسته شده است.");
            case EnrollmentExitCheck.TermClosed: throw LmsError.Conflict("نیم‌سال بسته و فقط خواندنی است.");
            case EnrollmentExitCheck.OutsideWindow: throw LmsError.Conflict("خروج دانشجو فقط در بازهٔ رسمی حذف‌واضافه ممکن است.");
        }
        if (!admin && (study.EndedUtc is not null || !o.Offering.Active)) throw LmsError.Conflict("پرونده و ارائه باید فعال باشند.");
        // Serializable locks the term/calendar read; the token protects simultaneous writers.
        // Only status and history change: the enrollment ID and all dependent rows stay in place.
        e.Status = admin ? EnrollmentStatus.RemovedByAdmin : EnrollmentStatus.WithdrawnByStudent;
        e.Revision = Guid.NewGuid(); e.ChangedUtc = now;
        AddEvent(e, now, admin ? LmsError.Text(reason, 500) : null);
        return await data.Finish(tx, new EnrollmentChange(e.Id, e.Status, true, now), id, ct);
    }
    private void AddEvent(Enrollment e, DateTimeOffset now, string? reason)
        => data.Db.Add(new EnrollmentEvent { EnrollmentId = e.Id, ActorId = access.ActorId, OccurredUtc = now, Status = e.Status, Reason = reason });

    public async Task<RosterRow[]> RosterAsync(Guid offeringId, string view, CancellationToken ct)
    {
        var info = await access.Offering(offeringId, ct);
        switch (view)
        {
            case "teacher": await access.AsTeaching(info, LmsPermissions.Teach, ct); break;
            case "admin":
                await access.AsEducation(LmsPermissions.Assign, ct);
                if (!access.CanAdminOffering(LmsPermissions.Assign, info)) throw LmsError.Missing(); break;
            default: throw LmsError.Missing();
        }
        var rows = await (from e in data.Read<Enrollment>() join study in data.Read<StudyRecord>() on e.StudyRecordId equals study.Id
                          join c in access.Cohorts() on study.CohortId equals c.Id
                          join u in data.Read<ApplicationUser>() on study.StudentId equals u.Id
                          where e.OfferingId == offeringId orderby u.DisplayName
                          select new { e, c, u.DisplayName, study.Number }).ToArrayAsync(ct);
        return rows.Where(x => view == "teacher" || access.AdminIn(LmsPermissions.Assign, x.c.FacultyId, x.c.MajorId)).Take(1000).Select(x =>
            new RosterRow(x.e.Id, x.DisplayName, x.c.Label, x.Number, x.e.Status, view == "admin" &&
                x.e.Status == EnrollmentStatus.Active && !AcademicRules.IsClosed(info.Term, data.Now) &&
                access.CanAdminOffering(LmsPermissions.AdminRemove, info) && access.AdminIn(LmsPermissions.AdminRemove, x.c.FacultyId, x.c.MajorId))).ToArray();
    }

    internal async Task<LessonInfo> LessonInfo(Guid id, CancellationToken ct)
    {
        var l = await data.Read<Lesson>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw LmsError.Missing();
        var c = await data.Read<Chapter>().SingleAsync(x => x.Id == l.ChapterId, ct);
        var i = await access.Offering(c.OfferingId, ct);
        return new(l, c, i);
    }
    internal static bool PublishedLesson(LessonInfo info, DateTimeOffset now) => info.Info.Offering.Active && info.Lesson.Active && info.Chapter.Active &&
        AcademicRules.CanSee(now, info.Info.Offering.PublishedUtc, info.Info.Offering.VisibleUtc) && AcademicRules.CanReadLesson(now, info.Chapter, info.Lesson);
    public async Task<bool> ProgressAsync(Guid lessonId, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        await access.AsStudent(LmsPermissions.Progress, ct);
        var lesson = await LessonInfo(lessonId, ct);
        var now = data.Now;
        LmsData.Open(lesson.Info.Term, now);
        if (!PublishedLesson(lesson, now)) throw LmsError.Missing();
        var mine = await access.ActiveEnrollment(lesson.Chapter.OfferingId, ct);
        await SetCompletion(data, mine.Id, lesson.Lesson.StableLessonId ?? lessonId, true, ct);
        return await data.Finish(tx, true, lessonId, ct);
    }

    internal static async Task SetCompletion(LmsData data, Guid enrollment, Guid stableLesson, bool completed, CancellationToken ct)
    {
        var row = await data.Db.Set<LessonProgress>().SingleOrDefaultAsync(p => p.EnrollmentId == enrollment && p.LessonId == stableLesson, ct);
        if (row is null)
        {
            if (completed) data.Db.Add(new LessonProgress { EnrollmentId = enrollment, LessonId = stableLesson, CompletedUtc = data.Now });
            return;
        }
        if (completed) row.ClearedUtc = null;
        else row.ClearedUtc ??= data.Now; // keep the existing completion record and original timestamp
    }

    public async Task<EnrollmentHistoryRow[]> HistoryAsync(Guid enrollmentId, string view, CancellationToken ct)
    {
        var admin = view == "admin";
        if (admin) await access.AsEducation(LmsPermissions.Assign, ct);
        else if (view == "student") await access.AsStudent(LmsPermissions.Learn, ct);
        else throw LmsError.Missing();
        var e = await data.Read<Enrollment>().SingleOrDefaultAsync(x => x.Id == enrollmentId, ct) ?? throw LmsError.Missing();
        var s = await data.Read<StudyRecord>().SingleAsync(x => x.Id == e.StudyRecordId, ct);
        var showReason = false;
        if (admin)
        {
            var i = await access.Offering(e.OfferingId, ct);
            var c = await access.Cohorts().SingleAsync(x => x.Id == s.CohortId, ct);
            if (!access.CanAdminOffering(LmsPermissions.Assign, i)) throw LmsError.Missing();
            access.DemandScope(LmsPermissions.Assign, c.FacultyId, c.MajorId);
            showReason = access.CanAdminOffering(LmsPermissions.AdminRemove, i) && access.AdminIn(LmsPermissions.AdminRemove, c.FacultyId, c.MajorId);
        }
        else if (s.StudentId != access.ActorId) throw LmsError.Missing();
        data.Resource(enrollmentId);
        return await data.Read<EnrollmentEvent>().Where(x => x.EnrollmentId == enrollmentId).OrderBy(x => x.OccurredUtc).ThenBy(x => x.Id)
            .Select(x => new EnrollmentHistoryRow(x.Status, x.OccurredUtc, showReason ? x.Reason : null)).ToArrayAsync(ct);
    }

    public async Task<TranscriptRow[]> TranscriptAsync(CancellationToken ct)
    {
        await access.AsStudent(LmsPermissions.Learn, ct);
        var user = access.ActorId;
        var rows = await (from e in data.Read<Enrollment>() join study in data.Read<StudyRecord>() on e.StudyRecordId equals study.Id
                          join o in offerings.Infos() on e.OfferingId equals o.Offering.Id where study.StudentId == user orderby o.Term.StartsUtc descending, o.Course.Title select new { e, o }).ToArrayAsync(ct);
        var ids = rows.Select(x => x.e.Id).ToArray();
        var grades = (await data.Read<FinalGrade>().Where(x => ids.Contains(x.EnrollmentId) && x.Published).OrderByDescending(x => x.Revision).ToArrayAsync(ct))
            .GroupBy(x => x.EnrollmentId).ToDictionary(x => x.Key, x => x.First());
        return rows.Select(x => new TranscriptRow(x.e.Id, x.e.OfferingId, x.o.Course.Title, x.o.Offering.GroupCode,
            $"{LmsError.Season(x.o.Term.Season)} {x.o.Term.Year}", x.e.Status, x.e.ChangedUtc,
            grades.TryGetValue(x.e.Id, out var g) ? new(g.Revision, g.Score, g.Feedback, g.Published, g.CreatedUtc) : null)).ToArray();
    }
}
