using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class StudioMaterializer(LmsData data)
{
    internal void Add(Guid offeringId, StudioContent content, IReadOnlyDictionary<Guid, ScheduleTimes>? times, DateTimeOffset? publishAt)
    {
        // The caller owns the transaction. No nested commands, implicit enrollment, or copying of student activity.
        foreach (var source in content.Chapters.OrderBy(c => c.Position))
        {
            var window = times?.GetValueOrDefault(source.Key);
            var chapter = new Chapter { OfferingId = offeringId, Title = source.Title, Summary = source.Summary, Position = source.Position,
                Active = true, TemplateItemKey = source.Key, VisibleFromUtc = window?.Start, VisibleUntilUtc = window?.End,
                PublishedUtc = publishAt };
            data.Db.Add(chapter);
            foreach (var item in source.Lessons.OrderBy(l => l.Position))
            {
                var slot = times?.GetValueOrDefault(item.Key);
                var lesson = new Lesson { ChapterId = chapter.Id, TemplateItemKey = item.Key,
                    Title = item.Title, Body = item.Body ?? "", Summary = item.Summary, Position = item.Position,
                    VisibleFromUtc = slot?.Start, VisibleUntilUtc = slot?.End, CoverMediaId = item.CoverId, FeaturedMaterialKey = item.FeaturedKey, Active = true };
                data.Db.Add(lesson);
                data.Db.AddRange(item.MediaIds.Distinct().Select((mediaId, index) => new LessonFile
                    { LessonId = lesson.Id, MediaId = mediaId, Position = index + 1 }));
                data.Db.AddRange((item.Materials ?? []).Select((r, index) => new LearningMaterial { LessonId = lesson.Id,
                    Key = r.Key, Kind = r.Kind, Title = r.Title, Description = r.Description, Body = r.Body, Alt = r.Alt,
                    MediaId = r.MediaId, CoverId = r.CoverId, Position = item.MediaIds.Length + index + 1 }));
            }
        }
    }
}
