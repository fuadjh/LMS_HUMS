using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Contracts;

public sealed record LmsHome(Guid UserId, string DisplayName, bool PlatformAdmin, bool EducationAdmin, bool Teacher, bool Student,
    string? StudyLabel, string? TeacherLabel, Guid? StudentAvatar, Guid? TeacherAvatar, string? CvUrl, string[] Permissions, bool ScheduleStudioEnabled = false);
public sealed record CalendarWindow(DateTimeOffset StartsUtc, DateTimeOffset EndsUtc, DateTimeOffset EnrollOpensUtc,
    DateTimeOffset EnrollClosesUtc, DateTimeOffset DropOpensUtc, DateTimeOffset DropClosesUtc);
public sealed record CatalogEdit(Guid? Id, Guid? ParentId, Guid? RelatedId, string? Code, string? Title,
    int Year = 0, TermSeason Season = TermSeason.Mehr, CalendarWindow? Calendar = null, bool Active = true, bool Close = false);
public sealed record CatalogRow(Guid Id, string Title, string? Code = null, Guid? ParentId = null,
    Guid? RelatedId = null, int Year = 0, TermSeason Season = TermSeason.Mehr, CalendarWindow? Calendar = null, bool Active = true, bool Closed = false);
public sealed record PeopleRow(Guid UserId, string DisplayName, bool AccountActive, bool ProfileActive,
    Guid? MajorId, Guid? CohortId, string? Label, string? Number, string? Classification, string? CvUrl);
public sealed record ProbeInput(string NationalId, Guid ScopeId);
public sealed record ProbeResult(bool AccountExists, bool AccountActive, bool IsInstructor, bool IsStudent, bool CanEditTarget, string? DisplayName);
public sealed record ProfileEdit(string NationalId, Guid ScopeId, string? FirstName = null, string? LastName = null,
    string? OrganizationCode = null, string? Email = null, string? Mobile = null, string? TemporaryPassword = null,
    string? StudentNumber = null, string? Classification = null, string? CvUrl = null, bool Active = true);
public sealed record StudyRow(Guid Id, Guid CohortId, string Cohort, string Number, DateTimeOffset OpenedUtc, DateTimeOffset? EndedUtc);
public sealed record ImportRow(int Line, string MaskedNationalId, string Status, string? Detail);
public sealed record ScopeGrantEdit(Guid UserId, Guid FacultyId, Guid? MajorId, bool Active = true);
public sealed record ScopeGrantRow(Guid Id, Guid UserId, string DisplayName, Guid FacultyId, Guid? MajorId, string Scope, bool Active);
public sealed record OfferingEdit(Guid? Id, Guid CourseId, Guid TermId, Guid LeadId, Guid? OwnerMajorId, string GroupCode,
    OfferingAudience Audience, Guid[] CohortIds, bool Active = true);
public sealed record CoTeacherEdit(Guid InstructorId, bool CanGrade, bool CanPublish, bool Active = true);
public sealed record CoTeacherRow(Guid InstructorId, string DisplayName, bool CanGrade, bool CanPublish, bool Active);
public sealed record OfferingFilter(string View, Guid? TermId = null, Guid? CohortId = null, bool Mine = false, string? Search = null);
public sealed record OfferingRow(Guid Id, Guid CourseId, Guid TermId, Guid LeadId, Guid? OwnerMajorId,
    string Course, string Group, string Term, string Lead, OfferingAudience Audience, DateTimeOffset? VisibleUtc,
    DateTimeOffset? PublishedUtc, Guid[] CohortIds, bool Active, bool Closed, Guid? EnrollmentId, EnrollmentStatus? Status,
    bool CanEnroll, bool CanWithdraw, DateTimeOffset DropClosesUtc);
public sealed record ScheduleEdit(DateTimeOffset VisibleUtc);
public sealed record SectionEdit(Guid? Id, Guid? ParentId, string Title, int Position = 1, Guid? SessionId = null,
    string? Body = null, string? Rubric = null, DateTimeOffset? OpensUtc = null, DateTimeOffset? DueUtc = null,
    DateTimeOffset? CutoffUtc = null, int MaxAttempts = 1, decimal MaxScore = 20, bool Active = true, QuizQuestionRow[]? Quiz = null);
public sealed record FileRow(Guid Id, string Name, long Bytes, string Extension);
public sealed record LessonRow(Guid Id, string Title, int Position, Guid? SessionId, DateTimeOffset? OpensUtc, string? Body,
    bool CanRead, bool Completed, bool Active, FileRow[] Files, string? Summary = null, DateTimeOffset? ClosesUtc = null,
    Guid? CoverMediaId = null, Guid? TemplateItemKey = null);
