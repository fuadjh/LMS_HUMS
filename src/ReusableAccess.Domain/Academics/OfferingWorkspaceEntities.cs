namespace ReusableAccess.Domain.Academics;

public sealed class AssignmentTemplateVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid FamilyId { get; init; }
    public Guid OwnerId { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = "";
    public string Prompt { get; init; } = "";
    public string Rubric { get; init; } = "";
    public int MaxAttempts { get; init; }
    public decimal MaxScore { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
}

public sealed class AssignmentExtension
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AssessmentId { get; init; }
    public Guid InstructorId { get; init; }
    public DateTimeOffset PreviousDeadlineUtc { get; init; }
    public DateTimeOffset DeadlineUtc { get; init; }
    public decimal Penalty { get; init; }
    public DateTimeOffset RecordedUtc { get; init; }
}

// One immutable event per accepted submission. It is not an upcoming SMS reminder.
public sealed class AssignmentCalendarReceipt
{
    public Guid SubmissionId { get; init; }
    public Guid InstructorId { get; init; }
    public string Title { get; init; } = "";
    public DateTimeOffset ReceivedUtc { get; init; }
}

public sealed class CourseDiscussionMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OfferingId { get; init; }
    public Guid SenderId { get; init; }
    public bool ByInstructor { get; init; }
    public string Text { get; init; } = "";
    public DateTimeOffset SentUtc { get; init; }
}
