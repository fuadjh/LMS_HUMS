namespace ReusableAccess.Domain.Academics;

public enum ScheduleMode { Relative = 1, Shift = 2 }
public enum ScheduleDraftState { Draft = 1, Applied = 2, Discarded = 3 }

// Operational times belong to Chapter, LessonSession, Lesson and Assessment. This row is a draft or receipt.
public sealed class OfferingScheduleDraft
{
    public Guid OfferingId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public Guid? OriginOfferingId { get; set; }
    public DateOnly ClassesStartDate { get; set; }
    public ScheduleMode Mode { get; set; }
    public ScheduleDraftState State { get; set; }
    public Guid OfferingRevision { get; set; }
    public Guid? AppliedOfferingRevision { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public DateTimeOffset? AppliedUtc { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }
    public string TermVersion { get; set; } = "";
    public string? OriginVersion { get; set; }
    public string Payload { get; set; } = "";
}

public sealed class TemplateVersionMedia
{
    public Guid TemplateVersionId { get; set; }
    public Guid MediaId { get; set; }
}
