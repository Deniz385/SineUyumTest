namespace SineUyum.Api.Services.Push
{
    // Bir kullanıcının tüm cihazlarına gidecek tek push bildirimi.
    // Data değerleri FCM gereği string'dir.
    public record PushMessage(
        string UserId,
        string Title,
        string Body,
        IReadOnlyDictionary<string, string> Data);

    public enum PushSendResult
    {
        Sent,

        // FCM token'ı artık geçersiz (uygulama kaldırıldı, token yenilendi...).
        // Kayıt silinmeli.
        InvalidToken,

        // Geçici ya da bilinmeyen hata; kayıt korunur.
        Failed,
    }

    // Tek bir cihaz token'ına gönderim. Gerçek uygulama FCM HTTP v1'dir;
    // yapılandırılmamışsa NullPushSender kullanılır.
    public interface IPushSender
    {
        bool IsEnabled { get; }

        Task<PushSendResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken);
    }

    // Kimlik bilgisi tanımlı değilse push sessizce devre dışıdır; uygulama
    // (bildirimler REST + SignalR) aynen çalışmaya devam eder.
    public sealed class NullPushSender : IPushSender
    {
        public bool IsEnabled => false;

        public Task<PushSendResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken) =>
            Task.FromResult(PushSendResult.Failed);
    }
}
