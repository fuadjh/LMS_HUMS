using System.Text.Json;
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
            var chapter = new Chapter { OfferingId = offeringId, Title = source.Title, Position = source.Position,
                Active = true, TemplateItemKey = source.Key, VisibleFromUtc = window?.Start, VisibleUntilUtc = window?.End,
                PublishedUtc = publishAt };
            data.Db.Add(chapter);
            var lessons = new Dictionary<Guid, Guid>();
            foreach (var item in source.Lessons.OrderBy(l => l.Position))
            {
                var slot = times?.GetValueOrDefault(item.Key);
                LessonSession? session = null;
                if (slot?.Start is { } when)
                {
                    session = new() { OfferingId = offeringId, Title = item.Title, OpensUtc = when, Active = true };
                    data.Db.Add(session);
                }
                var lesson = new Lesson { ChapterId = chapter.Id, SessionId = session?.Id, TemplateItemKey = item.Key,
                    Title = item.Title, Body = item.Body ?? "", Summary = item.Summary, Position = item.Position,
                    VisibleFromUtc = slot?.Start, VisibleUntilUtc = slot?.End, CoverMediaId = item.CoverId, Active = true };
                data.Db.Add(lesson);
                lessons.Add(item.Key, lesson.Id);
                data.Db.AddRange(item.MediaIds.Distinct().Select((mediaId, index) => new LessonFile
                    { LessonId = lesson.Id, MediaId = mediaId, Position = index + 1 }));
            }
            foreach (var a in source.Assessments)
            {
                var slot = times?.GetValueOrDefault(a.Key);
                data.Db.Add(new Assessment { ChapterId = chapter.Id, TemplateItemKey = a.Key,
                    LessonId = a.LessonKey is { } key ? lessons[key] : null,
                    Title = a.Title, Prompt = a.Prompt ?? "", Rubric = a.Rubric ?? "", MaxAttempts = a.MaxAttempts,
                    MaxScore = a.MaxScore, QuizSpec = a.Quiz is { Length: > 0 } ? JsonSerializer.Serialize(a.Quiz) : null,
                    OpensUtc = slot?.Start, DueUtc = slot?.Due, CutoffUtc = slot?.End ?? slot?.Due, Active = true });
            }
        }
    }
}
