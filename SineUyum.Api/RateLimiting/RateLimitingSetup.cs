using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace SineUyum.Api.RateLimiting
{
    // The "RateLimit" section (requests per minute). Can also be given as
    // environment variables, e.g. RateLimit__GlobalPermitLimit=600.
    public class RateLimitSettings
    {
        // Every request: per user when signed in, otherwise per IP.
        public int GlobalPermitLimit { get; set; } = 300;
        // Login/register/refresh/password: per IP (brute force).
        public int AuthPermitLimit { get; set; } = 10;
        // Photo upload and payment verification: per user.
        public int UploadPermitLimit { get; set; } = 10;
        // Endpoints that call TMDb (movie search/details/lists, joint recommendations): protects the quota.
        public int TmdbPermitLimit { get; set; } = 60;
        // Writes that create notifications (message, follow): against spam.
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

    // G5: only the login endpoints used to be limited; every other endpoint
    // (TMDb proxies included) could be called without limit. Now a global
    // limit applies to every request and a strict one to expensive/abusable
    // endpoints (both at once). Limits are read per request, so tests can
    // supply their own values.
    public static class RateLimitingSetup
    {
        // Locale key of the 429 title.
        public const string RejectedTitleKey = "errors.tooManyRequests";

        public static IServiceCollection AddSineUyumRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RateLimitSettings>(configuration.GetSection("RateLimit"));

            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    IsExempt(context.Request.Path)
                        ? RateLimitPartition.GetNoLimiter("exempt")
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

        // Orchestrator health probes and developer tools are not limited.
        private static bool IsExempt(PathString path) =>
            path.StartsWithSegments("/health") || path.StartsWithSegments("/swagger");

        private static RateLimitSettings Settings(HttpContext context) =>
            context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        private static string Ip(HttpContext context) =>
            "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        // Authenticated requests are partitioned per user: users on the same
        // network (NAT) do not use up each other's allowance.
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

        // Same format as other errors (ProblemDetails, in the request language)
        // + when to retry (Retry-After, seconds).
        private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            var http = context.HttpContext;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                http.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
            var t = http.RequestServices.GetRequiredService<IStringLocalizer>();
            await problems.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = t[RejectedTitleKey]
                }
            });
        }
    }
}
