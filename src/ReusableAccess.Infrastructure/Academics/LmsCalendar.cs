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
        var scheduled = await Events(offers, fromUtc, toUtc, view == "student", ct);
        if (view != "teacher") return scheduled;
        // Receipt data is scoped independently of the upcoming-event/notification job.
        var receipts = await (from r in data.Read<AssignmentCalendarReceipt>()
                              join s in data.Read<Submission>() on r.SubmissionId equals s.Id
                              join a in data.Read<Assessment>() on s.AssessmentId equals a.Id
                              join o in data.Read<Offering>() on a.OfferingId equals o.Id
                              where r.InstructorId == id && o.LeadId == id && r.ReceivedUtc >= fromUtc && r.ReceivedUtc < toUtc
                              orderby r.ReceivedUtc descending, r.SubmissionId
                              select new CalendarEntry(a.OfferingId, s.Id, "assignment-received", r.Title, r.ReceivedUtc, a.Id))
            .ToArrayAsync(ct);
        return scheduled.Concat(receipts).OrderBy(x => x.AtUtc).ThenBy(x => x.Title).ThenBy(x => x.EventId).ToArray();
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

        var assessments =
            from a in data.Read<Assessment>()
            join o in data.Read<Offering>() on a.OfferingId equals o.Id
            join course in data.Read<CatalogCourse>() on o.CourseId equals course.Id
            where offers.Contains(o.Id) && a.Active && (!published || a.PublishedUtc != null)
            select new
            {
                OfferingId = o.Id, AssessmentId = a.Id, Title = a.Title + " · " + course.Title, a.DueUtc, a.OpensUtc,
                Cutoff = a.VisibleUntilUtc != null && (a.CutoffUtc == null || a.VisibleUntilUtc < a.CutoffUtc)
                    ? a.VisibleUntilUtc : a.CutoffUtc
            };

        var due = await assessments.Where(x => x.DueUtc >= fromUtc && x.DueUtc < toUtc && x.DueUtc <= x.Cutoff)
            .Select(x => new CalendarEntry(x.OfferingId, x.AssessmentId, "due", x.Title, x.DueUtc!.Value, x.AssessmentId)).ToArrayAsync(ct);
        var late = await assessments.Where(x => x.Cutoff != x.DueUtc && x.Cutoff >= fromUtc && x.Cutoff < toUtc)
            .Select(x => new CalendarEntry(x.OfferingId, x.AssessmentId, "cutoff", x.Title, x.Cutoff!.Value, x.AssessmentId)).ToArrayAsync(ct);
        var openingAssessments = await assessments.Where(x => x.OpensUtc >= fromUtc && x.OpensUtc < toUtc && x.OpensUtc < x.Cutoff)
            .Select(x => new CalendarEntry(x.OfferingId, x.AssessmentId, "assessment-open", x.Title, x.OpensUtc!.Value, x.AssessmentId)).ToArrayAsync(ct);
        var openingChapters = await (from c in chapters
                                     join o in data.Read<Offering>() on c.OfferingId equals o.Id
                                     join course in data.Read<CatalogCourse>() on o.CourseId equals course.Id
                                     where offers.Contains(o.Id) && c.VisibleFromUtc >= fromUtc && c.VisibleFromUtc < toUtc
                                     select new CalendarEntry(o.Id, c.Id, "chapter-open", c.Title + " · " + course.Title, c.VisibleFromUtc!.Value))
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

        return openingAssessments
            .Concat(openingChapters)
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
