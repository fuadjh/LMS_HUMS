namespace ReusableAccess.Domain.Academics;

// مدل آموزشی؛ قواعد وضعیت در AcademicRules و موارد کاربرد قرار دارند.
public enum TermSeason { Mehr = 1, Bahman = 2, Summer = 3 }
public enum OfferingAudience { General = 1, Common = 2, Specialized = 3 }
public enum EnrollmentStatus { Active = 1, WithdrawnByStudent = 2, RemovedByAdmin = 3 }
public enum MediaPurpose { InstructorAvatar = 1, StudentAvatar = 2, LessonMaterial = 3, Submission = 4, RosterExcel = 5, TemplateMaterial = 6, TemplateCover = 7, CurriculumFile = 8, CurriculumCover = 9 }
public enum UploadState { Pending = 1, Receiving = 2, Stored = 3, Rejected = 4, Interrupted = 5, Incomplete = 6 }

public sealed class Faculty
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Active { get; set; }
}

public sealed class Major
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FacultyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Active { get; set; }
}

public sealed class Degree
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Active { get; set; }
}

public sealed class AcademicProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MajorId { get; set; }
    public Guid DegreeId { get; set; }
    public bool Active { get; set; }
}

public sealed class Cohort
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProgramId { get; set; }
    public int Year { get; set; }
    public TermSeason Season { get; set; }
    public bool Active { get; set; }
}

public sealed class Term
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Year { get; set; }
    public TermSeason Season { get; set; }
    public DateTimeOffset StartsUtc { get; set; }
    public DateTimeOffset EndsUtc { get; set; }
    public DateTimeOffset EnrollOpensUtc { get; set; }
    public DateTimeOffset EnrollClosesUtc { get; set; }
    public DateTimeOffset DropOpensUtc { get; set; }
    public DateTimeOffset DropClosesUtc { get; set; }
    public DateTimeOffset? ClosedUtc { get; set; }
    public bool Active { get; set; }
}

public sealed class InstructorProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MajorId { get; set; }
    public string Classification { get; set; } = string.Empty;
    public string? CvUrl { get; set; }
    public Guid? AvatarId { get; set; }
    public bool Active { get; set; }
}

public sealed class StudentProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AvatarId { get; set; }
    public bool Active { get; set; }
}

public sealed class StudyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid CohortId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTimeOffset OpenedUtc { get; set; }
    public DateTimeOffset? EndedUtc { get; set; }
}

public sealed class AcademicGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid FacultyId { get; set; }
    public Guid? MajorId { get; set; }
    public bool Active { get; set; }
}

public sealed class CatalogCourse
{
    // FacultyId is retained for historical and common offerings; new courses require MajorId.
    public Guid? MajorId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FacultyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Active { get; set; }
}

public sealed class Offering
{
    public string? DraftIntroduction { get; set; }
    public string? PublishedIntroduction { get; set; }
    public int CurriculumVersion { get; set; }
    public Guid? PublishedCurriculumToken { get; set; }
    public Guid? PublishedCurriculumScope { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid TermId { get; set; }
    public Guid? OwnerMajorId { get; set; }
    public Guid LeadId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public OfferingAudience Audience { get; set; }
    public DateTimeOffset? VisibleUtc { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }
    public bool Active { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}

public sealed class OfferingCohort
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferingId { get; set; }
    public Guid CohortId { get; set; }
}

public sealed class CoTeacher
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferingId { get; set; }
    public Guid InstructorId { get; set; }
    public bool CanGrade { get; set; }
    public bool CanPublish { get; set; }
    public bool Active { get; set; }
}

public sealed class CourseTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool Active { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}

public sealed class TemplateVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public int Number { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public string Snapshot { get; set; } = string.Empty;
    public int? ChapterCount { get; set; }
    public int? LessonCount { get; set; }
}

public sealed class Chapter
{
    public string? Summary { get; set; }
    public bool WorkspaceManaged { get; set; }
    public Guid? PendingBaseId { get; set; }
    public Guid? StableChapterId { get; set; }
    public bool Retired { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }
    public bool Active { get; set; }
    public Guid? TemplateItemKey { get; set; }
    public DateTimeOffset? VisibleFromUtc { get; set; }
    public DateTimeOffset? VisibleUntilUtc { get; set; }
}

