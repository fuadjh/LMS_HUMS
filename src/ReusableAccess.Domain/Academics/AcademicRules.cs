using System.Globalization;
using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Domain.Academics;

public enum EnrollmentExitCheck { Change, AlreadyApplied, NotActive, TermClosed, OutsideWindow, InvalidReason }

public static class AcademicRules
{
    public static bool InWindow(DateTimeOffset now, DateTimeOffset opens, DateTimeOffset closes)
        => opens < closes && now >= opens && now < closes;

    public static bool IsClosed(Term term, DateTimeOffset now)
        => !term.Active || term.ClosedUtc is not null || now >= term.EndsUtc;

    // Closed terms are immutable. A retry of an already recorded transition is a no-op.
    public static EnrollmentExitCheck CheckExit(EnrollmentStatus status, bool admin, Term term,
        DateTimeOffset now, string? reason)
    {
        if (admin && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500))
            return EnrollmentExitCheck.InvalidReason;
        if (status == (admin ? EnrollmentStatus.RemovedByAdmin : EnrollmentStatus.WithdrawnByStudent))
            return EnrollmentExitCheck.AlreadyApplied;
        if (status != EnrollmentStatus.Active) return EnrollmentExitCheck.NotActive;
        if (IsClosed(term, now)) return EnrollmentExitCheck.TermClosed;
        if (!admin && !InWindow(now, term.DropOpensUtc, term.DropClosesUtc))
            return EnrollmentExitCheck.OutsideWindow;
        return EnrollmentExitCheck.Change;
    }

    public static bool Eligible(OfferingAudience audience, Guid courseFaculty, Guid? ownerMajor,
        Guid studentFaculty, Guid studentMajor, bool cohortAllowed) => cohortAllowed && (audience switch
    {
        OfferingAudience.General => true,
        OfferingAudience.Common => courseFaculty == studentFaculty,
        OfferingAudience.Specialized => ownerMajor == studentMajor,
        _ => false
    });

    public static bool CanSee(DateTimeOffset now, DateTimeOffset? published, DateTimeOffset? visible)
        => published is not null && visible is not null && visible.Value <= now;

    public static DateTimeOffset? Later(DateTimeOffset? first, DateTimeOffset? second)
        => first is null ? second : second is null || first.Value >= second.Value ? first : second;

    public static bool Inside(DateTimeOffset now, DateTimeOffset? begin, DateTimeOffset? end)
        => (begin is null || now >= begin.Value) && (end is null || now < end.Value);

    public static bool CanReadChapter(DateTimeOffset now, Chapter chapter)
        => chapter.Active && chapter.PublishedUtc is not null && Inside(now, chapter.VisibleFromUtc, chapter.VisibleUntilUtc);

    public static bool CanReadLesson(DateTimeOffset now, Chapter chapter, Lesson lesson)
        => lesson.Active && !lesson.NeedsAvailabilityReview && lesson.ChapterId == chapter.Id &&
           CanReadChapter(now, chapter) && Inside(now, lesson.VisibleFromUtc, lesson.VisibleUntilUtc);

    public static DateTimeOffset? Earlier(DateTimeOffset? first, DateTimeOffset? second)
        => first is null ? second : second is null || first.Value <= second.Value ? first : second;

}

public static class JalaliTime
{
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
    public static DateTime InTehran(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, Tehran).DateTime;

    public static string Display(DateTimeOffset utc, bool withTime = true)
    {
        var local = InTehran(utc);
        var calendar = new PersianCalendar();
        var date = FormattableString.Invariant($"{calendar.GetYear(local):0000}/{calendar.GetMonth(local):00}/{calendar.GetDayOfMonth(local):00}");
        return withTime ? date + local.ToString(" HH:mm", CultureInfo.InvariantCulture) : date;
    }

    public static DateTimeOffset FromDate(DateTime date)
    {
        var office = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        if (Tehran.IsInvalidTime(office) || Tehran.IsAmbiguousTime(office))
            throw new ArgumentException("این ساعت در تهران مبهم یا نامعتبر است.");
        return new DateTimeOffset(office, Tehran.GetUtcOffset(office)).ToUniversalTime();
    }

    public static bool TryParse(string? value, out DateTimeOffset utc)
    {
        utc = default;
        if (!TryParseLocal(value, out var local)) return false;
        try { utc = FromDate(local); return true; }
        catch (ArgumentException) { return false; }
    }

    public static bool TryParseLocal(string? value, out DateTime local)
    {
        local = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var pieces = IdentityRules.NormalizeDigits(value).Split(['/', ' ', ':'], StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length is not (3 or 5)) return false;
        var n = new int[pieces.Length];
        for (var i = 0; i < n.Length; i++)
            if (!int.TryParse(pieces[i], NumberStyles.None, CultureInfo.InvariantCulture, out n[i])) return false;
        if (n[0] is < 1300 or > 1600) return false;
        try
        {
            local = new PersianCalendar().ToDateTime(n[0], n[1], n[2], n.Length == 5 ? n[3] : 0, n.Length == 5 ? n[4] : 0, 0, 0);
            return true;
        }
        catch (ArgumentException) { return false; }
    }
}
