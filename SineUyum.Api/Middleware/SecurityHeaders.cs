namespace SineUyum.Api.Middleware
{
    // G9: Her yanıta (hatalar ve 429 dahil) temel güvenlik başlıkları. API
    // yalnızca JSON döndüğü için katı bir CSP uygundur: yanıt bir tarayıcıda
    // doğrudan açılsa ya da çerçeveye konsa bile betik çalıştıramaz.
    // Swagger arayüzü (yalnızca geliştirmede) kendi betiklerine ihtiyaç
    // duyduğu için CSP'den muaftır.
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
                    // Tarayıcı içerik türünü tahmin etmesin (ör. yüklenen dosyayı HTML sanmasın).
                    headers["X-Content-Type-Options"] = "nosniff";
                    // Başka sitelerin çerçevesinde açılmasın (clickjacking).
                    headers["X-Frame-Options"] = "DENY";
                    // İstek adreslerindeki kimlikler (/profile/{id} vb.) başka sitelere sızmasın.
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
