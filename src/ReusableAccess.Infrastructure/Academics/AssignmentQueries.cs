using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class AssignmentQueries(LmsData data, LmsAccess access) : IAssignmentQueries
{
    private sealed record Workspace(OfferingInfo Info, bool Teacher, bool SeesPublished, bool Lead, CoTeacher? Co,
        Enrollment? Own, bool CanSubmit, bool Writable);

    private async Task<Workspace> Authorize(Guid offeringId, string view, DateTimeOffset now, CancellationToken ct)
    {
        var info = await access.Offering(offeringId, ct);
        var offer = info.Offering;
        var visible = offer.Active && AcademicRules.CanSee(now, offer.PublishedUtc, offer.VisibleUtc);
        var closed = AcademicRules.IsClosed(info.Term, now);
        if (view is "teacher" or "preview")
        {
            await access.AsTeaching(info, LmsPermissions.Teach, ct);
            var lead = offer.LeadId == access.ActorId;
            var co = lead ? null : await access.CoAssignment(offeringId, ct);
            return new(info, view == "teacher", visible, lead, co, null, false, !closed && offer.Active);
        }
        if (view != "student") throw LmsError.Missing();
        await access.AsStudent(LmsPermissions.Learn, ct);
        var own = await access.OwnEnrollment(offeringId, ct);
        var study = await access.ActiveStudy(ct);
        if (own is null && (!visible || closed || !await access.Eligible(info, study, ct))) throw LmsError.Missing();
        var current = own is { Status: EnrollmentStatus.Active } && study is not null && study.Record.Id == own.StudyRecordId && study.Cohort.Active;
        var canRead = visible && own?.Status == EnrollmentStatus.Active && (closed || current);
        return new(info, false, canRead, false, null, own,
            canRead && current && !closed && access.HasStudent(LmsPermissions.Submit), false);
    }

    private IQueryable<Assessment> Rows(Workspace workspace)
    {
        var offeringId = workspace.Info.Offering.Id;
        var rows = data.Read<Assessment>().Where(a => a.OfferingId == offeringId);
        if (workspace.Teacher) return rows;
        var visible = workspace.SeesPublished;
        if (workspace.Own is null)
            return rows.Where(a => visible && a.Active && a.PublishedUtc != null && a.DueUtc != null && a.CutoffUtc != null);
        var actor = access.ActorId;
        // Include a student's own historical attempts even after withdrawal; never return private content via that path.
        var ownWork = from s in data.Read<Submission>() join e in data.Read<Enrollment>() on s.EnrollmentId equals e.Id
                      join study in data.Read<StudyRecord>() on e.StudyRecordId equals study.Id
                      where e.OfferingId == offeringId && study.StudentId == actor select s.AssessmentId;
        return rows.Where(a => visible && a.Active && a.PublishedUtc != null && a.DueUtc != null && a.CutoffUtc != null || ownWork.Contains(a.Id));
    }

    private bool CanAuthor(Workspace w) => w.Teacher && w.Lead && w.Writable && access.HasTeacher(LmsPermissions.Content);
    private static AssignmentSummary Summary(AssignmentStatus status, Assessment a) => new(a.Id, a.Title, a.QuizSpec is not null,
        status, a.OpensUtc, a.DueUtc, AssignmentPolicies.Deadline(a), a.MaxScore, a.MaxAttempts);

    public async Task<AssignmentPanel> ListAsync(Guid offeringId, string view, CancellationToken ct)
    {
        var now = data.Now;
        var w = await Authorize(offeringId, view, now, ct);
        var assessments = await Rows(w).OrderBy(a => a.DueUtc == null).ThenBy(a => a.DueUtc).ThenBy(a => a.Id).Take(500).ToArrayAsync(ct);
        return new(assessments.Select(a => Summary(AssignmentPolicies.Status(now, a, w.Info.Term), a)).ToArray(), CanAuthor(w));
    }

    public async Task<AssessmentRow> DetailAsync(Guid offeringId, Guid assessmentId, string view, CancellationToken ct)
    {
        var now = data.Now;
        var w = await Authorize(offeringId, view, now, ct);
        var a = await Rows(w).SingleOrDefaultAsync(x => x.Id == assessmentId, ct) ?? throw LmsError.Missing();
        var status = AssignmentPolicies.Status(now, a, w.Info.Term);
        var readable = w.Teacher || w.SeesPublished && AssignmentPolicies.Readable(now, a);
        var submissions = data.Read<Submission>().Where(s => s.AssessmentId == a.Id);
        var canEdit = CanAuthor(w) && a.PublishedUtc is null && !await submissions.AnyAsync(ct);
        var canPublish = w.Teacher && w.Writable && a.Active && a.PublishedUtc is null && access.HasTeacher(LmsPermissions.Publish) &&
                         (w.Lead || w.Co?.CanPublish == true);
        var canExtend = CanAuthor(w) && a.Active && a.PublishedUtc is not null && a.QuizSpec is null && AssignmentPolicies.Configured(a, w.Info.Term);
        var canSubmit = readable && w.CanSubmit && AssignmentPolicies.CanSubmit(now, a, w.Info.Term) && w.Own is { } own &&
                        await submissions.CountAsync(x => x.EnrollmentId == own.Id, ct) < a.MaxAttempts;
        var canReadWork = view == "student" && w.Own is not null || w.Teacher && (w.Lead || w.Co?.CanGrade == true) &&
                          (access.HasTeacher(LmsPermissions.Grade) || access.HasTeacher(LmsPermissions.Release));
        var extensions = await data.Read<AssignmentExtension>().Where(e => e.AssessmentId == a.Id)
            .OrderByDescending(e => e.RecordedUtc).ThenByDescending(e => e.Id).Take(30)
            .Select(e => new AssignmentExtensionRow(e.DeadlineUtc, e.Penalty, e.RecordedUtc)).ToArrayAsync(ct);
        return new(Summary(status, a), readable ? a.Prompt : null, readable ? a.Rubric : null,
            readable ? AssessmentQuiz.Read(a, w.Teacher && w.Lead) : null, readable, canSubmit, canEdit, canPublish, canExtend,
            w.Teacher ? a.Revision : Guid.Empty,
            w.SeesPublished && AssignmentPolicies.CanSubmit(now, a, w.Info.Term) || w.Teacher && AssignmentPolicies.CanSubmit(now, a, w.Info.Term)
                ? AssignmentPolicies.ScoreLimit(now, a) : null,
            a.ExtensionPenalty, extensions, canReadWork);
    }

    public async Task<AssignmentQueueRow[]> QueueAsync(CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Teach, ct);
        if (!access.HasTeacher(LmsPermissions.Grade) && !access.HasTeacher(LmsPermissions.Release)) throw LmsError.Denied();
        var actor = access.ActorId;
        var now = data.Now;
        var members = data.Read<CoTeacher>().Where(c => c.InstructorId == actor && c.Active && c.CanGrade);
        var work = from s in data.Read<Submission>()
                   join e in data.Read<Enrollment>() on s.EnrollmentId equals e.Id
                   where e.Status == EnrollmentStatus.Active && !data.Read<AssessmentGrade>().Any(g => g.SubmissionId == s.Id && g.Published)
                   group s by new { s.AssessmentId, e.OfferingId } into g
                   select new { g.Key.AssessmentId, g.Key.OfferingId, Pending = g.Count() };
        return await (from item in work
                      join a in data.Read<Assessment>() on new { Id = item.AssessmentId, item.OfferingId } equals new { a.Id, a.OfferingId }
                      join o in data.Read<Offering>() on a.OfferingId equals o.Id
                      join t in data.Read<Term>() on o.TermId equals t.Id
                      join c in data.Read<CatalogCourse>() on o.CourseId equals c.Id
                      where a.Active && a.PublishedUtc != null && o.Active && t.Active && t.ClosedUtc == null && now < t.EndsUtc &&
                            (o.LeadId == actor || members.Any(m => m.OfferingId == o.Id))
                      orderby item.Pending descending, a.Id
                      select new AssignmentQueueRow(o.Id, a.Id, c.Title, o.GroupCode, a.Title, item.Pending)).Take(100).ToArrayAsync(ct);
    }

    public async Task<AssignmentTemplateRow[]> TemplatesAsync(string? search, int page, CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        if (page is < 1 or > 10000) throw LmsError.Invalid("شمارهٔ صفحه معتبر نیست.");
        const int size = 24;
        var owner = access.ActorId;
        var q = LmsError.Text(search, 80, true);
        return await data.Read<AssignmentTemplateVersion>().Where(v => v.OwnerId == owner && (q == "" || v.Title.Contains(q)))
            .OrderByDescending(v => v.CreatedUtc).ThenByDescending(v => v.Number).ThenBy(v => v.Id)
            .Skip((page - 1) * size).Take(size + 1)
            .Select(v => new AssignmentTemplateRow(v.Id, v.FamilyId, v.Number, v.Title, v.Prompt, v.Rubric,
                v.MaxAttempts, v.MaxScore, v.CreatedUtc)).ToArrayAsync(ct);
    }
}
