using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal static class StudioDocuments
{
    private const int MaxSnapshotLength = 4_000_000;
    private sealed record Envelope(int Format, StudioChapter[] Chapters, string? Title, string? Description);
    private sealed record V1Lesson(string Title, string Body, int Position, Guid[] MediaIds);
    private sealed record V1Assessment(string Title, string Prompt, string Rubric, int MaxAttempts, decimal MaxScore, QuizQuestionRow[]? Quiz);
    private sealed record V1Chapter(string Title, int Position, V1Lesson[] Lessons, V1Assessment[] Assessments);
    private sealed record V1Document(int Format, V1Chapter[] Chapters);

    internal static string Write(StudioContent content, string title, string? description)
    {
        var json = JsonSerializer.Serialize(new Envelope(2, content.Chapters, title, description));
        if (json.Length > MaxSnapshotLength) throw LmsError.Invalid("طول محتوای قالب بیش از حد مجاز است.");
        return json;
    }

    internal static (int Format, StudioContent Content, string? Title, string? Description) Read(TemplateVersion row)
    {
        try
        {
            if (row.Snapshot.Length > MaxSnapshotLength) throw LmsError.Conflict("حجم نسخهٔ ذخیره‌شده نامعتبر است.");
            using var json = JsonDocument.Parse(row.Snapshot);
            var format = json.RootElement.GetProperty("Format").GetInt32();
            if (format == 2)
            {
                var v2 = json.RootElement.Deserialize<Envelope>();
                if (v2?.Chapters is not { } chapters) throw new JsonException();
                return (2, new(chapters), v2.Title, v2.Description);
            }

            if (format == 1)
            {
                var v1 = json.RootElement.Deserialize<V1Document>();
                if (v1?.Chapters is not { } legacy) throw new JsonException();
                // V1 had no item identifiers or dates. Stable IDs within the V1 version support opening it twice;
                // a saved V2 retains those IDs. No inferred dates or assessment/lesson links.
                var converted = legacy.Select((c, i) => new StudioChapter(LegacyKey(row.Id, $"chapter/{i}"), c.Title, c.Position, null, null,
                    c.Lessons.Select((l, j) => new StudioLesson(LegacyKey(row.Id, $"chapter/{i}/lesson/{j}"), l.Title, null, l.Body,
                        l.Position, null, null, l.MediaIds, null)).ToArray(),
                    c.Assessments.Select((a, j) => new StudioAssessment(LegacyKey(row.Id, $"chapter/{i}/assessment/{j}"), null, a.Title,
                        a.Prompt, a.Rubric, a.MaxAttempts, a.MaxScore, a.Quiz is { Length: > 0 }, a.Quiz, null, null, null)).ToArray())).ToArray();
                return (1, new(converted), null, null);
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or NullReferenceException)
        {
            throw LmsError.Conflict("ساختار نسخهٔ قالب قابل خواندن نیست.");
        }
        throw LmsError.Conflict("شمارهٔ قالب نسخه پشتیبانی نمی‌شود.");
    }

    // V1/V2 documents are immutable; old assessment fields can be read but never scheduled or copied as content.
    internal static ScheduleItem[] ToSchedule(StudioContent content) => content.Chapters.OrderBy(c => c.Position).SelectMany(c =>
        new[] { new ScheduleItem(c.Key, ScheduleKind.Chapter, c.Title, StartDay: c.StartDay, EndDay: c.EndDay) }
            .Concat(c.Lessons.OrderBy(l => l.Position).Select(l => new ScheduleItem(l.Key, ScheduleKind.Lesson, l.Title, c.Key,
                StartDay: l.StartDay, EndDay: l.EndDay)))).ToArray();

    internal static Guid[] MediaIds(StudioContent content) => content.Chapters.SelectMany(c => c.Lessons)
        .SelectMany(l => l.MediaIds.Concat(l.CoverId is { } cover ? new[] { cover } : Array.Empty<Guid>())
            .Concat((l.Materials ?? []).SelectMany(m => new[] { m.MediaId, m.CoverId }).OfType<Guid>())).Distinct().ToArray();

    internal static StudioContent Validate(StudioContent? content)
    {
        if (content?.Chapters is not { Length: > 0 and <= 60 } chapters || chapters.Any(x => x is null))
            throw LmsError.Invalid("بین یک تا ۶۰ فصل معتبر وارد کنید.");
        var keys = new HashSet<Guid>();
        var chapterPositions = new HashSet<int>();
        var count = 0;

        var clean = chapters.Select(c =>
        {
            CheckItem(c.Key);
            Position(c.Position, chapterPositions);
            Window(c.StartDay, c.EndDay);
            if (c.Lessons is not { Length: > 0 and <= 100 } || c.Lessons.Any(x => x is null))
                throw LmsError.Invalid("هر فصل به حداقل یک درسک معتبر نیاز دارد.");
            var lessonPositions = new HashSet<int>();
            var lessons = c.Lessons.Select(l =>
            {
                CheckItem(l.Key);
                Position(l.Position, lessonPositions);
                Window(l.StartDay, l.EndDay);
                ParentWindow(c, l.StartDay, l.EndDay);
                var body = LmsError.Text(l.Body, 32000, true);
                if (l.MediaIds is not { Length: <= 16 } || l.MediaIds.Any(m => m == Guid.Empty) || l.MediaIds.Distinct().Count() != l.MediaIds.Length ||
                    (l.CoverId is { } cover && (cover == Guid.Empty || l.MediaIds.Contains(cover))))
                    throw LmsError.Invalid("شناسهٔ فایل/کاور معتبر نیست یا بیش از ۱۶ پیوست وارد شده است.");
                var typed = l.Materials ?? [];
                if (typed.Length + l.MediaIds.Length > 16 || typed.Any(m => m is null || m.Key == Guid.Empty || !Enum.IsDefined(m.Kind)) ||
                    typed.Select(m => m.Key).Distinct().Count() != typed.Length ||
                    l.FeaturedKey is { } featured && !typed.Any(m => m.Key == featured))
                    throw LmsError.Invalid("منابع نوع‌دار یا تصویر نمایندهٔ درس معتبر نیستند.");
                var materials = typed.Select(m =>
                {
                    if (m.MediaId == Guid.Empty || m.CoverId == Guid.Empty || (m.Kind == LearningKind.Text) == m.MediaId.HasValue)
                        throw LmsError.Invalid("فایل اصلی برای نوع محتوا معتبر نیست.");
                    return m with { Title = LmsError.Text(m.Title, 180), Description = LmsError.Text(m.Description, 450, true),
                        Body = m.Kind == LearningKind.Text ? LmsError.Text(m.Body, 32000) : null, Alt = LmsError.Text(m.Alt, 250, true) };
                }).ToArray();
                if (body == "" && l.MediaIds.Length == 0 && materials.Length == 0) throw LmsError.Invalid("درسک باید متن یا پیوست آموزشی داشته باشد.");
                return l with { Title = LmsError.Text(l.Title, 180), Summary = LmsError.Text(l.Summary, 320), Body = body, Materials = materials };
            }).OrderBy(x => x.Position).ToArray();
            if (c.Assessments is { Length: > 0 })
                throw LmsError.Invalid("نسخهٔ جدید محتوا ارزیابی ندارد؛ از بانک مستقل تکلیف استفاده کنید.");
            return c with { Title = LmsError.Text(c.Title, 180), Summary = LmsError.Text(c.Summary, 320, true), Lessons = lessons, Assessments = [] };
        }).OrderBy(x => x.Position).ToArray();
        return new(clean);

        void CheckItem(Guid key)
        {
            if (key == Guid.Empty || !keys.Add(key) || ++count > AcademicSchedule.MaxItems)
                throw LmsError.Invalid("شناسهٔ قلم تکراری یا شمار فصل و درسک بیش از حد مجاز است.");
        }
    }

    private static Guid LegacyKey(Guid version, string path) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{version:D}/{path}")).AsSpan(0, 16));

    private static void Position(int value, HashSet<int> used)
    {
        if (value is < 1 or > 1000 || !used.Add(value)) throw LmsError.Invalid("ترتیب هر قلم در سطح خودش باید یکتا و بین ۱ تا ۱۰۰۰ باشد.");
    }
    private static void Day(int? value)
    {
        if (value is < 0 or > AcademicSchedule.MaxOffsetDays) throw LmsError.Invalid("روز نسبی باید بین صفر و ۷۳۰ باشد.");
    }
    private static void Window(int? start, int? end)
    {
        Day(start);
        Day(end);
        if (end < start) throw LmsError.Invalid("روز پایان نباید پیش از روز شروع باشد.");
    }
    private static void ParentWindow(StudioChapter chapter, int? start, int? end)
    {
        if (start < chapter.StartDay || start > chapter.EndDay || end > chapter.EndDay)
            throw LmsError.Invalid("روزهای درسک باید در بازهٔ فصل باشند.");
    }
}
