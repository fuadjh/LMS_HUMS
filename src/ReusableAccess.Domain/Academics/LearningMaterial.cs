namespace ReusableAccess.Domain.Academics;

public enum LearningKind { Video = 1, Pdf = 2, Audio = 3, Image = 4, Text = 5, LegacyFile = 6 }

// Key is the URL identity; Id identifies the physical edition belonging to LessonId.
public sealed class LearningMaterial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid Key { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public int Position { get; set; }
    public LearningKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Body { get; set; }
    public string? Alt { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? CoverId { get; set; }
    public bool Removed { get; set; }
}

public static class LearningKinds
{
    public static LearningKind FromExtension(string? extension) => extension switch
    {
        ".mp4" or ".webm" => LearningKind.Video, ".pdf" => LearningKind.Pdf,
        ".mp3" or ".wav" or ".ogg" => LearningKind.Audio,
        ".png" or ".jpeg" or ".jpg" or ".webp" or ".gif" => LearningKind.Image,
        _ => LearningKind.LegacyFile
    };
    public static bool IsCover(string? extension) => extension is ".png" or ".jpeg" or ".jpg" or ".webp";
}
