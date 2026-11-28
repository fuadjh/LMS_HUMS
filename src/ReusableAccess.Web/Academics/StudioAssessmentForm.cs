using System;
using System.Collections.Generic;

namespace ReusableAccess.Web.Academics;

public sealed class StudioAssessmentForm
{
    public StudioAssessmentForm(bool isQuiz = false)
    {
        IsQuiz = isQuiz;
    }

    public bool IsQuiz { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid? LessonKey { get; set; }

    public int MaxAttempts { get; set; } = 1;

    public decimal MaxScore { get; set; } = 20m;

    public int? StartDay { get; set; }

    public int? DueDay { get; set; }

    public int? EndDay { get; set; }

    public string? Prompt { get; set; }

    public string? Rubric { get; set; }

    public List<StudioQuestionForm> Questions { get; } = [];
}

public sealed class StudioQuestionForm
{
    public string Prompt { get; set; } = string.Empty;

    public string[] Choices { get; set; } =
        [string.Empty, string.Empty, string.Empty, string.Empty];

    public int? Correct { get; set; }

    public static StudioQuestionForm Fresh() => new();
}