using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace SineUyum.Api.RateLimiting
{
    // "RateLimit" bölümü (dakikadaki istek sayısı). Ortam değişkeniyle de
    // verilebilir: RateLimit__GlobalPermitLimit=600 gibi.
    public class RateLimitSettings
    {
        // Her istek: giriş yapmışsa kullanıcı, yapmamışsa IP başına.
        public int GlobalPermitLimit { get; set; } = 300;
        // Giriş/kayıt/yenileme/parola: IP başına (brute force).
        public int AuthPermitLimit { get; set; } = 10;
        // Fotoğraf yükleme ve ödeme doğrulama: kullanıcı başına.
        public int UploadPermitLimit { get; set; } = 10;
        // TMDb'ye giden uçlar (film arama/detay/listeler, ortak öneri): kotayı korur.
        public int TmdbPermitLimit { get; set; } = 60;
        // Bildirim üreten yazmalar (mesaj, takip): spam'e karşı.
        public int SocialWritePermitLimit { get; set; } = 30;
    }

    public static class RateLimitPolicies
    {
        public const string Auth = "auth";
        public const string Upload = "upload";
        public const string Payment = "payment";
        public const string Tmdb = "tmdb";
        public const string SocialWrite = "social-write";
    }

    // G5: önceden yalnızca giriş uçları sınırlıydı; geri kalan her uç (TMDb
    // vekilleri dahil) sınırsız çağrılabiliyordu. Artık her isteğe genel bir
    // sınır, pahalı/kötüye kullanılabilir uçlara ayrıca sıkı bir sınır uygulanır
    // (ikisi birlikte geçerli). Sınırlar istekte okunur; testler kendi
    // değerlerini verebilir.
    public static class RateLimitingSetup
    {
        public const string RejectedTitle = "Çok fazla istek. Lütfen biraz sonra tekrar deneyin.";

        public static IServiceCollection AddSineUyumRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RateLimitSettings>(configuration.GetSection("RateLimit"));

            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    IsExempt(context.Request.Path)
                        ? RateLimitPartition.GetNoLimiter("muaf")
                        : PerMinute(UserOrIp(context), Settings(context).GlobalPermitLimit));

                options.AddPolicy(RateLimitPolicies.Auth, context =>
                    PerMinute(Ip(context), Settings(context).AuthPermitLimit));
                options.AddPolicy(RateLimitPolicies.Upload, context =>
                    PerMinute(UserOrIp(context), Settings(context).UploadPermitLimit));
                options.AddPolicy(RateLimitPolicies.Payment, context =>
                    PerMinute(UserOrIp(context), Settings(context).UploadPermitLimit));
                options.AddPolicy(RateLimitPolicies.Tmdb, context =>
                    PerMinute(UserOrIp(context), Settings(context).TmdbPermitLimit));
                options.AddPolicy(RateLimitPolicies.SocialWrite, context =>
                    PerMinute(UserOrIp(context), Settings(context).SocialWritePermitLimit));

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = WriteRejectionAsync;
            });

            return services;
        }

        // Orkestratör sağlık yoklamaları ve geliştirici araçları sınırlanmaz.
        private static bool IsExempt(PathString path) =>
            path.StartsWithSegments("/health") || path.StartsWithSegments("/swagger");

        private static RateLimitSettings Settings(HttpContext context) =>
            context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        private static string Ip(HttpContext context) =>
            "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor");

        // Kimliği doğrulanmış istekler kullanıcıya göre bölünür: aynı ağdaki
        // (NAT) kullanıcılar birbirinin hakkını tüketmez.
        private static string UserOrIp(HttpContext context) =>
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
                ? "u:" + userId
                : Ip(context);

        private static RateLimitPartition<string> PerMinute(string key, int permitLimit) =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permitLimit),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

        // Diğer hatalarla aynı biçim (ProblemDetails) + ne zaman tekrar
        // denenebileceği (Retry-After, saniye).
        private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            var http = context.HttpContext;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                http.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problems.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = RejectedTitle
                }
            });
        }
    }
}
