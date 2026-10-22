namespace SineUyum.Api.Services.Push
{
    // A single push notification for all of a user's devices.
    // Data values are strings, as FCM requires.
    public record PushMessage(
        string UserId,
        string Title,
        string Body,
        IReadOnlyDictionary<string, string> Data);

    public enum PushSendResult
    {
        Sent,

        // The FCM token is no longer valid (app uninstalled, token refreshed...).
        // The row should be deleted.
        InvalidToken,

        // A temporary or unknown error; the row is kept.
        Failed,
    }

    // Sending to a single device token. The real implementation is FCM HTTP v1;
    // NullPushSender is used when it is not configured.
    public interface IPushSender
    {
        bool IsEnabled { get; }

        Task<PushSendResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken);
    }

    // Without credentials push is silently disabled; the app (notifications over
    // REST + SignalR) keeps working as before.
    public sealed class NullPushSender : IPushSender
    {
        public bool IsEnabled => false;

        public Task<PushSendResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken) =>
            Task.FromResult(PushSendResult.Failed);
    }
}
