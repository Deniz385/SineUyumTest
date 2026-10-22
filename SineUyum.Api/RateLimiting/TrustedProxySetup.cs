using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace SineUyum.Api.RateLimiting
{
    // Behind a reverse proxy (nginx, load balancer) the client's IP is in
    // X-Forwarded-For; otherwise everyone shares the proxy's IP and per-IP
    // limits (login, anonymous requests) cut off all users together. The header
    // is only trusted when it comes from a proxy listed in TRUSTED_PROXIES; if
    // none is set it is ignored (so a client cannot fake an IP to dodge limits).
    //
    //   TRUSTED_PROXIES="10.0.0.5,172.18.0.0/16"
    public static class TrustedProxySetup
    {
        public static IServiceCollection AddTrustedProxies(this IServiceCollection services)
        {
            services.AddOptions<ForwardedHeadersOptions>()
                .Configure<IConfiguration>((options, configuration) =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    // Loopback is trusted by default; keep only the explicitly given ones.
                    options.KnownProxies.Clear();
                    options.KnownNetworks.Clear();

                    var entries = (configuration["TRUSTED_PROXIES"] ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var entry in entries)
                    {
                        AddEntry(options, entry);
                    }

                    // Careful: with both lists empty the middleware trusts the
                    // header from ANYONE. Without a configured proxy the header is not processed at all.
                    if (options.KnownProxies.Count == 0 && options.KnownNetworks.Count == 0)
                    {
                        options.ForwardedHeaders = ForwardedHeaders.None;
                    }
                })
                // A bad value is caught at startup, not on the first request.
                .ValidateOnStart();

            return services;
        }

        private static void AddEntry(ForwardedHeadersOptions options, string entry)
        {
            var parts = entry.Split('/');
            if (!IPAddress.TryParse(parts[0], out var address))
            {
                throw new OptionsValidationException(nameof(ForwardedHeadersOptions), typeof(ForwardedHeadersOptions),
                    [$"TRUSTED_PROXIES is invalid: '{entry}' is not an IP address or CIDR (e.g. 10.0.0.0/8)."]);
            }

            if (parts.Length == 1)
            {
                options.KnownProxies.Add(address);
                return;
            }

            var maxPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
            if (parts.Length != 2 || !int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > maxPrefix)
            {
                throw new OptionsValidationException(nameof(ForwardedHeadersOptions), typeof(ForwardedHeadersOptions),
                    [$"TRUSTED_PROXIES is invalid: '{entry}' is not an IP address or CIDR (e.g. 10.0.0.0/8)."]);
            }
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(address, prefix));
        }
    }
}
