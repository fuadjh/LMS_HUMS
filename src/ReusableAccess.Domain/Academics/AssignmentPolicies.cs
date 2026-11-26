namespace ReusableAccess.Domain.Academics;

public enum AssignmentStatus { Draft, Upcoming, Open, Extended, Expired, Archived }

public static class AssignmentPolicies
{
    public static DateTimeOffset? Deadline(Assessment assignment)
        => AcademicRules.Earlier(assignment.CutoffUtc, assignment.VisibleUntilUtc);

    public static bool Readable(DateTimeOffset now, Assessment assignment)
        => assignment.Active && assignment.PublishedUtc is not null &&
           AcademicRules.Inside(now, assignment.OpensUtc, assignment.VisibleUntilUtc);

    public static bool Configured(Assessment assignment, Term term)
        => assignment.DueUtc is { } due && assignment.CutoffUtc is { } cutoff &&
           due >= term.StartsUtc && due <= term.EndsUtc && cutoff >= due && cutoff <= term.EndsUtc &&
           (assignment.OpensUtc is not { } start || start >= term.StartsUtc && start < due);

    public static AssignmentStatus Status(DateTimeOffset now, Assessment assignment, Term term)
    {
        if (!assignment.Active) return AssignmentStatus.Archived;
        if (assignment.PublishedUtc is null || !Configured(assignment, term)) return AssignmentStatus.Draft;
        if (AcademicRules.IsClosed(term, now) || Deadline(assignment) is { } end && now >= end)
            return AssignmentStatus.Expired;
        if (assignment.OpensUtc is { } start && now < start) return AssignmentStatus.Upcoming;
        return Deadline(assignment) > assignment.DueUtc ? AssignmentStatus.Extended : AssignmentStatus.Open;
    }

    public static bool CanSubmit(DateTimeOffset now, Assessment assignment, Term term)
        => Status(now, assignment, term) is AssignmentStatus.Open or AssignmentStatus.Extended;

    // A deadline is an exclusive bound. Announcing an extension early never penalizes on-time work.
    public static bool IsLate(DateTimeOffset now, Assessment assignment)
        => assignment.DueUtc is { } due && now >= due;

    public static decimal ScoreLimit(DateTimeOffset now, Assessment assignment)
        => assignment.QuizSpec is null && IsLate(now, assignment)
            ? assignment.MaxScore - (assignment.ExtensionPenalty ?? 0m) : assignment.MaxScore;

    public static bool ValidPenalty(decimal penalty, decimal maxScore)
        => penalty >= 0 && penalty <= maxScore && decimal.Round(penalty, 2) == penalty;
}
