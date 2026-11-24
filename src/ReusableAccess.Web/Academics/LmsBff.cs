using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using ReusableAccess.Contracts;

namespace ReusableAccess.Web.Academics;

public static class LmsBff
{
    public static IServiceCollection AddLmsWeb(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.KnownProxies.Clear(); o.KnownIPNetworks.Clear();
            foreach (var child in config.GetSection("Hosting:TrustedProxyIps").GetChildren())
            {
                if (!IPAddress.TryParse(child.Value, out var ip)) throw new InvalidOperationException("Invalid trusted proxy IP.");
                o.KnownProxies.Add(ip);
            }
            if (o.KnownProxies.Count == 0) return;
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
        });
        services.AddHttpClient("LmsStream", client =>
        {
            client.BaseAddress = new Uri(config["Api:BaseAddress"] ?? throw new InvalidOperationException("API address is required."));
            client.Timeout = TimeSpan.FromMinutes(45);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        return services;
    }
    public static IEndpointRouteBuilder MapLmsBff(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/bff/lms").RequireAuthorization();
        g.MapPut("/uploads/{id:guid}", async (Guid id, HttpContext c, IAntiforgery anti, IHttpClientFactory clients) =>
        {
            try { await anti.ValidateRequestAsync(c); }
            catch (AntiforgeryValidationException) { c.Response.StatusCode = 400; return; }
            if (c.Request.ContentLength > UploadLimits.FileBytes) { c.Response.StatusCode = 413; return; }
            using var req = new HttpRequestMessage(HttpMethod.Put, $"api/lms/uploads/{id:D}");
            req.Content = new StreamContent(c.Request.Body, 65536);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            req.Content.Headers.ContentLength = c.Request.ContentLength;
            await Relay(c, req, clients);
        }).WithMetadata(new RequestSizeLimitAttribute(UploadLimits.FileBytes));
        g.MapGet("/files/{id:guid}", async (Guid id, HttpContext c, IHttpClientFactory clients) =>
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"api/lms/files/{id:D}");
            foreach (var h in new[] { "Range", "If-Range" })
                if (c.Request.Headers.TryGetValue(h, out var value)) req.Headers.TryAddWithoutValidation(h, value.ToArray());
            await Relay(c, req, clients);
        });
        return app;
    }
    private static async Task Relay(HttpContext c, HttpRequestMessage req, IHttpClientFactory clients)
    {
        c.Response.Headers.CacheControl = "private, no-store";
        c.Response.Headers["X-Content-Type-Options"] = "nosniff";
        var token = await c.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) { c.Response.StatusCode = 401; return; }
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (c.Connection.RemoteIpAddress is { } ip) req.Headers.TryAddWithoutValidation("X-Forwarded-For", ip.ToString());
        req.Headers.TryAddWithoutValidation("X-Forwarded-Proto", c.Request.Scheme);
        try
        {
            using var client = clients.CreateClient("LmsStream");
            using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, c.RequestAborted);
            var status = (int)response.StatusCode;
            c.Response.StatusCode = status is >= 300 and < 400 ? 502 : status;
            if (status is >= 300 and < 400) return;
            c.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            c.Response.ContentLength = response.Content.Headers.ContentLength;
            foreach (var h in new[] { "Content-Disposition", "Content-Range", "Last-Modified", "Accept-Ranges", "ETag" })
                if (response.Content.Headers.TryGetValues(h, out var values) || response.Headers.TryGetValues(h, out values))
                    c.Response.Headers[h] = values.ToArray();
            await response.Content.CopyToAsync(c.Response.Body, c.RequestAborted);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
        {
            if (c.Response.HasStarted) { c.Abort(); return; }
            if (!c.RequestAborted.IsCancellationRequested) { c.Response.ContentLength = null; c.Response.StatusCode = 502; }
        }
    }
}
