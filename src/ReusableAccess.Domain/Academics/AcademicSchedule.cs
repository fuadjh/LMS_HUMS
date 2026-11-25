namespace ReusableAccess.Domain.Academics;

public enum ScheduleKind { Visibility, Chapter, Lesson, Assessment }
public enum ScheduleField { Start, End, Due }
public enum ScheduleDateSource { Missing, Policy, Relative, Previous, Manual, Existing }
public enum ScheduleOverrideMode { Set = 1, Clear = 2 }

public sealed record ScheduleItem(Guid Key, ScheduleKind Kind, string Title, Guid? ChapterKey = null, Guid? LessonKey = null,
    int? StartDay = null, int? EndDay = null, int? DueDay = null);
public sealed record ScheduleOverride(Guid Key, ScheduleField Field, ScheduleOverrideMode Mode, DateTimeOffset? AtUtc);
public sealed record ScheduleTimes(DateTimeOffset? Start, DateTimeOffset? End, DateTimeOffset? Due);
public sealed record ScheduleDate(DateTimeOffset? SuggestedUtc, DateTimeOffset? AtUtc, ScheduleDateSource Source);
public sealed record ScheduleRow(ScheduleItem Item, ScheduleDate Start, ScheduleDate? End, ScheduleDate? Due)
{
    public ScheduleTimes Times => new(Start.AtUtc, End?.AtUtc, Due?.AtUtc);
}
public sealed record ScheduleIssue(Guid? Key, ScheduleField? Field, string Code, string Message);
public sealed record ScheduleCalculation(ScheduleRow[] Rows, ScheduleIssue[] Issues);
public sealed record ScheduleOriginRow(ScheduleKind Kind, ScheduleTimes Times);
public sealed record ScheduleOrigin(DateOnly ClassesStart, IReadOnlyDictionary<Guid, ScheduleOriginRow> Rows);

/// <summary>Deterministic Tehran-local day arithmetic; projections and persistence stay outside the domain.</summary>
public static class AcademicSchedule
{
    public const int MaxOffsetDays = 730;
    public const int MaxItems = 300;
    public static readonly TimeOnly ClassStartTime = new(8, 0);

    public static bool SupportedInstant(DateTimeOffset value)
        => value.Year is >= 1900 and <= 2250 && value.Ticks % TimeSpan.TicksPerMinute == 0;

