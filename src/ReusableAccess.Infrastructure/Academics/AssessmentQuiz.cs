using System.Text.Json;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal static class AssessmentQuiz
{
    internal static QuizQuestionRow[]? Read(Assessment a, bool includeQuizKey)
    {
        if (a.QuizSpec is null) return null;
        var questions = JsonSerializer.Deserialize<QuizQuestionRow[]>(a.QuizSpec) ?? throw LmsError.Conflict("ساختار آزمون قابل خواندن نیست.");
        return includeQuizKey ? questions : questions.Select(q => q with { CorrectIndex = null }).ToArray();
    }
    internal static string? Validate(QuizQuestionRow[]? questions)
    {
        if (questions is null || questions.Length == 0) return null;
        if (questions.Length > 30) throw LmsError.Invalid("آزمون حداکثر ۳۰ سؤال دارد.");
        var normalized = new List<QuizQuestionRow>();
        foreach (var q in questions)
        {
            if (q is null || q.Choices is null || q.Choices.Length != 4 || q.CorrectIndex is null or < 0 or > 3)
                throw LmsError.Invalid("هر سؤال باید چهار گزینه و یک پاسخ درست داشته باشد.");
            var options = q.Choices.Select(x => LmsError.Text(x, 500)).ToArray();
            if (options.Distinct().Count() != 4) throw LmsError.Invalid("گزینه‌های یک سؤال باید متفاوت باشند.");
            normalized.Add(new(LmsError.Text(q.Prompt, 1000), options, q.CorrectIndex));
        }
        return JsonSerializer.Serialize(normalized);
    }
}