// Kept as historical data. No learning rule reads this table.
public sealed class LessonSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset OpensUtc { get; set; }
    public bool Active { get; set; }
}

public sealed class Lesson
{
    public Guid? StableLessonId { get; set; }
    public Guid? FeaturedMaterialKey { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool Active { get; set; }
    public Guid? TemplateItemKey { get; set; }
    public DateTimeOffset? VisibleFromUtc { get; set; }
    public DateTimeOffset? VisibleUntilUtc { get; set; }
    public string? Summary { get; set; }
    public Guid? CoverMediaId { get; set; }
    public bool NeedsAvailabilityReview { get; set; }
}

public sealed class LessonFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Guid MediaId { get; set; }
    public int? Position { get; set; }
}

public sealed class Assessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Rubric { get; set; } = string.Empty;
    public DateTimeOffset? DueUtc { get; set; }
    public DateTimeOffset? CutoffUtc { get; set; }
    public int MaxAttempts { get; set; }
    public decimal MaxScore { get; set; }
    public string? QuizSpec { get; set; }
    public bool Active { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }
    // Old assessments retain a copied, never live, visibility boundary where one existed.
    public DateTimeOffset? VisibleUntilUtc { get; set; }
    public decimal? ExtensionPenalty { get; set; }
    public Guid? TemplateVersionId { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public DateTimeOffset? OpensUtc { get; set; }
}

public sealed class Enrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferingId { get; set; }
    public Guid StudyRecordId { get; set; }
    public EnrollmentStatus Status { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ChangedUtc { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}

public sealed class EnrollmentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnrollmentId { get; set; }
    public Guid ActorId { get; set; }
    public EnrollmentStatus Status { get; set; }
    public DateTimeOffset OccurredUtc { get; set; }
    public string? Reason { get; set; }
}

public sealed class LessonProgress
{
    public DateTimeOffset? ClearedUtc { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnrollmentId { get; set; }
    public Guid LessonId { get; set; }
    public DateTimeOffset CompletedUtc { get; set; }
}

public sealed class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssessmentId { get; set; }
    public Guid EnrollmentId { get; set; }
    public int Attempt { get; set; }
    public DateTimeOffset SentUtc { get; set; }
    public bool IsLate { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? QuizAnswers { get; set; }
    // Null on legacy attempts; their grades and original grading scale are never backfilled.
    public decimal? ScoreLimit { get; set; }
    public Guid? RequestId { get; set; }
    public string? RequestHash { get; set; }
}

public sealed class SubmissionFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid MediaId { get; set; }
}

public sealed class AssessmentGrade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid? GraderId { get; set; }
    public int Revision { get; set; } 
    public decimal Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public bool Published { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class FinalGrade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnrollmentId { get; set; }
    public Guid GraderId { get; set; }
    public int Revision { get; set; } 
    public decimal Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public bool Published { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class CohortMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CohortId { get; set; }
    public Guid SenderId { get; set; }
    public Guid? RecipientId { get; set; }
    public DateTimeOffset SentUtc { get; set; }
    public string Body { get; set; } = string.Empty;
}

public sealed class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid OfferingId { get; set; }
    public Guid EventId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset DueUtc { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ReadUtc { get; set; }
    public string SmsState { get; set; } = string.Empty;
}

public sealed class SmsSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = string.Empty;
    public string ProtectedKey { get; set; } = string.Empty;
    public DateTimeOffset UpdatedUtc { get; set; }
}

public sealed class Media
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid TargetId { get; set; }
    public MediaPurpose Purpose { get; set; }
    public long Bytes { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class UploadAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorId { get; set; }
    public Guid TargetId { get; set; }
    public MediaPurpose Purpose { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long ReportedBytes { get; set; }
    public long? ReceivedBytes { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public UploadState State { get; set; }
    public string? Code { get; set; }
    public string? Ip { get; set; }
    public string? Correlation { get; set; }
}

public sealed class OperationalEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public DateTimeOffset OccurredUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public Guid? ResourceId { get; set; }
    public string? Ip { get; set; }
    public string? Correlation { get; set; }
    public int? HttpStatus { get; set; }
}
