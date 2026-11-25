using ReusableAccess.Contracts;

namespace ReusableAccess.Web.Academics;

internal sealed class StudioEditorModel(StudioTemplateDetails source)
{
    internal string Title { get; set; } = source.Template.Title;
    internal string? Description { get; set; } = source.Template.Description;
    internal List<StudioChapterForm> Chapters { get; } = source.Content.Chapters.OrderBy(c => c.Position).Select(c => new StudioChapterForm(c)).ToList();
    internal Dictionary<Guid, FileRow> Files { get; } = source.Files.ToDictionary(f => f.Id);
    internal StudioContent Content() => new(Chapters.Select((c, i) => c.ToContract(i + 1)).ToArray());

    internal static void Move<T>(List<T> list, T value, int offset)
    {
        var from = list.IndexOf(value);
        var to = from + offset;
        if (from >= 0 && to >= 0 && to < list.Count) (list[from], list[to]) = (list[to], list[from]);
    }
}

internal sealed class StudioChapterForm(StudioChapter c)
{
    internal Guid Key { get; } = c.Key;
    internal string Title { get; set; } = c.Title;
    internal int? StartDay { get; set; } = c.StartDay;
    internal int? EndDay { get; set; } = c.EndDay;
    internal List<StudioLessonForm> Lessons { get; } = c.Lessons.OrderBy(l => l.Position).Select(l => new StudioLessonForm(l)).ToList();
    internal List<StudioAssessmentForm> Assessments { get; } = c.Assessments.Select(a => new StudioAssessmentForm(a)).ToList();
    internal StudioChapter ToContract(int position) => new(Key, Title, position, StartDay, EndDay,
        Lessons.Select((l, i) => l.ToContract(i + 1)).ToArray(), Assessments.Select(a => a.ToContract()).ToArray());

    internal static StudioChapterForm Fresh() => new(new(Guid.NewGuid(), "", 1, 0, null, [], []));
}

internal sealed class StudioLessonForm(StudioLesson l)
{
    internal Guid Key { get; } = l.Key;
    internal string Title { get; set; } = l.Title;
    internal string? Summary { get; set; } = l.Summary;
    internal string? Body { get; set; } = l.Body;
    internal int? StartDay { get; set; } = l.StartDay;
    internal int? EndDay { get; set; } = l.EndDay;
    internal List<Guid> Files { get; } = [.. l.MediaIds];
    internal Guid? Cover { get; set; } = l.CoverId;
    internal StudioLesson ToContract(int position) => new(Key, Title, Summary, Body, position, StartDay, EndDay, [.. Files], Cover);
    internal static StudioLessonForm Fresh(int? day) => new(new(Guid.NewGuid(), "", "", "", 1, day, null, [], null));
}

public sealed class StudioAssessmentForm(StudioAssessment a)
{
    public Guid Key { get; } = a.Key;
    public Guid? LessonKey { get; set; } = a.LessonKey;
    public string Title { get; set; } = a.Title;
    public string? Prompt { get; set; } = a.Prompt;
    public string? Rubric { get; set; } = a.Rubric;
    public int MaxAttempts { get; set; } = a.MaxAttempts;
    public decimal MaxScore { get; set; } = a.MaxScore;
    public bool IsQuiz { get; set; } = a.IsQuiz;
    public int? StartDay { get; set; } = a.StartDay;
    public int? DueDay { get; set; } = a.DueDay;
    public int? EndDay { get; set; } = a.EndDay;
    public List<StudioQuestionForm> Questions { get; } = (a.Quiz ?? []).Select(q => new StudioQuestionForm(q)).ToList();
    internal StudioAssessment ToContract() => new(Key, LessonKey, Title, Prompt, Rubric, MaxAttempts, MaxScore, IsQuiz,
        IsQuiz ? Questions.Select(x => new QuizQuestionRow(x.Prompt, x.Choices, x.Correct)).ToArray() : null, StartDay, DueDay, EndDay);
    internal static StudioAssessmentForm Fresh(Guid? lesson, bool quiz)
        => new(new(Guid.NewGuid(), lesson, "", "", "", 1, 20, quiz, null, null, null, null));
}

public sealed class StudioQuestionForm(QuizQuestionRow q)
{
    public string Prompt { get; set; } = q.Prompt;
    public string[] Choices { get; } = [.. q.Choices];
    public int? Correct { get; set; } = q.CorrectIndex;
    public static StudioQuestionForm Fresh() => new(new("", ["", "", "", ""]));
}
