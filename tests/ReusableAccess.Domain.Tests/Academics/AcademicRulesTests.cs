using ReusableAccess.Domain.Academics;
using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Domain.Tests.Academics;

public sealed class AcademicRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
    private static Term Term() => new() { Active = true, DropOpensUtc = Now, DropClosesUtc = Now.AddHours(1), EndsUtc = Now.AddDays(60) };

    [Theory]
    [InlineData(-1, EnrollmentExitCheck.OutsideWindow)]
    [InlineData(0, EnrollmentExitCheck.Change)]
    [InlineData(59, EnrollmentExitCheck.Change)]
    [InlineData(60, EnrollmentExitCheck.OutsideWindow)]
    public void WithdrawalUsesHalfOpenOfficialWindow(int minute, EnrollmentExitCheck expected)
        => Assert.Equal(expected, AcademicRules.CheckExit(EnrollmentStatus.Active, false, Term(), Now.AddMinutes(minute), null));

    [Fact]
    public void ClosedTermRejectsNewExitsButRetryOfSameTransitionIsNoOp()
    {
        var t = Term(); t.ClosedUtc = Now;
        Assert.Equal(EnrollmentExitCheck.TermClosed, AcademicRules.CheckExit(EnrollmentStatus.Active, true, t, Now, "reason"));
        Assert.Equal(EnrollmentExitCheck.AlreadyApplied, AcademicRules.CheckExit(EnrollmentStatus.RemovedByAdmin, true, t, Now, "reason"));
        Assert.Equal(EnrollmentExitCheck.NotActive, AcademicRules.CheckExit(EnrollmentStatus.WithdrawnByStudent, true, t, Now, "reason"));
    }
    [Fact]
    public void AdminExitRequiresReasonAndMayOccurOutsideStudentWindow()
    {
        Assert.Equal(EnrollmentExitCheck.Change, AcademicRules.CheckExit(EnrollmentStatus.Active, true, Term(), Now.AddHours(2), " reason "));
        Assert.Equal(EnrollmentExitCheck.InvalidReason, AcademicRules.CheckExit(EnrollmentStatus.Active, true, Term(), Now, "   "));
        Assert.Equal(EnrollmentExitCheck.InvalidReason, AcademicRules.CheckExit(EnrollmentStatus.Active, true, Term(), Now, new string('x', 501)));
    }
    [Fact]
    public void AudienceCannotOverrideCohortOrCrossFacultyForCommonOffering()
    {
        var faculty = Guid.NewGuid(); var other = Guid.NewGuid(); var major = Guid.NewGuid();
        Assert.True(AcademicRules.Eligible(OfferingAudience.General, faculty, null, other, major, true));
        Assert.False(AcademicRules.Eligible(OfferingAudience.General, faculty, null, other, major, false));
        Assert.False(AcademicRules.Eligible(OfferingAudience.Common, faculty, null, other, major, true));
        Assert.True(AcademicRules.Eligible(OfferingAudience.Common, faculty, null, faculty, major, true));
        Assert.False(AcademicRules.Eligible(OfferingAudience.Specialized, faculty, Guid.NewGuid(), faculty, major, true));
        Assert.True(AcademicRules.Eligible(OfferingAudience.Specialized, faculty, major, faculty, major, true));
    }
    [Fact]
    public void ContentReadsOnlyFromItsOwnChapterAndLessonWindows()
    {
        var chapter = new Chapter { Active = true, PublishedUtc = Now.AddDays(-1) };
        var lesson = new Lesson { Active = true, ChapterId = chapter.Id, VisibleFromUtc = Now.AddMinutes(1) };
        Assert.True(AcademicRules.CanSee(Now, Now.AddDays(-1), Now));
        Assert.False(AcademicRules.CanReadLesson(Now, chapter, lesson));
        Assert.True(AcademicRules.CanReadLesson(Now.AddMinutes(1), chapter, lesson));
        lesson.NeedsAvailabilityReview = true;
        Assert.False(AcademicRules.CanReadLesson(Now.AddMinutes(1), chapter, lesson));
    }
    [Fact]
    public void JalaliParsesPersianAndArabicDigitsAndRoundTripsTehran()
    {
        Assert.True(JalaliTime.TryParse("۱۴۰۵/۰۸/۳۰ ١٣:٣٠", out var instant));
        Assert.Equal(Now, instant);
        Assert.Equal("1405/08/30 13:30", JalaliTime.Display(instant));
        Assert.False(JalaliTime.TryParse("۱۴۰۵/۱۳/۳۰ ۱۳:۳۰", out _));
    }
    [Fact]
    public void SyntheticChecksummedNumberPreservesLeadingZero()
    {
        // Generate a deterministic synthetic test value without embedding a person's national ID.
        var digits = Enumerable.Range(0, 9).Select(n => (char)('۰' + n)).ToArray();
        var partial = new string(digits);
        var mod = Enumerable.Range(0, 9).Sum(i => i * (10 - i)) % 11;
        var valid = partial + (char)('۰' + (mod < 2 ? mod : 11 - mod));
        Assert.True(IdentityRules.IsValidNationalId(valid));
        Assert.Equal('0', IdentityRules.NormalizeDigits(valid)[0]);
        Assert.False(IdentityRules.IsValidNationalId(valid[1..]));
    }
}