public sealed record ChapterRow(Guid Id, string Title, int Position, DateTimeOffset? PublishedUtc, bool Active, LessonRow[] Lessons, AssessmentRow[] Assessments,
    DateTimeOffset? OpensUtc = null, DateTimeOffset? ClosesUtc = null, Guid? TemplateItemKey = null);
public sealed record SessionRow(Guid Id, string Title, DateTimeOffset OpensUtc, bool Active);
public sealed record AssessmentRow(Guid Id, Guid ChapterId, string Title, string? Prompt, string? Rubric,
    DateTimeOffset? DueUtc, DateTimeOffset? CutoffUtc, int MaxAttempts, decimal MaxScore, bool Active, QuizQuestionRow[]? Quiz = null,
    Guid? LessonId = null, DateTimeOffset? OpensUtc = null, bool CanRead = true, bool IsQuiz = false, Guid? TemplateItemKey = null);
public sealed record QuizQuestionRow(string Prompt, string[] Choices, int? CorrectIndex = null);
public sealed record OfferingDetails(OfferingRow Offering, ChapterRow[] Chapters, SessionRow[] Sessions, CoTeacherRow[] CoTeachers,
    bool TeacherEditor, bool CanPublish, bool CanGrade, bool CanRelease, bool Lead, bool ReadOnly);
public sealed record TemplateSave(Guid? TemplateId, string Title);
public sealed record TemplateApply(Guid VersionId);
public sealed record TemplateRow(Guid Id, Guid CourseId, Guid VersionId, int Version, string Title, DateTimeOffset CreatedUtc);
public sealed record RemoveEnrollmentInput(string Reason);
public sealed record EnrollmentHistoryRow(EnrollmentStatus Status, DateTimeOffset OccurredUtc, string? Reason);
public sealed record EnrollmentChange(Guid Id, EnrollmentStatus Status, bool Changed, DateTimeOffset ChangedUtc);
public sealed record RosterRow(Guid EnrollmentId, string Name, string StudyLabel, string Number, EnrollmentStatus Status, bool CanRemove);
public sealed record GradeEdit(decimal Score, string? Feedback, bool Publish = false);
public sealed record GradeRow(int Revision, decimal Score, string Feedback, bool Published, DateTimeOffset CreatedUtc);
public sealed record AttemptInput(string? Text, Guid[]? FileIds, int[]? Answers = null);
public sealed record AttemptRow(Guid Id, Guid EnrollmentId, string? Student, int Attempt, DateTimeOffset SentUtc,
    bool Late, string Text, FileRow[] Files, GradeRow[] Grades);
public sealed record TranscriptRow(Guid EnrollmentId, Guid OfferingId, string Course, string Group, string Term,
    EnrollmentStatus Status, DateTimeOffset ChangedUtc, GradeRow? UnofficialGrade);
public sealed record CalendarEntry(Guid OfferingId, Guid EventId, string Kind, string Title, DateTimeOffset AtUtc);
public sealed record NoticeRow(Guid Id, string Title, string Kind, DateTimeOffset AtUtc, DateTimeOffset CreatedUtc, bool Read, string SmsState);
public sealed record ChatPeer(Guid Id, string Name);
public sealed record ChatPost(Guid? RecipientId, string Text);
public sealed record ChatMessageRow(Guid Id, Guid SenderId, string Sender, Guid? RecipientId, bool Mine, string Text, DateTimeOffset SentUtc);
public sealed record ChatRoom(string Label, Guid MyId, ChatPeer[] Peers, ChatMessageRow[] Messages);
public sealed record UploadStart(Guid TargetId, MediaPurpose Purpose, string Name, long Bytes);
public sealed record UploadTicket(Guid TrackingId, UploadState State, string? Code, long LimitBytes);
public sealed record UploadStatusRow(Guid TrackingId, string Name, long ReportedBytes, long? ReceivedBytes, UploadState State,
    string? Code, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc, Guid? ActorId, Guid TargetId, string? Ip, string? Correlation);
public sealed record LogFilter(DateTimeOffset? FromUtc, DateTimeOffset? ToUtc, string? Name, long? MinBytes,
    long? MaxBytes, UploadState? State, Guid? TrackingId, int Page = 1, Guid? ActorId = null, string? Outcome = null);
public sealed record LogPage<T>(T[] Rows, int Page, bool HasMore);
public sealed record SecurityRow(DateTimeOffset OccurredUtc, Guid? ActorId, string Action, string Outcome,
    Guid? ResourceId, string? Ip, string? Correlation, int? HttpStatus);
public sealed record SmsEdit(string? Provider, string? ApiKey, bool Enabled);
public sealed record SmsView(string[] Providers, string? Selected, bool Configured);
