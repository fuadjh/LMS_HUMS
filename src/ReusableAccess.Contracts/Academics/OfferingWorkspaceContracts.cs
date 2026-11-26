using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Contracts;

public sealed record AssignmentSummary(Guid Id, string Title, bool IsQuiz, AssignmentStatus Status, DateTimeOffset? OpensUtc,
    DateTimeOffset? DueUtc, DateTimeOffset? DeadlineUtc, decimal MaxScore, int MaxAttempts);
public sealed record AssignmentQueueRow(Guid OfferingId, Guid AssessmentId, string Course, string Group, string Title, int Pending);
public sealed record AssignmentPanel(AssignmentSummary[] Rows, bool CanAuthor);
public sealed record AssessmentRow(AssignmentSummary Summary, string? Prompt, string? Rubric, QuizQuestionRow[]? Quiz,
    bool CanRead, bool CanSubmit, bool CanEdit, bool CanPublish, bool CanExtend, Guid Revision,
    decimal? CurrentScoreLimit, decimal? ExtensionPenalty, AssignmentExtensionRow[] Extensions, bool CanReadWork)
{
    public Guid Id => Summary.Id;
    public string Title => Summary.Title;
    public decimal MaxScore => Summary.MaxScore;
    public int MaxAttempts => Summary.MaxAttempts;
}
public sealed record AssignmentExtensionRow(DateTimeOffset DeadlineUtc, decimal Penalty, DateTimeOffset RecordedUtc);
public sealed record AssessmentEdit(Guid? Id, Guid? ExpectedRevision, string? Title, string? Prompt, string? Rubric,
    DateTimeOffset? OpensUtc, DateTimeOffset DueUtc, int MaxAttempts = 1, decimal MaxScore = 20,
    QuizQuestionRow[]? Quiz = null, Guid? TemplateVersionId = null, bool Active = true);
public sealed record PublishAssessmentInput(Guid Revision);
public sealed record ExtendAssignmentInput(Guid Revision, DateTimeOffset DeadlineUtc, decimal Penalty);
public sealed record AssignmentTemplateInput(Guid? PreviousVersionId, string Title, string Prompt, string? Rubric,
    int MaxAttempts = 1, decimal MaxScore = 20);
public sealed record AssignmentTemplateRow(Guid Id, Guid FamilyId, int Number, string Title, string Prompt, string Rubric,
    int MaxAttempts, decimal MaxScore, DateTimeOffset CreatedUtc);
public sealed record CourseDiscussionPost(string Text);
public sealed record CourseDiscussionRow(Guid Id, string Sender, bool Mine, bool ByInstructor, string Text, DateTimeOffset SentUtc);
public sealed record CourseDiscussionPage(CourseDiscussionRow[] Rows, bool CanPost, bool HasMore);
