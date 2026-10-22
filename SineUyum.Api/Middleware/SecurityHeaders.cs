namespace SineUyum.Api.Middleware
{
    // G9: basic security headers on every response (errors and 429 included).
    // The API only returns JSON, so a strict CSP fits: even if a response is
    // opened directly in a browser or framed, it cannot run scripts. The
    // Swagger UI (development only) needs its own scripts and is exempt from the CSP.
    public static class SecurityHeaders
    {
        public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
            app.Use((context, next) =>
            {
                var path = context.Request.Path;
                context.Response.OnStarting(() =>
                {
                    var headers = context.Response.Headers;
                    // The browser must not guess the content type (e.g. treat an uploaded file as HTML).
                    headers["X-Content-Type-Options"] = "nosniff";
                    // Must not be opened inside other sites' frames (clickjacking).
                    headers["X-Frame-Options"] = "DENY";
                    // Ids in request URLs (/profile/{id} etc.) must not leak to other sites.
                    headers["Referrer-Policy"] = "no-referrer";
                    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                    headers["Cross-Origin-Opener-Policy"] = "same-origin";
                    if (!path.StartsWithSegments("/swagger"))
                    {
                        headers["Content-Security-Policy"] = ApiContentSecurityPolicy;
                    }
                    return Task.CompletedTask;
                });
                return next(context);
            });
    }
}
