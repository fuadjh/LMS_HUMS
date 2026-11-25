using System.Globalization;
using Microsoft.AspNetCore.Components;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Web.Components.Lms;

public partial class LmsScheduleStudio
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Parameter] public Guid TemplateId { get; set; }
    [Parameter] public Guid VersionId { get; set; }
    [SupplyParameterFromQuery(Name = "target")] public Guid? TargetHint { get; set; }
    private int _step = 1;
    private bool _enabled, _changed, _differentPlan, _adjust, _confirm;
    private StudioTemplateDetails? _version;
    private StudioTarget[] _targets = [];
    private StudioTarget? _target;
    private Guid? _targetId, _termFilter, _origin;
    private CatalogRow[] _terms = [];
    private StudioSource[] _sources = [];
    private StudioDraftView? _draft;
    private StudioSchedulePreview? _preview;
    private ScheduleMode _mode = ScheduleMode.Relative;
    private string? _start, _dateToEdit, _status;
    private (ScheduleRow Row, ScheduleField Field)? _field;
    private readonly Dictionary<(Guid, ScheduleField), ScheduleOverride> _overrides = [];
    private string _confirmTitle = "", _confirmMessage = "";
    private Func<Task>? _action;
    private string Root => $"offerings/{_targetId:D}";
    private IEnumerable<CatalogRow> Available => _targets.Where(t => t.CanPrepare)
        .Select(t => new CatalogRow(t.Id, $"{TermLabel(t.Year, t.Season)} · گروه {Fa(t.Group)}{(t.DraftState == ScheduleDraftState.Draft ? " · پیش‌نویس" : "")}"));
    private IEnumerable<CatalogRow> Origins => _sources.Select(s => new CatalogRow(s.OfferingId,
        $"{TermLabel(s.Year, s.Season)} · گروه {Fa(s.Group)} · آغاز {LocalText(s.ClassesStartDate)}"));
    private bool Ready => !_changed && !_differentPlan && _preview is { Issues.Length: 0 } view &&
        _draft is { State: ScheduleDraftState.Draft, Preview: { } saved } && view.Stamp == saved.Stamp;

    protected override Task OnParametersSetAsync() => Run(async () =>
    {
        _step = 1;
        _target = null;
        _termFilter = null;
        _targetId = TargetHint;
        var home = await Get<LmsHome>("me");
        _enabled = home.Teacher && home.ScheduleStudioEnabled && home.Permissions.Contains(LmsPermissions.Template) &&
                   home.Permissions.Contains(LmsPermissions.Content);
        if (!_enabled) throw new ArgumentException("کارگاه فعال نیست یا مجوز لازم را ندارید.");
        _version = await Get<StudioTemplateDetails>($"template-bank/{TemplateId:D}?versionId={VersionId:D}");
        _terms = await Get<CatalogRow[]>("catalog/terms");
        await LoadTargets();
    });

    private async Task LoadTargets()
    {
        if (_version is not { } version) return;
        var query = $"teaching/schedule-targets?courseId={version.Template.CourseId:D}" +
                    (_termFilter is { } term ? $"&termId={term:D}" : "");
        _targets = await Get<StudioTarget[]>(query);
        if (!_targets.Any(t => t.Id == _targetId && t.CanPrepare)) _targetId = null;
    }
    private Task ReloadTargets() => Run(LoadTargets);
    private Task Choose() => Run(async () =>
    {
        _target = _targets.SingleOrDefault(t => t.Id == _targetId && t.CanPrepare)
            ?? throw new ArgumentException("ارائهٔ مقصد را انتخاب کنید.");
        _sources = await Get<StudioSource[]>($"{Root}/schedule-origins?versionId={VersionId:D}");
        _draft = (await Get<StudioDraftResult>($"{Root}/schedule-draft")).Draft;
        _differentPlan = _draft is { State: ScheduleDraftState.Draft, Plan: { } plan } && plan.TemplateVersionId != VersionId;
        _status = null;
        _preview = null;
        _overrides.Clear();
        _start = LocalText(_target.SuggestedClassesStart);
        _mode = ScheduleMode.Relative;
        _origin = null;
        _changed = true;
        if (!_differentPlan && _draft is { State: ScheduleDraftState.Draft, Plan: { } prior } && prior.TemplateVersionId == VersionId)
        {
            _start = LocalText(prior.ClassesStartDate);
            _mode = prior.Mode;
            _origin = prior.OriginOfferingId;
            foreach (var edit in prior.Overrides) _overrides[(edit.Key, edit.Field)] = edit;
            // Re-evaluate after reopening, preserving explicit overrides if source/term changed.
        }
        _step = 2;
    });
    private void Back()
    {
        if (_step == 3) _step = 2;
        else if (_step == 2 && _changed && (_preview is not null || _overrides.Count > 0))
            Ask("بازگشت به انتخاب مقصد", "پیش‌نویس ذخیره‌شده باقی می‌ماند؛ اصلاح‌های ثبت‌نشدهٔ این مقصد کنار گذاشته می‌شوند.",
                () => { _step = 1; return Task.CompletedTask; });
        else _step = 1;
    }
    private void ChangeStart(string? value) { _start = value; Dirty(); }
    private void ChangeOrigin(Guid? value) { _origin = value; Dirty(); }
    private void ChangeMode(ChangeEventArgs change)
    {
        if (!Enum.TryParse<ScheduleMode>(change.Value?.ToString(), out var mode)) return;
        _mode = mode;
        Dirty();
    }
    private void Dirty() { _changed = true; _status = null; }

    private StudioPlan Plan()
    {
        if (!JalaliTime.TryParseLocal(_start, out var date)) throw new ArgumentException("روز آغاز کلاس را به شمسی وارد کنید.");
        if (_mode == ScheduleMode.Shift && _origin is null) throw new ArgumentException("ارائهٔ مبدأ را انتخاب کنید.");
        return new(VersionId, DateOnly.FromDateTime(date), _mode, _mode == ScheduleMode.Shift ? _origin : null, _overrides.Values.ToArray());
    }
    private Task Preview() => Run(async () =>
    {
        _preview = await Post<StudioSchedulePreview>($"{Root}/schedule-preview", Plan());
        _changed = false;
        _step = 3;
        _status = "پیش‌نمایش محاسبه شد؛ هنوز روی ارائه اعمال نشده است.";
    });
    private Task Save() => Run(async () =>
    {
        if (_changed || _preview is not { } view || _differentPlan) return;
        _draft = await Post<StudioDraftView>($"{Root}/schedule-draft", new StudioStoreDraft(_draft?.Revision, view.OfferingRevision, view.Stamp, Plan()));
        _preview = _draft.Preview;
        _status = "پیش‌نویس ذخیره شد؛ هنوز برای دانشجو منتشر نشده است.";
    });
    private void AskDiscard() => Ask("دور ریختن پیش‌نویس", "فقط تنظیم‌های ثبت‌شدهٔ کارگاه لغو می‌شوند. ارائهٔ واگذارشده و الگو باقی می‌مانند.", Discard);
    private Task Discard() => Run(async () =>
    {
        if (_draft is not { State: ScheduleDraftState.Draft } draft) return;
        _ = await Post<bool>($"{Root}/schedule-draft/discard", new StudioRevision(draft.Revision));
        _draft = (await Get<StudioDraftResult>($"{Root}/schedule-draft")).Draft;
        _differentPlan = false;
        _overrides.Clear();
        _preview = null;
        Dirty();
        _status = "پیش‌نویس لغو شد.";
        _step = 2;
    });
    private void AskReset() => Ask("پاک‌کردن اصلاحات دستی", "پیشنهادهای نسبی یا منتقل‌شده دوباره محاسبه خواهند شد.", () =>
    {
        _overrides.Clear();
        Dirty();
        return Preview();
    });
    private void AskApply(bool publish) => Ask(publish ? "اعمال و انتشار" : "اعمال بدون انتشار",
        publish ? "محتوا و تاریخ‌های تأییدشده در یک عملیات ثبت و منتشر می‌شوند. هیچ دانشجویی خودکار اخذ نمی‌کند."
                : "فصل، درسک و ارزیابی ساخته می‌شوند ولی منتشر نمی‌شوند. ادامهٔ انتشار از نمای ارائه انجام می‌شود.", () => Apply(publish));
    private Task Apply(bool publish) => Run(async () =>
    {
        if (!Ready || _draft is not { } draft || _preview is not { } view) return;
        var operation = publish ? "publish" : "apply";
        var receipt = await Post<StudioReceipt>($"{Root}/schedule-draft/{operation}", new StudioApplyDraft(draft.Revision, view.OfferingRevision));
        _confirm = false;
        Navigation.NavigateTo($"/lms/offer/{receipt.OfferingId:D}/teacher");
    }, publish ? "ارائه منتشر شد." : "تقویم بدون انتشار اعمال شد.");

    private void EditField((ScheduleRow Row, ScheduleField Field) edit)
    {
        _field = edit;
        _dateToEdit = DateInput(edit.Field switch { ScheduleField.Start => edit.Row.Start.AtUtc,
            ScheduleField.Due => edit.Row.Due?.AtUtc, _ => edit.Row.End?.AtUtc });
        _adjust = true;
    }
    private Task SetDate() => Run(() =>
    {
        if (_field is not { } field || !JalaliTime.TryParseLocal(_dateToEdit, out var time))
            throw new ArgumentException("تاریخ یا ساعت معتبر نیست.");
        if (_dateToEdit?.Contains(':') == false)
            time = field.Field == ScheduleField.Start ? time.Date.AddHours(8) : time.Date.AddDays(1);
        var key = (field.Row.Item.Key, field.Field);
        _overrides[key] = new(key.Item1, key.Item2, ScheduleOverrideMode.Set, JalaliTime.FromDate(time));
        _adjust = false;
        Dirty();
        return Task.CompletedTask;
    });
    private void ClearDate()
    {
        if (_field is not { } field || field.Field != ScheduleField.End) return;
        _overrides[(field.Row.Item.Key, field.Field)] = new(field.Row.Item.Key, field.Field, ScheduleOverrideMode.Clear, null);
        _adjust = false;
        Dirty();
    }
    private void Inherit()
    {
        if (_field is { } field) _overrides.Remove((field.Row.Item.Key, field.Field));
        _adjust = false;
        Dirty();
    }
    private void Ask(string title, string message, Func<Task> run)
    { _confirmTitle = title; _confirmMessage = message; _action = run; _confirm = true; }
    private async Task Confirm() { _confirm = false; if (_action is not null) await _action(); }

    internal static string LocalText(DateOnly day)
    {
        var p = new PersianCalendar(); var date = day.ToDateTime(TimeOnly.MinValue);
        return Fa(FormattableString.Invariant($"{p.GetYear(date):0000}/{p.GetMonth(date):00}/{p.GetDayOfMonth(date):00}"));
    }
    private static string TermLabel(int year, TermSeason season) =>
        $"{(season switch { TermSeason.Mehr => "مهر", TermSeason.Bahman => "بهمن", _ => "تابستان" })} {Fa(year)}";
}
