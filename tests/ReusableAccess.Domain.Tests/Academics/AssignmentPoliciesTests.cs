using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Domain.Tests.Academics;

public sealed class AssignmentPoliciesTests
{
    private static readonly DateTimeOffset Origin = new(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);
    private static Assessment Assignment() => new()
    {
        Active = true, PublishedUtc = Origin, OpensUtc = Origin.AddHours(1), DueUtc = Origin.AddHours(2),
        CutoffUtc = Origin.AddHours(3), MaxScore = 20, MaxAttempts = 2, ExtensionPenalty = 2
    };
    private static Term Term() => new() { Active = true, StartsUtc = Origin, EndsUtc = Origin.AddDays(3) };

    [Fact]
    public void OnlyTheTimeOfSubmissionSelectsTheCap()
    {
        var a = Assignment();
        var due = a.DueUtc!.Value;
        Assert.Equal(20m, AssignmentPolicies.ScoreLimit(due.AddTicks(-1), a));
        Assert.Equal(18m, AssignmentPolicies.ScoreLimit(due, a));
        Assert.Equal(AssignmentStatus.Extended, AssignmentPolicies.Status(due, a, Term()));
        Assert.True(AssignmentPolicies.CanSubmit(due, a, Term()));
        Assert.Equal(AssignmentStatus.Expired, AssignmentPolicies.Status(a.CutoffUtc!.Value, a, Term()));
        Assert.False(AssignmentPolicies.CanSubmit(a.CutoffUtc.Value, a, Term()));
    }

    [Fact]
    public void UpcomingDraftAndExpiredAreNotAccepting()
    {
        var a = Assignment();
        Assert.Equal(AssignmentStatus.Upcoming, AssignmentPolicies.Status(Origin, a, Term()));
        Assert.False(AssignmentPolicies.CanSubmit(Origin, a, Term()));
        a.CutoffUtc = a.DueUtc;
        Assert.Equal(AssignmentStatus.Open, AssignmentPolicies.Status(a.OpensUtc!.Value, a, Term()));
        Assert.False(AssignmentPolicies.CanSubmit(a.DueUtc!.Value, a, Term()));
        a.PublishedUtc = null;
        Assert.Equal(AssignmentStatus.Draft, AssignmentPolicies.Status(a.OpensUtc.Value, a, Term()));
    }

    [Fact]
    public void LegacyReadBoundaryIsRespectedWithoutAnyReferenceToTheChapter()
    {
        var a = Assignment();
        a.VisibleUntilUtc = Origin.AddMinutes(100);
        Assert.Equal(a.VisibleUntilUtc, AssignmentPolicies.Deadline(a));
        Assert.False(AssignmentPolicies.Readable(a.VisibleUntilUtc.Value, a));
        Assert.False(AssignmentPolicies.CanSubmit(a.VisibleUntilUtc.Value, a, Term()));
    }

    [Fact]
    public void ExtensionPenaltyOnlyAppliesToAssignmentsAndMayBeZero()
    {
        var a = Assignment();
        Assert.True(AssignmentPolicies.ValidPenalty(0, 20));
        Assert.False(AssignmentPolicies.ValidPenalty(-1, 20));
        Assert.False(AssignmentPolicies.ValidPenalty(21, 20));
        Assert.False(AssignmentPolicies.ValidPenalty(1.001m, 20));
        a.ExtensionPenalty = 0;
        Assert.Equal(20m, AssignmentPolicies.ScoreLimit(a.DueUtc!.Value, a));
        a.ExtensionPenalty = 2;
        a.QuizSpec = "[]";
        Assert.Equal(20m, AssignmentPolicies.ScoreLimit(a.DueUtc.Value, a));
    }
}
