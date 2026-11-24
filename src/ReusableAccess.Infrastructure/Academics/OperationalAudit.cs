using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Academics;

public static class OperationalAudit
{
    public static void Outcome(HttpContext? context, string value, Guid? actor = null)
    {
        if (context is null) return;
        context.Items["lms-audit-outcome"] = value;
        if (actor.HasValue) context.Items["lms-audit-actor"] = actor.Value;
    }
    public static void Actor(HttpContext? context, Guid actor)
    {
        if (context is not null) context.Items["lms-audit-actor"] = actor;
    }
    public static string? SafeCorrelation(string? raw)
        => !string.IsNullOrWhiteSpace(raw) && raw.Length <= 100 && raw.All(x => char.IsAsciiLetterOrDigit(x) || x is ':' or '-' or '_')
            ? raw : null;
    public static async Task WriteAsync(HttpContext context, string action, string fallback, int status)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            // A failed business transaction or tracked entities from a request must not leak into the audit write.
            await using var scope = context.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            var actor = context.Items["lms-audit-actor"] is Guid known ? known : LmsAccess.Subject(context.User);
            var resource = context.Items["lms-resource"] is Guid marked ? marked : context.Request.RouteValues.Values
                .Select(x => Guid.TryParse(x?.ToString(), out var id) ? (Guid?)id : null).FirstOrDefault(x => x.HasValue);
            db.Add(new OperationalEvent { ActorId = actor, Action = action, Outcome = context.Items["lms-audit-outcome"] as string ?? fallback,
                OccurredUtc = clock.GetUtcNow(), ResourceId = resource, Ip = context.Connection.RemoteIpAddress?.ToString(),
                Correlation = SafeCorrelation(context.TraceIdentifier), HttpStatus = status });
            await db.SaveChangesAsync(timeout.Token);
        }
        catch (Exception)
        {
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ReusableAccess.OperationalAudit")
                .LogWarning("Operational audit persistence failed for {Action}; status {Status}.", action, status);
        }
    }
}
