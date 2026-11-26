using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class CourseDiscussionAccess(LmsData data, LmsAccess access)
{
    internal async Task<bool> CanReadAndMayPost(Guid offeringId, string view, CancellationToken ct)
    {
        var now = data.Now;
        var info = await access.Offering(offeringId, ct);
        var closed = AcademicRules.IsClosed(info.Term, now);
        if (view == "teacher")
        {
            await access.AsTeaching(info, LmsPermissions.Teach, ct);
            return info.Offering.Active && !closed;
        }
        if (view != "student") throw LmsError.Missing();
        await access.AsStudent(LmsPermissions.Chat, ct);
        var own = await access.OwnEnrollment(offeringId, ct);
        var activeStudy = await access.ActiveStudy(ct);
        if (own is not { Status: EnrollmentStatus.Active } || !info.Offering.Active ||
            !AcademicRules.CanSee(now, info.Offering.PublishedUtc, info.Offering.VisibleUtc)) throw LmsError.Missing();
        var current = activeStudy is not null && activeStudy.Record.Id == own.StudyRecordId && activeStudy.Cohort.Active;
        if (!current && !closed) throw LmsError.Missing();
        return current && !closed && access.HasStudent(LmsPermissions.Send);
    }
}

internal sealed class CourseDiscussionQueries(LmsData data, LmsAccess access, CourseDiscussionAccess room) : ICourseDiscussionQueries
{
    public async Task<CourseDiscussionPage> ListAsync(Guid offeringId, string view, int page, CancellationToken ct)
    {
        if (page is < 1 or > 10000) throw LmsError.Invalid("شمارهٔ صفحه معتبر نیست.");
        var canPost = await room.CanReadAndMayPost(offeringId, view, ct);
        var actor = access.ActorId;
        const int size = 35;
        var rows = await (from m in data.Read<CourseDiscussionMessage>()
                          join u in data.Read<ApplicationUser>() on m.SenderId equals u.Id
                          where m.OfferingId == offeringId
                          orderby m.SentUtc descending, m.Id descending
                          select new CourseDiscussionRow(m.Id, u.DisplayName, m.SenderId == actor, m.ByInstructor, m.Text, m.SentUtc))
            .Skip((page - 1) * size).Take(size + 1).ToArrayAsync(ct);
        return new(rows.Take(size).Reverse().ToArray(), canPost, rows.Length > size);
    }
}

internal sealed class CourseDiscussionCommands(LmsData data, LmsAccess access, CourseDiscussionAccess room) : ICourseDiscussionCommands
{
    public async Task<Guid> PostAsync(Guid offeringId, string view, CourseDiscussionPost input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        if (!await room.CanReadAndMayPost(offeringId, view, ct)) throw LmsError.Denied();
        var now = data.Now;
        var actor = access.ActorId;
        var since = now.AddMinutes(-1);
        if (await data.Read<CourseDiscussionMessage>().CountAsync(x => x.SenderId == actor && x.SentUtc >= since, ct) >= 20)
            throw new UseCaseException(429, "سرعت ارسال پیام بیش از حد مجاز است.");
        var entry = new CourseDiscussionMessage { OfferingId = offeringId, SenderId = actor, ByInstructor = view == "teacher",
            Text = LmsError.Text(input.Text, 2000), SentUtc = now };
        data.Db.Add(entry);
        return await data.Finish(tx, entry.Id, offeringId, ct);
    }
}
