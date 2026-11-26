using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed record AssessmentInfo(Assessment Assessment, OfferingInfo Offering);
internal sealed class LmsGrading(LmsData data, LmsAccess access) : ILmsGrading
{
    internal async Task<AssessmentInfo> Find(Guid assessmentId, CancellationToken ct)
    {
        var a = await data.Read<Assessment>().SingleOrDefaultAsync(x => x.Id == assessmentId, ct) ?? throw LmsError.Missing();
        return new(a, await access.Offering(a.OfferingId, ct));
    }

    private static void CheckWindow(AssessmentInfo i, DateTimeOffset now)
    {
        LmsData.Open(i.Offering.Term, now);
        if (!i.Offering.Offering.Active || !AcademicRules.CanSee(now, i.Offering.Offering.PublishedUtc, i.Offering.Offering.VisibleUtc) ||
            !i.Assessment.Active || i.Assessment.PublishedUtc is null) throw LmsError.Missing();
        if (!AssignmentPolicies.CanSubmit(now, i.Assessment, i.Offering.Term))
            throw LmsError.Conflict("بازهٔ مجاز تحویل آغاز نشده یا مهلت پایان یافته است.");
    }

    internal async Task<Enrollment> CheckSubmission(AssessmentInfo i, DateTimeOffset now, string permission, CancellationToken ct)
    {
        await access.AsStudent(permission, ct);
        var mine = await access.ActiveEnrollment(i.Assessment.OfferingId, ct);
        CheckWindow(i, now);
        var count = await Attempts(i.Assessment.Id, mine.Id).CountAsync(ct);
        if (count >= i.Assessment.MaxAttempts) throw LmsError.Conflict("حداکثر تعداد تلاش تکمیل شده است.");
        return mine;
    }

    private IQueryable<Submission> Attempts(Guid assessment, Guid enrollment)
        => data.Read<Submission>().Where(x => x.AssessmentId == assessment && x.EnrollmentId == enrollment);

