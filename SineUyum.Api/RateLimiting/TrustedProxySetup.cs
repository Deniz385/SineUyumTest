using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace SineUyum.Api.RateLimiting
{
    // API bir ters vekil (nginx, yük dengeleyici) arkasındaysa istemcinin IP'si
    // X-Forwarded-For'dadır; aksi halde herkes vekilin IP'sini paylaşır ve IP
    // başına sınırlar (giriş, kimliksiz istekler) tüm kullanıcıları birlikte
    // keser. Başlığa yalnızca TRUSTED_PROXIES'te listelenen vekillerden gelince
    // güvenilir; tanımsızsa yok sayılır (istemci sahte IP yazıp sınırı aşamasın).
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
                    // Varsayılan olarak loopback güvenilir sayılır; yalnızca açıkça verilenler kalsın.
                    options.KnownProxies.Clear();
                    options.KnownNetworks.Clear();

                    var entries = (configuration["TRUSTED_PROXIES"] ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var entry in entries)
                    {
                        AddEntry(options, entry);
                    }

                    // Dikkat: iki liste de boşsa ara katman başlığa HERKESTEN
                    // güvenir. Vekil tanımlı değilse başlık hiç işlenmez.
                    if (options.KnownProxies.Count == 0 && options.KnownNetworks.Count == 0)
                    {
                        options.ForwardedHeaders = ForwardedHeaders.None;
                    }
                })
                // Hatalı değer ilk istekte değil açılışta fark edilsin.
                .ValidateOnStart();

            return services;
        }

        private static void AddEntry(ForwardedHeadersOptions options, string entry)
        {
            var parts = entry.Split('/');
            if (!IPAddress.TryParse(parts[0], out var address))
            {
                throw new OptionsValidationException(nameof(ForwardedHeadersOptions), typeof(ForwardedHeadersOptions),
                    [$"TRUSTED_PROXIES geçersiz: '{entry}' bir IP adresi ya da CIDR (ör. 10.0.0.0/8) değil."]);
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
                    [$"TRUSTED_PROXIES geçersiz: '{entry}' bir IP adresi ya da CIDR (ör. 10.0.0.0/8) değil."]);
            }
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(address, prefix));
        }
    }
}
