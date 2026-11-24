using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ReusableAccess.Api.Academics;

public static class LmsProxyTrust
{
    public static IServiceCollection AddLmsProxyTrust(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.KnownProxies.Clear(); o.KnownIPNetworks.Clear();
            foreach (var child in config.GetSection("Hosting:TrustedProxyIps").GetChildren())
            {
                if (!IPAddress.TryParse(child.Value, out var ip)) throw new InvalidOperationException("Invalid trusted proxy IP.");
                o.KnownProxies.Add(ip);
            }
            if (o.KnownProxies.Count == 0) return; // no trusted proxies means no forwarded headers, never trust every proxy
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
        });
        return services;
    }
}
