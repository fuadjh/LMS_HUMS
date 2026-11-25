using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Contracts;

public sealed record StudioContent(StudioChapter[] Chapters);
public sealed record StudioChapter(Guid Key, string Title, int Position, int? StartDay, int? EndDay,
    StudioLesson[] Lessons, StudioAssessment[] Assessments);
public sealed record StudioLesson(Guid Key, string Title, string? Summary, string? Body, int Position,
    int? StartDay, int? EndDay, Guid[] MediaIds, Guid? CoverId);
public sealed record StudioAssessment(Guid Key, Guid? LessonKey, string Title, string? Prompt, string? Rubric,
    int MaxAttempts, decimal MaxScore, bool IsQuiz, QuizQuestionRow[]? Quiz, int? StartDay, int? DueDay, int? EndDay);

public sealed record StudioTemplateCard(Guid Id, Guid CourseId, string Course, string Title, string? Description, Guid Revision,
    Guid? LatestVersion, int? ChapterCount, int? LessonCount, DateTimeOffset? UpdatedUtc);
public sealed record StudioBank(StudioTemplateCard[] Rows, bool HasMore, bool ScheduleEnabled);
public sealed record StudioVersionHeader(Guid Id, int Number, DateTimeOffset CreatedUtc);
public sealed record StudioVersionList(StudioVersionHeader[] Rows, bool HasMore);
public sealed record StudioTemplateDetails(StudioTemplateCard Template, Guid? VersionId, int Format,
    StudioContent Content, FileRow[] Files);
public sealed record StudioCreateTemplate(Guid CourseId, string Title, string? Description);
public sealed record StudioSaveVersion(Guid ExpectedRevision, string Title, string? Description, StudioContent Content);
public sealed record StudioRevision(Guid ExpectedRevision);
public sealed record StudioVersionSaved(Guid VersionId, int Number, Guid Revision);

public sealed record StudioTarget(Guid Id, Guid TermId, int Year, TermSeason Season, string Course, string Group,
    OfferingAudience Audience, Guid[] CohortIds, int EligibleStudents, int ActiveEnrollments, Guid Revision,
    ScheduleDraftState? DraftState, bool CanPrepare, bool CanPublish, DateTimeOffset? VisibleUtc, DateTimeOffset? PublishedUtc,
    DateOnly SuggestedClassesStart);
public sealed record StudioSource(Guid OfferingId, int Year, TermSeason Season, string Group, DateOnly ClassesStartDate, Guid VersionId);
public sealed record StudioPlan(Guid TemplateVersionId, DateOnly ClassesStartDate, ScheduleMode Mode, Guid? OriginOfferingId, ScheduleOverride[] Overrides);
public sealed record StudioSchedulePreview(Guid OfferingRevision, string Stamp, ScheduleRow[] Rows, ScheduleIssue[] Issues);
public sealed record StudioStoreDraft(Guid? ExpectedDraftRevision, Guid ExpectedOfferingRevision, string? PreviewStamp, StudioPlan Plan);
public sealed record StudioDraftView(Guid Revision, ScheduleDraftState State, StudioPlan? Plan, StudioSchedulePreview? Preview,
    DateTimeOffset UpdatedUtc, DateTimeOffset? AppliedUtc, DateTimeOffset? PublishedUtc);
public sealed record StudioDraftResult(StudioDraftView? Draft);
public sealed record StudioApplyDraft(Guid ExpectedDraftRevision, Guid ExpectedOfferingRevision);
public sealed record StudioReceipt(Guid OfferingId, Guid DraftRevision, Guid OfferingRevision, DateTimeOffset AppliedUtc, DateTimeOffset? PublishedUtc);
public sealed record StudioReadSchedule(Guid OfferingId, string Course, DateOnly? ClassesStartDate,
    DateTimeOffset? VisibleUtc, DateTimeOffset? PublishedUtc, StudioActualRow[] Rows);
public sealed record StudioActualRow(Guid Key, ScheduleKind Kind, string Title, ScheduleTimes Times);