    public static ScheduleCalculation Calculate(Term term, DateOnly start, IReadOnlyList<ScheduleItem> items,
        IReadOnlyList<ScheduleOverride> edits, ScheduleOrigin? origin, DateTimeOffset? existingVisibility = null)
    {
        var issues = new List<ScheduleIssue>();
        if (items.Count > MaxItems || items.Any(i => i.Key == Guid.Empty) || items.Select(i => i.Key).Distinct().Count() != items.Count)
            throw new ArgumentException("شناسه یا تعداد اقلام برنامه معتبر نیست.", nameof(items));
        var openingDay = DateOnly.FromDateTime(JalaliTime.InTehran(term.StartsUtc));
        var lastDay = DateOnly.FromDateTime(JalaliTime.InTehran(term.EndsUtc.AddTicks(-1)));
        if (start < openingDay || start > lastDay)
            issues.Add(new(null, null, "ClassesStartOutsideTerm", "روز آغاز کلاس باید در نیم‌سال مقصد باشد."));

        ScheduleItem[] all = [new(Guid.Empty, ScheduleKind.Visibility, "نمایش ارائه برای دانشجو"), .. items];
        var known = all.ToDictionary(x => x.Key);
        var overrides = new Dictionary<(Guid, ScheduleField), ScheduleOverride>();
        foreach (var edit in edits)
        {
            if (edit is null || !known.TryGetValue(edit.Key, out var item) || !HasField(item.Kind, edit.Field) ||
                !Enum.IsDefined(edit.Mode) || (edit.Mode == ScheduleOverrideMode.Set && (edit.AtUtc is null || !SupportedInstant(edit.AtUtc.Value))) ||
                (edit.Mode == ScheduleOverrideMode.Clear && (edit.Field != ScheduleField.End || edit.AtUtc is not null)) ||
                !overrides.TryAdd((edit.Key, edit.Field), edit))
                throw new ArgumentException("اصلاح دستی نامعتبر، تکراری یا متعلق به نسخهٔ دیگری است.", nameof(edits));
        }

        ScheduleDate Pick(ScheduleItem item, ScheduleField field, int? offset)
        {
            DateTimeOffset? guess = null;
            var source = ScheduleDateSource.Missing;
            try
            {
                if (item.Kind == ScheduleKind.Visibility)
                {
                    guess = existingVisibility ?? term.EnrollOpensUtc;
                    source = existingVisibility.HasValue ? ScheduleDateSource.Existing : ScheduleDateSource.Policy;
                }
                else if (origin is not null && origin.Rows.TryGetValue(item.Key, out var previous) && previous.Kind == item.Kind &&
                         (Read(previous.Times, field) is not null || field == ScheduleField.End))
                {
                    var shift = start.DayNumber - origin.ClassesStart.DayNumber;
                    var old = Read(previous.Times, field);
                    guess = old is { } value ? JalaliTime.FromDate(JalaliTime.InTehran(value).AddDays(shift)) : null;
                    source = ScheduleDateSource.Previous;
                }
                else if (offset is { } day && day is >= 0 and <= MaxOffsetDays)
                {
                    var date = start.AddDays(day);
                    guess = field == ScheduleField.Start ? JalaliTime.FromDate(date.ToDateTime(ClassStartTime))
                        : JalaliTime.FromDate(date.AddDays(1).ToDateTime(TimeOnly.MinValue));
                    source = ScheduleDateSource.Relative;
                }
            }
            catch (ArgumentException)
            {
                if (!overrides.ContainsKey((item.Key, field)))
                    issues.Add(new(item.Key, field, "LocalTimeInvalid", "زمان پیشنهادی در تقویم/منطقهٔ تهران معتبر نیست؛ زمان را دستی انتخاب کنید."));
            }

            return overrides.TryGetValue((item.Key, field), out var edit)
                ? new(guess, edit.AtUtc?.ToUniversalTime(), ScheduleDateSource.Manual)
                : new(guess, guess, source);
        }

        var rows = all.Select(item => new ScheduleRow(item, Pick(item, ScheduleField.Start, item.StartDay),
            item.Kind == ScheduleKind.Visibility ? null : Pick(item, ScheduleField.End, item.EndDay),
            item.Kind == ScheduleKind.Assessment ? Pick(item, ScheduleField.Due, item.DueDay) : null)).ToArray();
        var map = rows.ToDictionary(x => x.Item.Key, x => x.Times);
        foreach (var row in rows)
        {
            var item = row.Item;
            var (open, end, due) = row.Times;
            void Add(ScheduleField field, string code, string message) => issues.Add(new(item.Key, field, code, message));
            if (open is null) Add(ScheduleField.Start, "RequiredStart", "تاریخ شروع را وارد کنید.");
            if (item.Kind == ScheduleKind.Visibility)
            {
                if (open >= term.EnrollClosesUtc) Add(ScheduleField.Start, "VisibilityAfterEnrollment", "شروع نمایش باید قبل از پایان اخذ درس باشد.");
                continue; // Visibility is not the start of teaching; it may precede the term.
            }

            if (open < term.StartsUtc || open >= term.EndsUtc) Add(ScheduleField.Start, "OutsideTerm", "شروع باید داخل نیم‌سال باشد.");
            if (end > term.EndsUtc || end <= term.StartsUtc) Add(ScheduleField.End, "OutsideTerm", "مرز پایان باید حداکثر پایان نیم‌سال باشد.");
            if (end <= open) Add(ScheduleField.End, "BeforeStart", "پایان باید بعد از شروع باشد.");
            if (item.ChapterKey is { } chapter && map.TryGetValue(chapter, out var parent))
            {
                if (open < parent.Start || open >= parent.End) Add(ScheduleField.Start, "ChapterWindow", "شروع باید در بازهٔ فصل باشد.");
                if (end > parent.End) Add(ScheduleField.End, "ChapterWindow", "پایان نمی‌تواند از پایان فصل بگذرد.");
            }

            if (item.Kind != ScheduleKind.Assessment) continue;
            if (due is null) Add(ScheduleField.Due, "RequiredDue", "موعد عادی را وارد کنید.");
            if (due <= open || due > term.EndsUtc || due <= term.StartsUtc)
                Add(ScheduleField.Due, "InvalidDue", "موعد عادی باید بعد از شروع و حداکثر در مرز پایان نیم‌سال باشد.");
            if (end < due) Add(ScheduleField.End, "CutoffBeforeDue", "مهلت نهایی نمی‌تواند قبل از موعد عادی باشد.");
            if (item.ChapterKey is { } owner && map.TryGetValue(owner, out var chapterTimes) && due > chapterTimes.End)
                Add(ScheduleField.Due, "ChapterWindow", "موعد عادی نمی‌تواند از پایان فصل بگذرد.");
            if (item.LessonKey is { } lesson && map.TryGetValue(lesson, out var lessonTimes) && open < lessonTimes.Start)
                Add(ScheduleField.Start, "BeforeLesson", "ارزیابی نمی‌تواند قبل از باز شدن درسک شروع شود.");
        }

        return new(rows, issues.ToArray());
    }

    private static bool HasField(ScheduleKind kind, ScheduleField field) => field switch
    {
        ScheduleField.Start => true,
        ScheduleField.End => kind != ScheduleKind.Visibility,
        ScheduleField.Due => kind == ScheduleKind.Assessment,
        _ => false
    };

    private static DateTimeOffset? Read(ScheduleTimes times, ScheduleField field) => field switch
    {
        ScheduleField.Start => times.Start,
        ScheduleField.End => times.End,
        ScheduleField.Due => times.Due,
        _ => null
    };
}
