using System.Globalization;
using Microsoft.AspNetCore.Components;
using ReusableAccess.Domain.Academics;
#if USE_PERSIAN_DATEPICKER_ZO
using Blazor_PersianDatePickerZO.Component;
#endif

namespace ReusableAccess.Web.Components.Ui;

public partial class PersianDateField
{
    [Parameter, EditorRequired] public string Label { get; set; } = "تاریخ";
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public bool DayOnly { get; set; }
    [Parameter] public bool UtcClock { get; set; }
    [Parameter] public bool AllowClear { get; set; } = true;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public TimeOnly PreferredTime { get; set; } = TimeOnly.MinValue;
    private readonly string _fieldId = $"ra-date-{Guid.NewGuid():N}";
    private string _clockId => _fieldId + "-clock";
    private string? _error;
    private DateTime? _pending;
    private bool _dialog;
    private string _clock = "00:00";
    private string ZoneLabel => UtcClock ? "UTC" : "تهران";

    public static bool UsingZo
    {
        get
        {
#if USE_PERSIAN_DATEPICKER_ZO
            return true;
#else
            return false;
#endif
        }
    }

    private void Open()
    {
        if (Disabled) return;
        var initial = JalaliTime.TryParseLocal(Value, out var local);
        _pending = initial ? local.Date : (UtcClock ? DateTime.UtcNow : JalaliTime.InTehran(DateTimeOffset.UtcNow)).Date;
        _clock = initial ? local.ToString("HH:mm", CultureInfo.InvariantCulture) : PreferredTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        _error = null;
        _dialog = true;
    }

    private Task ClosePicker(bool open) { _dialog = open; return Task.CompletedTask; }
    private Task ChangeText(ChangeEventArgs e) => ValueChanged.InvokeAsync(e.Value?.ToString());
    private void ChangeClock(ChangeEventArgs e) => _clock = e.Value?.ToString() ?? "";
    private Task Clear() => ValueChanged.InvokeAsync(null);

    private async Task Commit()
    {
        if (Disabled || _pending is not { } selected) return;
        var persian = new PersianCalendar();
        var day = FormattableString.Invariant($"{persian.GetYear(selected):0000}/{persian.GetMonth(selected):00}/{persian.GetDayOfMonth(selected):00}");
        var value = DayOnly ? day : $"{day} {_clock}";
        if ((!DayOnly && !TimeOnly.TryParseExact(_clock, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) ||
            !(UtcClock ? JalaliTime.TryParseLocal(value, out _) : JalaliTime.TryParse(value, out _)))
        {
            _error = "روز یا ساعت انتخاب‌شده معتبر نیست.";
            return;
        }
        _dialog = false;
        await ValueChanged.InvokeAsync(value);
    }

    private RenderFragment ZoCalendar() => builder =>
    {
#if USE_PERSIAN_DATEPICKER_ZO
        builder.OpenComponent<DatePickerZO>(0);
        builder.AddComponentParameter(1, nameof(DatePickerZO.PopupDatePickerZO), false);
        builder.AddComponentParameter(2, nameof(DatePickerZO.Format), "yyyy/MM/dd");
        builder.AddComponentParameter(3, nameof(DatePickerZO.SelectDate), _pending);
        // Upstream initializes callbacks during rendering. Only mutate the modal's temporary date here.
        builder.AddComponentParameter(4, nameof(DatePickerZO.SelectDateChanged),
            EventCallback.Factory.Create<DateTime?>(this, value => { _pending = value; }));
        builder.CloseComponent();
#endif
    };
}
