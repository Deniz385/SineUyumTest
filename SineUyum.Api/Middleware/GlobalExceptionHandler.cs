using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SineUyum.Api.Exceptions;

namespace SineUyum.Api.Middleware
{
    // Yakalanmayan tüm istisnaları tek yerde ele alır. Önceden beklenmeyen bir
    // hata stack trace'i istemciye sızdırabiliyordu (ör. AdminController ex.Message'ı
    // doğrudan döndürüyordu). Artık:
    //   - DomainException → kendi durum kodu + güvenli mesaj
    //   - diğer her şey → 500 + genel mesaj (detay yalnızca log'a yazılır)
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // İstek izlemek için correlation id — hem log'da hem yanıtta.
            var traceId = httpContext.TraceIdentifier;

            ProblemDetails problem;
            if (exception is DomainException domain)
            {
                // Beklenen iş kuralı hatası: bilgi amaçlı logla, mesajı göster.
                _logger.LogInformation(
                    "Domain hatası ({StatusCode}) {TraceId}: {Message}",
                    domain.StatusCode, traceId, domain.Message);

                problem = new ProblemDetails
                {
                    Status = domain.StatusCode,
                    Title = domain.Message,
                };
            }
            else if (exception is BadHttpRequestException badRequest)
            {
                // Kestrel'in istemci kaynaklı reddi (ör. gövde sınırı aşıldı → 413,
                // bozuk istek → 400). Önceden bunlar da "sunucu hatası" 500'üne
                // düşüyordu. Kestrel'in mesajı iç ayrıntı içerebileceği için
                // istemciye genel bir başlık gider.
                _logger.LogInformation(
                    "İstemci isteği reddedildi ({StatusCode}) {TraceId}: {Message}",
                    badRequest.StatusCode, traceId, badRequest.Message);

                problem = new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Title = badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                        ? "İstek gövdesi izin verilen boyutu aşıyor."
                        : "Geçersiz istek.",
                };
            }
            else
            {
                // Beklenmeyen hata: tüm ayrıntı log'a, istemciye genel mesaj.
                _logger.LogError(
                    exception,
                    "İşlenmeyen hata {TraceId} — {Path}",
                    traceId, httpContext.Request.Path);

                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Sunucuda beklenmeyen bir hata oluştu.",
                };
            }

            problem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = problem.Status!.Value;
            await httpContext.Response
                .WriteAsJsonAsync(problem, cancellationToken);

            return true; // hata ele alındı
        }
    }
}
