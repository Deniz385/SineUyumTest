using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Exceptions;

namespace SineUyum.Api.Middleware
{
    // Handles every uncaught exception in one place. Previously an unexpected
    // error could leak a stack trace to the client (e.g. AdminController
    // returned ex.Message directly). Now:
    //   - DomainException → its own status code + its message in the request language
    //   - anything else → 500 + a generic message (details only go to the log)
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IStringLocalizer _t;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IStringLocalizer localizer)
        {
            _logger = logger;
            _t = localizer;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // Correlation id for tracing a request — in the log and the response.
            var traceId = httpContext.TraceIdentifier;

            ProblemDetails problem;
            if (exception is DomainException domain)
            {
                // Expected business error: log as information, show the message.
                _logger.LogInformation(
                    "Domain error ({StatusCode}) {TraceId}: {MessageKey}",
                    domain.StatusCode, traceId, domain.MessageKey);

                problem = new ProblemDetails
                {
                    Status = domain.StatusCode,
                    Title = _t[domain.MessageKey, domain.MessageArgs.Select(a => a ?? string.Empty).ToArray()],
                };
            }
            else if (exception is BadHttpRequestException badRequest)
            {
                // Kestrel rejected the client's request (e.g. body limit exceeded
                // → 413, malformed request → 400). These used to fall through to
                // a "server error" 500. Kestrel's message may contain internal
                // details, so the client gets a generic title.
                _logger.LogInformation(
                    "Client request rejected ({StatusCode}) {TraceId}: {Message}",
                    badRequest.StatusCode, traceId, badRequest.Message);

                problem = new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Title = badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                        ? _t["errors.requestTooLarge"]
                        : _t["errors.invalidRequest"],
                };
            }
            else
            {
                // Unexpected error: full details to the log, a generic message to the client.
                _logger.LogError(
                    exception,
                    "Unhandled error {TraceId} — {Path}",
                    traceId, httpContext.Request.Path);

                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = _t["errors.unexpected"],
                };
            }

            problem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = problem.Status!.Value;
            await httpContext.Response
                .WriteAsJsonAsync(problem, cancellationToken);

            return true; // handled
        }
    }
}
