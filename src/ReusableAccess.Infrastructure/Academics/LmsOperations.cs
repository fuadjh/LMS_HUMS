using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class LmsOperations(LmsData data, LmsAccess access, LmsFiles files, LmsSms sms) : ILmsOperations
{
    public Task<UploadTicket> StartAsync(UploadStart input, CancellationToken ct) => files.Start(input, ct);
    public async Task<UploadStatusRow> UploadAsync(Guid id, CancellationToken ct)
    {
        var actor = await access.Load(ct);
        var row = await data.Read<UploadAttempt>().SingleOrDefaultAsync(x => x.Id == id && (x.ActorId == actor.Id || actor.IsPlatformSuperAdmin), ct)
            ?? throw LmsError.Missing();
        return Map(row);
    }
    public async Task<LogPage<UploadStatusRow>> UploadsAsync(LogFilter filter, CancellationToken ct)
    {
        await access.AsPlatform(ct);
        var page = Page(filter);
        if (filter.MinBytes is < 0 || filter.MaxBytes is < 0 || filter.MinBytes > filter.MaxBytes ||
            filter.State is { } state && !Enum.IsDefined(state)) throw LmsError.Invalid("بازهٔ حجم یا وضعیت معتبر نیست.");
        var name = LmsError.Text(filter.Name, 100, true);
        var cutoff = data.Now.AddMonths(-1);
        var rows = await data.Read<UploadAttempt>().Where(x => x.CreatedUtc >= cutoff && (filter.FromUtc == null || x.CreatedUtc >= filter.FromUtc) &&
            (filter.ToUtc == null || x.CreatedUtc < filter.ToUtc) && (name == "" || x.Name.Contains(name)) &&
            (filter.MinBytes == null || x.ReportedBytes >= filter.MinBytes) && (filter.MaxBytes == null || x.ReportedBytes <= filter.MaxBytes) &&
            (filter.State == null || x.State == filter.State) && (filter.TrackingId == null || x.Id == filter.TrackingId) &&
            (filter.ActorId == null || x.ActorId == filter.ActorId))
            .OrderByDescending(x => x.CreatedUtc).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(51).ToArrayAsync(ct);
        return new(rows.Take(50).Select(Map).ToArray(), page, rows.Length > 50);
    }
    private static UploadStatusRow Map(UploadAttempt x)
        => new(x.Id, x.Name, x.ReportedBytes, x.ReceivedBytes, x.State, x.Code, x.CreatedUtc, x.UpdatedUtc, x.ActorId, x.TargetId, x.Ip, x.Correlation);
    public async Task<LogPage<SecurityRow>> SecurityAsync(LogFilter filter, CancellationToken ct)
    {
        await access.AsPlatform(ct);
        var page = Page(filter);
        var action = LmsError.Text(filter.Name, 100, true);
        var outcome = LmsError.Text(filter.Outcome, 40, true);
        var cutoff = data.Now.AddMonths(-1);
        var rows = await data.Read<OperationalEvent>().Where(x => x.OccurredUtc >= cutoff && (filter.FromUtc == null || x.OccurredUtc >= filter.FromUtc) &&
            (filter.ToUtc == null || x.OccurredUtc < filter.ToUtc) && (action == "" || x.Action.Contains(action)) &&
            (filter.ActorId == null || x.ActorId == filter.ActorId) && (outcome == "" || x.Outcome == outcome))
            .OrderByDescending(x => x.OccurredUtc).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(51).Select(x =>
                new SecurityRow(x.OccurredUtc, x.ActorId, x.Action, x.Outcome, x.ResourceId, x.Ip, x.Correlation, x.HttpStatus)).ToArrayAsync(ct);
        return new(rows.Take(50).ToArray(), page, rows.Length > 50);
    }
    private static int Page(LogFilter f) => f.FromUtc > f.ToUtc || f.Page is < 1 or > 10000
        ? throw LmsError.Invalid("بازهٔ زمان یا شمارهٔ صفحه معتبر نیست.") : f.Page;
    public async Task<SmsView> SmsAsync(CancellationToken ct)
    {
        await access.AsPlatform(ct);
        return await sms.View(ct);
    }
    public async Task<bool> SaveSmsAsync(SmsEdit input, CancellationToken ct)
    {
        await access.AsPlatform(ct);
        return await sms.Save(input, ct);
    }
}
