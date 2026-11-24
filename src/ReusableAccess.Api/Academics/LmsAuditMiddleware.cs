using ReusableAccess.Infrastructure.Academics;

namespace ReusableAccess.Api.Academics;

internal sealed class LmsAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext c)
    {
        var path = c.Request.Path;
        var write = !HttpMethods.IsGet(c.Request.Method) && !HttpMethods.IsHead(c.Request.Method);
        var isApi = path.StartsWithSegments("/api");
        var isAdmin = path.StartsWithSegments("/api/admin");
        var form = write && path.StartsWithSegments("/account/form");
        var logout = write && path.StartsWithSegments("/connect/logout");
        var oidc = path.StartsWithSegments("/connect");
        if (!isApi && !form && !logout && !oidc) { await next(c); return; }
        // Use only endpoint metadata and stable categories, not raw route values, query strings, headers, or bodies.
        var named = c.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
        var authAction = form ? path.Value?.ToLowerInvariant().Split('/').Last() switch
        {
            "password" => "identity.login", "challenge" => "identity.login.challenge", "change-required" => "identity.password.change-required",
            "recovery" => "identity.recovery.request", "reset" => "identity.recovery.reset", "register" => "identity.register",
            _ => "identity.form"
        } : null;
        var kind = authAction ?? (logout ? "identity.logout" : (path.Equals(new PathString("/api/account/password"), StringComparison.OrdinalIgnoreCase) && write ? "identity.password.change" : null));
        var isException = false;
        try { await next(c); }
        catch { isException = true; throw; }
        finally
        {
            var status = isException ? 500 : c.Response.StatusCode;
            var action = kind ?? (named?.StartsWith("lms.", StringComparison.Ordinal) == true ? named : null)
                ?? (isAdmin ? LegacyAdmin(c, write) : oidc ? "identity.oidc" : write ? "api.write" : "api.access");
            if (form || logout || (isApi && (write || isAdmin)) || status is 400 or 401 or 403 or 409 or 413 or 429 or >= 500)
            {
                var result = status switch
                {
                    401 or 403 => "denied", 400 or 413 or 415 or 422 => "invalid", 409 => "conflict", 429 => "rate-limited",
                    >= 400 => "failed", _ => "success"
                };
                await OperationalAudit.WriteAsync(c, action, result, status);
            }
        }
    }
    private static string LegacyAdmin(HttpContext c, bool write)
    {
        var path = c.Request.Path.Value ?? "";
        var part = path.Split('/').ElementAtOrDefault(3)?.ToLowerInvariant() switch
        { "roles" => "role", "scopes" => "scope", "bundles" => "bundle", "users" => "user", "audit" => "audit", "access-settings" => "policy", _ => "operation" };
        if (path.EndsWith("/permissions", StringComparison.OrdinalIgnoreCase)) part = "permission";
        if (path.EndsWith("/assign", StringComparison.OrdinalIgnoreCase)) part = "assignment";
        if (path.EndsWith("/reset-password", StringComparison.OrdinalIgnoreCase)) part = "password-reset";
        return "admin." + part + (write ? ".write" : ".read");
    }
}
