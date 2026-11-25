using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed record AssessmentInfo(Assessment Assessment, Chapter Chapter, OfferingInfo Offering);
internal sealed class LmsGrading(LmsData data, LmsAccess access) : ILmsGrading
{
    internal async Task<AssessmentInfo> Find(Guid assessmentId, CancellationToken ct)
    {
        var a = await data.Read<Assessment>().SingleOrDefaultAsync(x => x.Id == assessmentId, ct) ?? throw LmsError.Missing();
        var c = await data.Read<Chapter>().SingleAsync(x => x.Id == a.ChapterId, ct);
        var o = await access.Offering(c.OfferingId, ct);
        return new(a, c, o);
    }
    internal async Task<Enrollment> CheckSubmission(AssessmentInfo i, DateTimeOffset now, string permission, CancellationToken ct)
    {
        await access.AsStudent(permission, ct);
        LmsData.Open(i.Offering.Term, now);
        if (!i.Offering.Offering.Active || !AcademicRules.CanReadAssessment(now, i.Chapter, i.Assessment) ||
            !AcademicRules.CanSee(now, i.Offering.Offering.PublishedUtc, i.Offering.Offering.VisibleUtc)) throw LmsError.Missing();
        if (!LmsOfferings.Publishable(i.Assessment, i.Offering.Term) || now >= i.Assessment.CutoffUtc)
            throw LmsError.Conflict("مهلت نهایی تحویل گذشته یا تنظیم نشده است.");
        var mine = await access.ActiveEnrollment(i.Chapter.OfferingId, ct);
        var count = await data.Read<Submission>().CountAsync(x => x.AssessmentId == i.Assessment.Id && x.EnrollmentId == mine.Id, ct);
        if (count >= i.Assessment.MaxAttempts) throw LmsError.Conflict("حداکثر تعداد تلاش تکمیل شده است.");
        return mine;
    }
    public async Task<Guid> SubmitAsync(Guid assessmentId, AttemptInput input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var i = await Find(assessmentId, ct);
        var now = data.Now;
        var e = await CheckSubmission(i, now, LmsPermissions.Submit, ct);
        var body = LmsError.Text(input.Text, 32000, true);
        var ids = (input.FileIds ?? []).Distinct().ToArray();
        var quiz = LmsOfferings.QuizRows(i.Assessment, true);
        decimal? quizScore = null;
        if (quiz is not null)
        {
            var answers = input.Answers ?? [];
            if (ids.Length != 0 || body.Length != 0 || answers.Length != quiz.Length || answers.Any(x => x is < 0 or > 3))
                throw LmsError.Invalid("برای هر سؤال یک گزینه انتخاب کنید؛ آزمون پیوست یا متن جدا ندارد.");
            var correct = quiz.Select((q, j) => q.CorrectIndex == answers[j]).Count(x => x);
            quizScore = Math.Round(i.Assessment.MaxScore * correct / quiz.Length, 2, MidpointRounding.AwayFromZero);
            body = "پاسخ‌های آزمون ثبت و نمرهٔ خودکار اعلام شد.";
        }
        else if (ids.Length > 8 || body.Length == 0 && ids.Length == 0 || input.Answers is not null)
            throw LmsError.Invalid("متن یا دست‌کم یک فایل لازم است؛ حداکثر ۸ فایل.");
        var count = await data.Read<Media>().CountAsync(x => ids.Contains(x.Id) && x.OwnerId == access.ActorId && x.TargetId == assessmentId && x.Purpose == MediaPurpose.Submission, ct);
        if (count != ids.Length) throw LmsError.Missing();
        if (await data.Read<SubmissionFile>().AnyAsync(x => ids.Contains(x.MediaId), ct))
            throw LmsError.Conflict("در هر تلاش فایل را جداگانه ارسال کنید.");
        var number = (await data.Read<Submission>().Where(x => x.EnrollmentId == e.Id && x.AssessmentId == assessmentId).MaxAsync(x => (int?)x.Attempt, ct) ?? 0) + 1;
        var s = new Submission { AssessmentId = assessmentId, EnrollmentId = e.Id, Attempt = number, SentUtc = now, IsLate = now >= i.Assessment.DueUtc, Body = body, QuizAnswers = quiz is null ? null : JsonSerializer.Serialize(input.Answers) };
        data.Db.Add(s);
        if (quizScore is { } score) data.Db.Add(new AssessmentGrade
        { SubmissionId = s.Id, Revision = 1, Score = score, Feedback = "تصحیح خودکار آزمون", Published = true, CreatedUtc = now });
        data.Db.AddRange(ids.Select(id => new SubmissionFile { SubmissionId = s.Id, MediaId = id }));
        return await data.Finish(tx, s.Id, s.Id, ct);
    }
    public async Task<AttemptRow[]> WorkAsync(Guid assessmentId, string view, CancellationToken ct)
    {
        var i = await Find(assessmentId, ct);
        var instructor = view == "teacher";
        if (instructor) await GradeRead(i.Offering, ct);
        else if (view == "student") await access.AsStudent(LmsPermissions.Learn, ct);
        else throw LmsError.Missing();
        var actor = access.ActorId;
        var rows = await (from s in data.Read<Submission>() join e in data.Read<Enrollment>() on s.EnrollmentId equals e.Id
                          join study in data.Read<StudyRecord>() on e.StudyRecordId equals study.Id
                          join u in data.Read<ApplicationUser>() on study.StudentId equals u.Id
                          where s.AssessmentId == assessmentId && (instructor || study.StudentId == actor)
                          orderby u.DisplayName, s.Attempt descending select new { s, u.DisplayName }).Take(1000).ToArrayAsync(ct);
        var ids = rows.Select(x => x.s.Id).ToArray();
        var files = (await (from f in data.Read<SubmissionFile>() join m in data.Read<Media>() on f.MediaId equals m.Id
                            where ids.Contains(f.SubmissionId) select new { f.SubmissionId, File = new FileRow(m.Id, m.Name, m.Bytes, m.Extension) }).ToArrayAsync(ct))
            .ToLookup(x => x.SubmissionId, x => x.File);
        var grades = (await data.Read<AssessmentGrade>().Where(x => ids.Contains(x.SubmissionId) && (instructor || x.Published))
            .OrderByDescending(x => x.Revision).ToArrayAsync(ct)).ToLookup(x => x.SubmissionId, Row);
        return rows.Select(x => new AttemptRow(x.s.Id, x.s.EnrollmentId, instructor ? x.DisplayName : null, x.s.Attempt,
            x.s.SentUtc, x.s.IsLate, x.s.Body, files[x.s.Id].ToArray(), grades[x.s.Id].ToArray())).ToArray();
    }
    internal async Task GradeRead(OfferingInfo i, CancellationToken ct)
    {
        await access.Load(ct);
        await access.AsTeaching(i, access.HasTeacher(LmsPermissions.Grade) ? LmsPermissions.Grade : LmsPermissions.Release, ct);
    }
    private static GradeRow Row(AssessmentGrade g) => new(g.Revision, g.Score, g.Feedback, g.Published, g.CreatedUtc);
    private static GradeRow Row(FinalGrade g) => new(g.Revision, g.Score, g.Feedback, g.Published, g.CreatedUtc);

