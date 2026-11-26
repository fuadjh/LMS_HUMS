using Microsoft.AspNetCore.Components;
using MudBlazor;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Web.Completion;

namespace ReusableAccess.Web.Academics;

public abstract class LmsPage : ComponentBase
{
    [Inject] protected ConsoleApi Api { get; set; } = default!;
    [Inject] protected ISnackbar Snack { get; set; } = default!;
    protected bool Busy { get; private set; }
    protected string? Error { get; private set; }
    protected Task<T> Get<T>(string endpoint) => Api.GetAsync<T>("api/lms/" + endpoint);
    protected Task<T> Post<T>(string endpoint, object? body = null) => Api.PostAsync<T>("api/lms/" + endpoint, body ?? new { });
    protected async Task Run(Func<Task> action, string? success = null)
    {
        if (Busy) return;
        Busy = true; Error = null;
        try
        {
            await action();
            if (success is not null) Snack.Add(success, Severity.Success);
        }
        catch (Exception e) when (e is ConsoleApiException or ArgumentException)
        { Error = e.Message; Snack.Add(e.Message, Severity.Error); }
        finally { Busy = false; }
    }
    protected static DateTimeOffset InputDate(string? value) => JalaliTime.TryParse(value, out var utc) ? utc
        : throw new ArgumentException("تاریخ/ساعت شمسی را به شکل ۱۴۰۵/۰۹/۰۱ ۱۴:۳۰ وارد کنید.");
    protected static DateTimeOffset? DateOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : InputDate(value);
    // Only these new scheduling forms apply the day-only policy; exact times stay unchanged.
    protected static DateTimeOffset? AvailabilityDate(string? value, bool end = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!JalaliTime.TryParseLocal(value, out var local))
            throw new ArgumentException("تاریخ شمسی معتبر نیست؛ نمونه: ۱۴۰۵/۰۹/۰۱ ۰۸:۰۰");
        if (value.Split(['/', ' ', ':'], StringSplitOptions.RemoveEmptyEntries).Length == 3)
            local = end ? local.Date.AddDays(1) : local.Date.AddHours(8);
        return JalaliTime.FromDate(local);
    }
    internal static string DateText(DateTimeOffset? value) => value is { } t ? Fa(JalaliTime.Display(t)) : "—";
    protected static string? DateInput(DateTimeOffset? value) => value.HasValue ? DateText(value) : null;
    internal static string Fa(object? value) => string.Concat((Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "")
        .Select(c => c is >= '0' and <= '9' ? "۰۱۲۳۴۵۶۷۸۹"[c - '0'] : c));
    internal static string Size(long? bytes) => bytes is not { } b ? "نامعلوم" :
        b >= 1_000_000 ? Fa((b / 1_000_000m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)) + " مگابایت" : Fa(b) + " بایت";
    protected static string Q(string? s) => Uri.EscapeDataString(s ?? "").Replace(".", "%2E", StringComparison.Ordinal);
    protected static string State(EnrollmentStatus? s) => s switch
    {
        EnrollmentStatus.Active => "اخذ شده", EnrollmentStatus.WithdrawnByStudent => "حذف توسط دانشجو",
        EnrollmentStatus.RemovedByAdmin => "حذف توسط مدیر", _ => "اخذ نشده"
    };
    protected static string Audience(OfferingAudience a) => a switch
    { OfferingAudience.General => "عمومی دانشگاه", OfferingAudience.Common => "مشترک دانشکده", _ => "تخصصی رشته" };
    protected static string UploadStateLabel(UploadState s) => s switch
    {
        UploadState.Stored => "ذخیره شد", UploadState.Pending => "در انتظار بدنه", UploadState.Receiving => "دریافت در جریان",
        UploadState.Interrupted => "انتقال قطع شد", UploadState.Rejected => "رد شد", _ => "تکمیل تأیید نشد"
    };
}
