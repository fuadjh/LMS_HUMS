using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class LmsCalendar(LmsData data, LmsAccess access) : ILmsCalendar
{
    public async Task<CalendarEntry[]> CalendarAsync(string view, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        if (toUtc <= fromUtc || toUtc - fromUtc > TimeSpan.FromDays(100)) throw LmsError.Invalid("بازهٔ تقویم حداکثر ۱۰۰ روز است.");
        var id = access.ActorId; Guid[] offers;
        var now = data.Now;
        if (view == "teacher")
        {
            await access.AsTeacher(LmsPermissions.Calendar, ct);
            var co = data.Read<CoTeacher>().Where(x => x.InstructorId == id && x.Active).Select(x => x.OfferingId);
            offers = await data.Read<Offering>().Where(x => x.Active && (x.LeadId == id || co.Contains(x.Id))).Select(x => x.Id).ToArrayAsync(ct);
        }
        else if (view == "student")
        {
            await access.AsStudent(LmsPermissions.Calendar, ct);
            offers = await (from e in data.Read<Enrollment>() join study in data.Read<StudyRecord>() on e.StudyRecordId equals study.Id
                            join o in data.Read<Offering>() on e.OfferingId equals o.Id
                            join t in data.Read<Term>() on o.TermId equals t.Id
                            where study.StudentId == id && e.Status == EnrollmentStatus.Active && o.Active && o.PublishedUtc != null && o.VisibleUtc <= now &&
                                  (study.EndedUtc == null || t.ClosedUtc != null || t.EndsUtc <= now)
                            select o.Id).Distinct().ToArrayAsync(ct);
        }
        else throw LmsError.Missing();
        return await Events(offers, fromUtc, toUtc, view == "student", ct);
    }
    internal async Task<CalendarEntry[]> Events(
       Guid[] offers,
       DateTimeOffset from,
       DateTimeOffset to,
       bool published,
       CancellationToken ct)
    {
        if (offers.Length == 0)
            return [];

        var fromUtc = from;
        var toUtc = to;

        var chapters = data.Read<Chapter>()
            .Where(c => c.Active && (!published || c.PublishedUtc != null));

        var sessions = await (
            from s in data.Read<LessonSession>()
            join o in data.Read<Offering>() on s.OfferingId equals o.Id
            join course in data.Read<CatalogCourse>() on o.CourseId equals course.Id
            where offers.Contains(o.Id)
                && s.Active
                && s.OpensUtc >= fromUtc
                && s.OpensUtc < toUtc
                && (!published ||
                    (from l in data.Read<Lesson>()
                     join c in chapters on l.ChapterId equals c.Id
                     where l.Active
                         && l.SessionId == s.Id
                         && c.OfferingId == o.Id
                     select l.Id).Any())
            select new CalendarEntry(
                o.Id, s.Id, "session", s.Title + " · " + course.Title, s.OpensUtc)
        ).ToArrayAsync(ct);

        var assessments =
            from a in data.Read<Assessment>()
            join c in chapters on a.ChapterId equals c.Id
            join o in data.Read<Offering>() on c.OfferingId equals o.Id
            join course in data.Read<CatalogCourse>() on o.CourseId equals course.Id
            where offers.Contains(o.Id) && a.Active
            select new
            {
                OfferingId = o.Id,
                AssessmentId = a.Id,
                Title = a.Title + " · " + course.Title,
                a.DueUtc,
                a.CutoffUtc
            };

        var due = await assessments
            .Where(x => x.DueUtc >= fromUtc && x.DueUtc < toUtc)
            .Select(x => new CalendarEntry(
                x.OfferingId, x.AssessmentId, "due", x.Title, x.DueUtc!.Value))
            .ToArrayAsync(ct);

        var late = await assessments
            .Where(x => x.CutoffUtc > x.DueUtc
                && x.CutoffUtc >= fromUtc
                && x.CutoffUtc < toUtc)
            .Select(x => new CalendarEntry(
                x.OfferingId, x.AssessmentId, "cutoff", x.Title, x.CutoffUtc!.Value))
            .ToArrayAsync(ct);

        var termEntries = await (
            from o in data.Read<Offering>()
            join course in data.Read<CatalogCourse>() on o.CourseId equals course.Id
            join term in data.Read<Term>() on o.TermId equals term.Id
            where offers.Contains(o.Id)
                && term.DropClosesUtc >= fromUtc
                && term.DropClosesUtc < toUtc
            select new CalendarEntry(
                o.Id, o.Id, "add-drop", course.Title, term.DropClosesUtc)
        ).ToArrayAsync(ct);

        return sessions
            .Concat(due)
            .Concat(late)
            .Concat(termEntries)
            .OrderBy(x => x.AtUtc)
            .ThenBy(x => x.Title)
            .ToArray();
    }
    public async Task<NoticeRow[]> NoticesAsync(CancellationToken ct)
    {
        await HaveCalendar(LmsPermissions.Calendar, ct);
        var id = access.ActorId;
        return await data.Read<Reminder>().Where(x => x.UserId == id && x.SmsState != "Superseded").OrderByDescending(x => x.CreatedUtc)
            .Take(100).Select(x => new NoticeRow(x.Id, x.Title, x.Kind, x.DueUtc, x.CreatedUtc, x.ReadUtc != null, x.SmsState)).ToArrayAsync(ct);
    }
    public async Task<bool> ReadAsync(Guid noticeId, CancellationToken ct)
    {
        await HaveCalendar(LmsPermissions.Acknowledge, ct);
        var n = await data.Db.Set<Reminder>().SingleOrDefaultAsync(x => x.Id == noticeId && x.UserId == access.ActorId, ct) ?? throw LmsError.Missing();
        n.ReadUtc ??= data.Now;
        await data.Save(ct); data.Resource(noticeId); return true;
    }
    private async Task HaveCalendar(string key, CancellationToken ct)
    {
        await access.Load(ct);
        if (!access.HasStudent(key) && !access.HasTeacher(key)) throw LmsError.Denied();
    }
    private async Task<CohortInfo> ChatCohort(string key, CancellationToken ct)
    {
        await access.AsStudent(key, ct);
        var current = await access.ActiveStudy(ct);
        if (current?.Cohort.Active != true) throw LmsError.Denied();
        return current.Cohort;
    }
    private IQueryable<ApplicationUser> Peers(Guid cohortId) =>
        from r in data.Read<StudyRecord>() join p in data.Read<StudentProfile>() on r.StudentId equals p.Id
        join u in data.Read<ApplicationUser>() on p.Id equals u.Id
        where r.CohortId == cohortId && r.EndedUtc == null && p.Active && u.IsActive
        orderby u.DisplayName, u.Id select u;
    public async Task<ChatRoom> ChatAsync(Guid? recipientId, DateTimeOffset? beforeUtc, CancellationToken ct)
    {
        var c = await ChatCohort(LmsPermissions.Chat, ct);
        var me = access.ActorId;
        if (recipientId is { } peer && (peer == me || !await Peers(c.Id).AnyAsync(x => x.Id == peer, ct))) throw LmsError.Missing();
        var rows = await (from m in data.Read<CohortMessage>() join u in data.Read<ApplicationUser>() on m.SenderId equals u.Id
                          where m.CohortId == c.Id && (beforeUtc == null || m.SentUtc < beforeUtc) &&
                            (recipientId == null ? m.RecipientId == null :
                            m.SenderId == me && m.RecipientId == recipientId || m.SenderId == recipientId && m.RecipientId == me)
                          orderby m.SentUtc descending, m.Id descending
                          select new ChatMessageRow(m.Id, m.SenderId, u.DisplayName, m.RecipientId, m.SenderId == me, m.Body, m.SentUtc)).Take(100).ToArrayAsync(ct);
        var peers = await Peers(c.Id).Where(x => x.Id != me).Take(500).Select(x => new ChatPeer(x.Id, x.DisplayName)).ToArrayAsync(ct);
        return new(c.Label, me, peers, rows.Reverse().ToArray());
    }
    public async Task<Guid> SendAsync(ChatPost input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var c = await ChatCohort(LmsPermissions.Send, ct);
        var me = access.ActorId;
        if (input.RecipientId is { } peer && (peer == me || !await Peers(c.Id).AnyAsync(x => x.Id == peer, ct))) throw LmsError.Missing();
        var threshold = data.Now.AddMinutes(-1);
        if (await data.Read<CohortMessage>().CountAsync(x => x.SenderId == me && x.SentUtc >= threshold, ct) >= 20)
            throw new ReusableAccess.Application.Errors.UseCaseException(429, "سرعت ارسال پیام بیش از حد مجاز است.");
        var row = new CohortMessage { CohortId = c.Id, SenderId = me, RecipientId = input.RecipientId, SentUtc = data.Now, Body = LmsError.Text(input.Text, 2000) };
        data.Db.Add(row);
        return await data.Finish(tx, row.Id, row.Id, ct);
    }
}
