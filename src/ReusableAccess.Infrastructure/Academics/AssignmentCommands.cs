using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class AssignmentCommands(LmsData data, LmsAccess access, LmsOfferings offerings) : IAssignmentCommands
{
    public async Task<Guid> SaveAsync(Guid offeringId, AssessmentEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await offerings.Editable(offeringId, LmsPermissions.Content, ct);
        var a = input.Id is { } id ? await Tracked(offeringId, id, ct) : new Assessment { OfferingId = offeringId };
        if (input.Id is not null)
        {
            CheckRevision(a, input.ExpectedRevision);
            if (a.PublishedUtc is not null || await data.Read<Submission>().AnyAsync(x => x.AssessmentId == a.Id, ct))
                throw LmsError.Conflict("ارزیابی دارای انتشار یا پاسخ است؛ مهلت تکلیف از مسیر تمدید تغییر می‌کند.");
        }
        var due = LmsError.Instant(input.DueUtc);
        var opens = input.OpensUtc is { } time ? LmsError.Instant(time) : (DateTimeOffset?)null;
        if (due <= data.Now || due < info.Term.StartsUtc || due > info.Term.EndsUtc || opens >= due || opens < info.Term.StartsUtc)
            throw LmsError.Invalid("موعد باید در آینده و در نیم‌سال باشد؛ آغاز ارسال باید پیش از موعد باشد.");
        if (input.TemplateVersionId is { } versionId)
        {
            if (input.Id is not null || input.Quiz is not null) throw LmsError.Invalid("نسخهٔ الگو فقط برای ایجاد تکلیف تازه است.");
            var template = await OwnVersion(versionId, ct);
            a.TemplateVersionId = template.Id;
            a.Title = template.Title;
            a.Prompt = template.Prompt;
            a.Rubric = template.Rubric;
            a.MaxScore = template.MaxScore;
            a.MaxAttempts = template.MaxAttempts;
            a.QuizSpec = null;
        }
        else
        {
            ScoreAndAttempts(input.MaxScore, input.MaxAttempts);
            a.Title = LmsError.Text(input.Title, 180);
            a.Prompt = LmsError.Text(input.Prompt, 16000);
            a.Rubric = LmsError.Text(input.Rubric, 8000, true);
            a.MaxScore = input.MaxScore;
            a.MaxAttempts = input.MaxAttempts;
            a.QuizSpec = AssessmentQuiz.Validate(input.Quiz);
        }
        a.OpensUtc = opens;
        a.DueUtc = due;
        a.CutoffUtc = due;
        a.VisibleUntilUtc = null; // An explicit edit of a never-published draft sets its own dates.
        a.ExtensionPenalty = null;
        a.Active = input.Active;
        a.Revision = Guid.NewGuid();
        if (input.Id is null) data.Db.Add(a);
        return await data.Finish(tx, a.Id, a.Id, ct);
    }

    public async Task<bool> PublishAsync(Guid offeringId, Guid assessmentId, PublishAssessmentInput input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await offerings.Editable(offeringId, LmsPermissions.Publish, ct);
        var a = await Tracked(offeringId, assessmentId, ct);
        if (a.PublishedUtc is not null) return true;
        CheckRevision(a, input.Revision);
        var now = data.Now;
        if (!a.Active || !AssignmentPolicies.Configured(a, info.Term) || AssignmentPolicies.Deadline(a) <= now)
            throw LmsError.Conflict("برای انتشار، ارزیابی باید فعال و مهلت آن معتبر و در آینده باشد.");
        a.PublishedUtc = now;
        a.Revision = Guid.NewGuid();
        return await data.Finish(tx, true, a.Id, ct);
    }

    public async Task<Guid> ExtendAsync(Guid offeringId, Guid assessmentId, ExtendAssignmentInput input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        // Content permission is allowed only for the lead by AsTeaching, even when a co-teacher may grade or publish.
        var info = await offerings.Editable(offeringId, LmsPermissions.Content, ct);
        var a = await Tracked(offeringId, assessmentId, ct);
        CheckRevision(a, input.Revision);
        if (a.QuizSpec is not null || a.PublishedUtc is null || !a.Active || !AssignmentPolicies.Configured(a, info.Term) ||
            a.CutoffUtc is not { } previous)
            throw LmsError.Conflict("فقط تکلیف فعال و منتشرشده با موعد معتبر قابل تمدید است.");
        var now = data.Now;
        var end = LmsError.Instant(input.DeadlineUtc);
        if (end <= previous || end <= now || end > info.Term.EndsUtc)
            throw LmsError.Invalid("مهلت جدید باید بعد از مهلت فعلی و زمان جاری و حداکثر تا پایان نیم‌سال باشد.");
        if (!AssignmentPolicies.ValidPenalty(input.Penalty, a.MaxScore))
            throw LmsError.Invalid("کسر نمره باید بین صفر و سقف پایه و حداکثر با دو رقم اعشار باشد.");
        var revision = new AssignmentExtension { AssessmentId = a.Id, InstructorId = access.ActorId,
            PreviousDeadlineUtc = AssignmentPolicies.Deadline(a) ?? previous, DeadlineUtc = end,
            Penalty = input.Penalty, RecordedUtc = now };
        a.CutoffUtc = end;
        if (a.VisibleUntilUtc is { } priorVisibility && priorVisibility < end) a.VisibleUntilUtc = end;
        a.ExtensionPenalty = input.Penalty;
        a.Revision = revision.Id;
        data.Db.Add(revision);
        return await data.Finish(tx, revision.Id, a.Id, ct);
    }

    public async Task<Guid> SaveTemplateAsync(AssignmentTemplateInput input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        await access.AsTeacher(LmsPermissions.Template, ct);
        ScoreAndAttempts(input.MaxScore, input.MaxAttempts);
        var id = Guid.NewGuid();
        var family = id;
        var number = 1;
        if (input.PreviousVersionId is { } previousId)
        {
            var previous = await OwnVersion(previousId, ct);
            var owner = access.ActorId;
            if (await data.Read<AssignmentTemplateVersion>().AnyAsync(v => v.FamilyId == previous.FamilyId &&
                v.OwnerId == owner && v.Number > previous.Number, ct))
                throw LmsError.Conflict("نسخهٔ تازه‌تری ثبت شده است؛ جدیدترین نسخه را باز کنید.");
            family = previous.FamilyId;
            number = checked(previous.Number + 1);
        }
        data.Db.Add(new AssignmentTemplateVersion { Id = id, FamilyId = family, OwnerId = access.ActorId, Number = number,
            Title = LmsError.Text(input.Title, 180), Prompt = LmsError.Text(input.Prompt, 16000),
            Rubric = LmsError.Text(input.Rubric, 8000, true), MaxAttempts = input.MaxAttempts, MaxScore = input.MaxScore, CreatedUtc = data.Now });
        return await data.Finish(tx, id, id, ct);
    }

    private async Task<AssignmentTemplateVersion> OwnVersion(Guid id, CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        var owner = access.ActorId;
        return await data.Read<AssignmentTemplateVersion>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct)
            ?? throw LmsError.Missing();
    }

    private async Task<Assessment> Tracked(Guid offeringId, Guid id, CancellationToken ct)
        => await data.Db.Set<Assessment>().SingleOrDefaultAsync(a => a.OfferingId == offeringId && a.Id == id, ct) ?? throw LmsError.Missing();

    private static void CheckRevision(Assessment a, Guid? revision)
    {
        if (a.Revision != revision) throw LmsError.Conflict("دادهٔ تکلیف تغییر کرده است؛ دوباره آن را بارگیری کنید.");
    }

    private static void ScoreAndAttempts(decimal maxScore, int maxAttempts)
    {
        if (maxScore is <= 0 or > 1000 || decimal.Round(maxScore, 2) != maxScore || maxAttempts is < 1 or > 20)
            throw LmsError.Invalid("سقف نمره یا تعداد تلاش‌ها معتبر نیست.");
    }
}
