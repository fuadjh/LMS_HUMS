using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Web.Academics;

internal static class LearningUi
{
    private static readonly string[] Ordinals = ["اول", "دوم", "سوم", "چهارم", "پنجم", "ششم", "هفتم", "هشتم", "نهم", "دهم"];
    internal static string Ordinal(int zeroBased) => zeroBased is >= 0 and < 10 ? Ordinals[zeroBased] : $"شمارهٔ {LmsPage.Fa(zeroBased + 1)}";
    internal static int Percent(int completed, int total) => total <= 0 ? 0 : (int)Math.Round(100m * completed / total, MidpointRounding.AwayFromZero);
    internal static string Label(LearningKind kind) => kind switch
    {
        LearningKind.Video => "ویدئو", LearningKind.Pdf => "جزوه PDF", LearningKind.Audio => "صوت",
        LearningKind.Image => "تصویر", LearningKind.Text => "متن", _ => "فایل"
    };
    internal static string Icon(LearningKind kind) => kind switch
    {
        LearningKind.Video => "play", LearningKind.Pdf => "pdf", LearningKind.Audio => "audio", LearningKind.Image => "image", _ => "text"
    };
    internal static string Default(LearningKind kind) => $"/learning/{(kind == LearningKind.LegacyFile ? "text" : kind.ToString().ToLowerInvariant())}.svg";
    internal static string File(Guid id) => $"/bff/lms/files/{id:D}";
    internal static string Cover(LearningThumbnail thumb) => thumb.CoverId is { } cover ? File(cover) : Default(thumb.Kind);
    internal static string Offer(Guid id, string view, bool draft = false)
        => $"/lms/offer/{id:D}/{view}?tab=content" + (draft ? "&draft=true" : "");
    internal static string Study(Guid id, string view, Guid lesson, Guid? material = null, bool draft = false) =>
        $"/lms/offer/{id:D}/{view}/learn/{lesson:D}" + (material is { } m ? $"/{m:D}" : "") + (draft ? "?draft=true" : "");
}
