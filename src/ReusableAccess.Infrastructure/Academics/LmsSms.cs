using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class LmsSms(LmsData data, IConfiguration config, IDataProtectionProvider protection, IHttpClientFactory clients)
{
    private static readonly Guid SettingsId = new("4cad4e93-9331-4c89-a3d8-1bf6391dde29");
    private readonly IDataProtector _secret = protection.CreateProtector("ReusableAccess.Lms.Sms.v1");
    private Uri? Endpoint(string provider)
    {
        if (provider.Length is < 1 or > 50 || provider.Any(x => !char.IsAsciiLetterOrDigit(x) && x != '-')) return null;
        return Uri.TryCreate(config[$"Lms:SmsProviders:{provider}:Endpoint"], UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
            string.IsNullOrEmpty(uri.UserInfo) ? uri : null;
    }
    private Task<SmsSettings?> Get(CancellationToken ct) => data.Read<SmsSettings>().SingleOrDefaultAsync(x => x.Id == SettingsId, ct);
    internal async Task<SmsView> View(CancellationToken ct)
    {
        var row = await Get(ct);
        var providers = config.GetSection("Lms:SmsProviders").GetChildren().Select(x => x.Key).Where(x => Endpoint(x) is not null).ToArray();
        return new(providers, row?.Provider, row is not null && Endpoint(row.Provider) is not null);
    }
    internal async Task<bool> Save(SmsEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var row = await data.Db.Set<SmsSettings>().FindAsync([SettingsId], ct);
        if (!input.Enabled) { if (row is not null) data.Db.Remove(row); }
        else
        {
            var provider = LmsError.Text(input.Provider, 50);
            if (Endpoint(provider) is null) throw LmsError.Invalid("درگاه باید در فهرست مجاز استقرار تعریف شده باشد.");
            var key = LmsError.Text(input.ApiKey, 500, true);
            if (key.Any(c => c is < '!' or > '~')) throw LmsError.Invalid("قالب کلید پیامک معتبر نیست.");
            if (key.Length == 0 && (row is null || row.Provider != provider)) throw LmsError.Invalid("کلید درگاه الزامی است.");
            if (row is null) { row = new SmsSettings { Id = SettingsId }; data.Db.Add(row); }
            row.Provider = provider; row.UpdatedUtc = data.Now;
            if (key.Length != 0) row.ProtectedKey = _secret.Protect(key);
        }
        return await data.Finish(tx, true, SettingsId, ct);
    }
    internal async Task<string> Deliver(Reminder notice, CancellationToken ct)
    {
        var row = await Get(ct);
        if (row is null || Endpoint(row.Provider) is not { } uri) return "NotConfigured";
        var to = await data.Read<ApplicationUser>().Where(x => x.Id == notice.UserId && x.IsActive && x.PhoneNumberConfirmed)
            .Select(x => x.PhoneNumber).SingleOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(to)) return "UnverifiedPhone";
        string key;
        try { key = _secret.Unprotect(row.ProtectedKey); }
        catch (System.Security.Cryptography.CryptographicException) { return "KeyUnavailable"; }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Add("X-Api-Key", key);
            request.Content = JsonContent.Create(new
            {
                to, message = $"یادآوری آموزشی: {notice.Title}\n{JalaliTime.Display(notice.DueUtc)} به وقت تهران",
                reference = notice.Id
            });
            using var response = await clients.CreateClient("UniversitySms").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            return response.IsSuccessStatusCode ? "GatewayAccepted" : "GatewayRejected";
        }
        catch (HttpRequestException) { return "Unknown"; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return "Unknown"; }
    }
}