    public async Task<Guid> SubmitAsync(Guid assessmentId, AttemptInput input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        await access.AsStudent(LmsPermissions.Submit, ct);
        var i = await Find(assessmentId, ct);
        var now = data.Now;
        var a = i.Assessment;
        var enrollment = await access.ActiveEnrollment(a.OfferingId, ct);
        var attempts = Attempts(a.Id, enrollment.Id);
        var body = LmsError.Text(input.Text, 32000, true);
        if (input.FileIds is { Length: > 8 } || input.Answers is { Length: > 30 } || input.RequestId == Guid.Empty)
            throw LmsError.Invalid("شناسهٔ درخواست یا تعداد فایل‌ها یا گزینه‌ها معتبر نیست.");
        var ids = (input.FileIds ?? []).Distinct().OrderBy(x => x).ToArray();
        var hash = input.RequestId.HasValue ? Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(new { Text = body, Files = ids, input.Answers }))) : null;
        if (input.RequestId is { } request && await attempts.SingleOrDefaultAsync(x => x.RequestId == request, ct) is { } previous)
        {
            if (!string.Equals(previous.RequestHash, hash, StringComparison.Ordinal))
                throw LmsError.Conflict("این شناسه پیش‌تر برای پاسخ دیگری استفاده شده است.");
            data.Resource(previous.Id);
            return previous.Id;
        }
        CheckWindow(i, now);
        if (await attempts.CountAsync(ct) >= a.MaxAttempts) throw LmsError.Conflict("حداکثر تعداد تلاش تکمیل شده است.");
        var quiz = AssessmentQuiz.Read(a, true);
        decimal? quizScore = null;
        if (quiz is not null)
        {
            var answers = input.Answers ?? [];
            if (ids.Length != 0 || body.Length != 0 || answers.Length != quiz.Length || answers.Any(x => x is < 0 or > 3))
                throw LmsError.Invalid("برای هر سؤال یک گزینه انتخاب کنید؛ آزمون پیوست یا متن جدا ندارد.");
            var correct = quiz.Select((q, j) => q.CorrectIndex == answers[j]).Count(x => x);
            quizScore = Math.Round(a.MaxScore * correct / quiz.Length, 2, MidpointRounding.AwayFromZero);
            body = "پاسخ‌های آزمون ثبت و نمرهٔ خودکار اعلام شد.";
        }
        else if (body.Length == 0 && ids.Length == 0 || input.Answers is not null)
            throw LmsError.Invalid("متن یا دست‌کم یک فایل لازم است؛ حداکثر ۸ فایل.");
        var actor = access.ActorId;
        if (ids.Length != 0)
        {
            await access.AsStudent(LmsPermissions.SubmissionUpload, ct);
            var count = await data.Read<Media>().CountAsync(x => ids.Contains(x.Id) && x.OwnerId == actor &&
                x.TargetId == assessmentId && x.Purpose == MediaPurpose.Submission, ct);
            if (count != ids.Length) throw LmsError.Missing();
            if (await data.Read<SubmissionFile>().AnyAsync(x => ids.Contains(x.MediaId), ct))
                throw LmsError.Conflict("در هر تلاش فایل را جداگانه ارسال کنید.");
        }
        var number = (await attempts.MaxAsync(x => (int?)x.Attempt, ct) ?? 0) + 1;
        var limit = AssignmentPolicies.ScoreLimit(now, a);
        var submission = new Submission { AssessmentId = a.Id, EnrollmentId = enrollment.Id, Attempt = number,
            SentUtc = now, IsLate = AssignmentPolicies.IsLate(now, a), Body = body, ScoreLimit = limit,
            QuizAnswers = quiz is null ? null : JsonSerializer.Serialize(input.Answers), RequestId = input.RequestId, RequestHash = hash };
        data.Db.Add(submission);
        if (quizScore is { } score)
            data.Db.Add(new AssessmentGrade { SubmissionId = submission.Id, Revision = 1, Score = score,
                Feedback = "تصحیح خودکار آزمون", Published = true, CreatedUtc = now });
        else
            data.Db.Add(new AssignmentCalendarReceipt { SubmissionId = submission.Id, InstructorId = i.Offering.Offering.LeadId,
                ReceivedUtc = now, Title = $"جواب تکلیف {i.Offering.Course.Title} {PersianScore(limit)}" });
        data.Db.AddRange(ids.Select(id => new SubmissionFile { SubmissionId = submission.Id, MediaId = id }));
        return await data.Finish(tx, submission.Id, submission.Id, ct);
    }

    private static string PersianScore(decimal score) => string.Concat(score.ToString("0.##", CultureInfo.InvariantCulture)
        .Select(c => c is >= '0' and <= '9' ? "۰۱۲۳۴۵۶۷۸۹"[c - '0'] : c == '.' ? '٫' : c));

    public async Task<AttemptRow[]> WorkAsync(Guid assessmentId, string view, CancellationToken ct)
    {
        var i = await Find(assessmentId, ct);
        var instructor = view == "teacher";
        if (instructor) await GradeRead(i.Offering, ct);
        else if (view == "student")
        {
            await access.AsStudent(LmsPermissions.Learn, ct);
            if (await access.OwnEnrollment(i.Assessment.OfferingId, ct) is null) throw LmsError.Missing();
        }
        else throw LmsError.Missing();
        var actor = access.ActorId;
        var rows = await (from s in data.Read<Submission>() join e in data.Read<Enrollment>() on s.EnrollmentId equals e.Id
                          join study in data.Read<StudyRecord>() on e.StudyRecordId equals study.Id
                          join u in data.Read<ApplicationUser>() on study.StudentId equals u.Id
                          where s.AssessmentId == assessmentId && e.OfferingId == i.Assessment.OfferingId &&
                                (instructor || study.StudentId == actor)
                          orderby u.DisplayName, s.Attempt descending select new { s, u.DisplayName }).Take(1000).ToArrayAsync(ct);
        var ids = rows.Select(x => x.s.Id).ToArray();
        var files = (await (from f in data.Read<SubmissionFile>() join m in data.Read<Media>() on f.MediaId equals m.Id
                            where ids.Contains(f.SubmissionId) select new { f.SubmissionId, File = new FileRow(m.Id, m.Name, m.Bytes, m.Extension) }).ToArrayAsync(ct))
            .ToLookup(x => x.SubmissionId, x => x.File);
        var grades = (await data.Read<AssessmentGrade>().Where(x => ids.Contains(x.SubmissionId) && (instructor || x.Published))
            .OrderByDescending(x => x.Revision).ToArrayAsync(ct)).ToLookup(x => x.SubmissionId, Row);
        return rows.Select(x => new AttemptRow(x.s.Id, x.s.EnrollmentId, instructor ? x.DisplayName : null, x.s.Attempt,
            x.s.SentUtc, x.s.IsLate, x.s.Body, files[x.s.Id].ToArray(), grades[x.s.Id].ToArray(), x.s.ScoreLimit)).ToArray();
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
        if (e.OfferingId != i.Assessment.OfferingId) throw LmsError.Missing();
        var feedback = await GuardGrade(input, i.Offering, e, s.ScoreLimit ?? i.Assessment.MaxScore, ct);
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
