using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Contracts;

public sealed record LearningThumbnail(LearningKind Kind, Guid? CoverId = null);
public sealed record LearningMaterialRow(Guid Key, int Position, LearningKind Kind, string Title, string? Description,
    LearningThumbnail Thumbnail, bool HasFile, string? Alt = null, string? EditableBody = null);
public sealed record LearningLessonRow(Guid Id, int Position, string Title, string? Summary, bool Completed, bool NeedsReview,
    int Count, LearningThumbnail Thumbnail, Guid? FeaturedKey, DateTimeOffset? OpensUtc, DateTimeOffset? ClosesUtc);
public sealed record LearningChapterRow(Guid Id, int Position, string Title, string? Summary, bool Published, bool Pending, bool Removed,
    DateTimeOffset? OpensUtc, DateTimeOffset? ClosesUtc, LearningLessonRow[] Lessons);
public sealed record LearningCurriculum(string Title, string? Summary, int Version, LearningChapterRow[] Chapters,
    int VisibleLessons, int CompletedLessons);
public sealed record LearningIssue(string Code, string Message, Guid? ChapterId = null, Guid? LessonId = null, Guid? MaterialKey = null);
public sealed record LearningTeacherView(string Title, string? Summary, Guid Revision, int Version, bool HasChanges,
    bool CanEdit, bool CanPublish, bool CanUpload, bool CanPreviewDraft, LearningChapterRow[] Chapters, LearningIssue[] Issues);
public sealed record LearningLessonView(LearningLessonRow Lesson, string ChapterTitle, int ChapterIndex, int LessonIndex,
    Guid? PreviousId, Guid? NextId, bool CanComplete, LearningMaterialRow[] Materials);
public sealed record LearningReadMaterial(LearningMaterialRow Material, string? Body, Guid? FileId, string? FileName);
public enum LearningEditKind { Introduce, Chapter, Lesson, Material, Feature, RemoveChapter, RemoveLesson, RemoveMaterial }
public enum LearningCoverAction { Keep, Remove, Replace }
public sealed record LearningEdit(Guid ExpectedRevision, LearningEditKind Kind, Guid? ChapterId = null, Guid? LessonId = null,
    Guid? MaterialKey = null, string? Title = null, string? Summary = null, string? Body = null, string? Alt = null,
    LearningKind? MaterialKind = null, Guid? MediaId = null, LearningCoverAction CoverAction = LearningCoverAction.Keep,
    Guid? CoverId = null, DateTimeOffset? OpensUtc = null, DateTimeOffset? ClosesUtc = null);
public sealed record LearningSaved(Guid Revision, Guid? ChapterId = null, Guid? LessonId = null, Guid? MaterialKey = null);
public sealed record LearningPublish(Guid ExpectedRevision, Guid? ChapterId = null);
public sealed record LearningPublished(Guid Revision, int Version);
public sealed record LearningCompletion(bool Completed);