    public async Task<Guid> GradeAsync(Guid submissionId, GradeEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var s = await data.Read<Submission>().SingleOrDefaultAsync(x => x.Id == submissionId, ct) ?? throw LmsError.Missing();
        var i = await Find(s.AssessmentId, ct);
        var e = await data.Read<Enrollment>().SingleAsync(x => x.Id == s.EnrollmentId, ct);
        var feedback = await GuardGrade(input, i.Offering, e, i.Assessment.MaxScore, ct);
        var rev = (await data.Read<AssessmentGrade>().Where(x => x.SubmissionId == submissionId).MaxAsync(x => (int?)x.Revision, ct) ?? 0) + 1;
        var row = new AssessmentGrade { SubmissionId = submissionId, GraderId = access.ActorId, Revision = rev,
            Score = input.Score, Feedback = feedback, Published = input.Publish, CreatedUtc = data.Now };
        data.Db.Add(row);
        return await data.Finish(tx, row.Id, submissionId, ct);
    }
    public async Task<Guid> FinalAsync(Guid enrollmentId, GradeEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var e = await data.Read<Enrollment>().SingleOrDefaultAsync(x => x.Id == enrollmentId, ct) ?? throw LmsError.Missing();
        var i = await access.Offering(e.OfferingId, ct);
        // Only the assigned lead authors the unofficial final grade; co-teachers may grade submissions.
        if (i.Offering.LeadId != access.ActorId) throw LmsError.Missing();
        var feedback = await GuardGrade(input, i, e, 20, ct);
        var revision = (await data.Read<FinalGrade>().Where(x => x.EnrollmentId == enrollmentId).MaxAsync(x => (int?)x.Revision, ct) ?? 0) + 1;
        var row = new FinalGrade { EnrollmentId = e.Id, GraderId = access.ActorId, Revision = revision,
            Score = input.Score, Feedback = feedback, Published = input.Publish, CreatedUtc = data.Now };
        data.Db.Add(row);
        return await data.Finish(tx, row.Id, e.Id, ct);
    }
    public async Task<GradeRow[]> FinalsAsync(Guid enrollmentId, CancellationToken ct)
    {
        var e = await data.Read<Enrollment>().SingleOrDefaultAsync(x => x.Id == enrollmentId, ct) ?? throw LmsError.Missing();
        var i = await access.Offering(e.OfferingId, ct);
        if (i.Offering.LeadId != access.ActorId) throw LmsError.Missing();
        await GradeRead(i, ct);
        return (await data.Read<FinalGrade>().Where(x => x.EnrollmentId == enrollmentId).OrderByDescending(x => x.Revision).ToArrayAsync(ct)).Select(Row).ToArray();
    }
    private async Task<string> GuardGrade(GradeEdit input, OfferingInfo i, Enrollment e, decimal max, CancellationToken ct)
    {
        await access.AsTeaching(i, input.Publish ? LmsPermissions.Release : LmsPermissions.Grade, ct);
        LmsData.Open(i.Term, data.Now);
        if (!i.Offering.Active || e.Status != EnrollmentStatus.Active) throw LmsError.Conflict("ثبت‌نام یا ارائه فعال نیست؛ سوابق خواندنی می‌مانند.");
        if (input.Score < 0 || input.Score > max || decimal.Round(input.Score, 2) != input.Score)
            throw LmsError.Invalid("نمره باید در بازهٔ مجاز با حداکثر دو رقم اعشار باشد.");
        return LmsError.Text(input.Feedback, 8000, true);
    }
}
