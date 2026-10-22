using System.Net.Http.Headers;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

namespace SineUyum.Api.Services.Push
{
    public class FcmOptions
    {
        public string ProjectId { get; set; } = string.Empty;
    }

    // OAuth2 access token for FCM HTTP v1. Replaced with a fake in tests.
    public interface IFcmAccessTokenProvider
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
    }

    // Creates tokens from the service account key (Firebase console → Project
    // settings → Service accounts). The library caches the token and renews it
    // before it expires.
    public sealed class GoogleFcmAccessTokenProvider : IFcmAccessTokenProvider
    {
        private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
        private readonly ITokenAccess _credential;

        public GoogleFcmAccessTokenProvider(string serviceAccountJson)
        {
            // Only service account keys are accepted (the generic FromJson is not used
            // because it also loads untrusted credential types).
            _credential = CredentialFactory.FromJson<ServiceAccountCredential>(serviceAccountJson)
                .ToGoogleCredential()
                .CreateScoped(Scope);
        }

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            _credential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
    }

    // FCM HTTP v1: https://firebase.google.com/docs/cloud-messaging/send-message
    // iOS also goes through FCM (the APNs key is uploaded to the Firebase console).
    public sealed class FcmPushSender : IPushSender
    {
        public const string HttpClientName = "FCM";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IFcmAccessTokenProvider _tokenProvider;
        private readonly FcmOptions _options;
        private readonly ILogger<FcmPushSender> _logger;

        public FcmPushSender(
            IHttpClientFactory httpClientFactory,
            IFcmAccessTokenProvider tokenProvider,
            FcmOptions options,
            ILogger<FcmPushSender> logger)
        {
            _httpClientFactory = httpClientFactory;
            _tokenProvider = tokenProvider;
            _options = options;
            _logger = logger;
        }

        public bool IsEnabled => true;

        public async Task<PushSendResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken)
        {
            var body = new
            {
                message = new
                {
                    token = deviceToken,
                    notification = new { title = message.Title, body = message.Body },
                    data = message.Data,
                    // Reaches Android devices in Doze mode without delay too.
                    android = new { priority = "high" },
                    apns = new { payload = new { aps = new { sound = "default" } } },
                },
            };

            var url = $"https://fcm.googleapis.com/v1/projects/{_options.ProjectId}/messages:send";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body, options: Json),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", await _tokenProvider.GetAccessTokenAsync(cancellationToken));

            using var response = await _httpClientFactory.CreateClient(HttpClientName)
                .SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode) return PushSendResult.Sent;

            var errorCode = await ReadFcmErrorCodeAsync(response, cancellationToken);

            // Only UNREGISTERED reliably means "the token is dead". INVALID_ARGUMENT can
            // be a broken token or a bug in our payload; in the latter case the row is
            // kept and a warning logged so we do not delete everyone's registration.
            if (errorCode == "UNREGISTERED") return PushSendResult.InvalidToken;

            _logger.LogWarning("FCM send failed: {Status} {ErrorCode}",
                (int)response.StatusCode, errorCode ?? "-");
            return PushSendResult.Failed;
        }

        // Error body: {"error":{"status":"NOT_FOUND","details":[{"@type":"...FcmError","errorCode":"UNREGISTERED"}]}}
        private static async Task<string?> ReadFcmErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
        {
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (!doc.RootElement.TryGetProperty("error", out var error)) return null;

                if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        if (detail.TryGetProperty("errorCode", out var code)) return code.GetString();
                    }
                }
                return error.TryGetProperty("status", out var status) ? status.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
