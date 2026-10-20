namespace SineUyum.Api.Services.Payments
{
    // Sağlayıcının sunucu tarafında DOĞRULADIĞI satın alma. İstemcinin
    // "ödedim" demesi yetmez; sağlayıcının kendi API'sinden teyit edilir.
    public record VerifiedPurchase(
        string Provider,
        string Reference,
        DateTime PeriodEnd,
        bool WillRenew);

    public enum SubscriptionEventKind
    {
        Renewed,
        Canceled,
        Expired,
        Refunded,
    }

    // Sağlayıcının webhook'u ile gelen durum değişikliği (yenileme, iptal, iade).
    public record SubscriptionEvent(
        string Provider,
        string Reference,
        SubscriptionEventKind Kind,
        DateTime? PeriodEnd);

    // Bir ödeme sağlayıcısı (web: iyzico; mobil: App Store / Google Play).
    // Gerçek sağlayıcılar bu arayüzle eklenir; hiçbiri yapılandırılmamışsa
    // abonelik satın alınamaz (ücretsiz aktivasyon kaldırıldı).
    public interface IPaymentProvider
    {
        // URL'de ve kayıtta kullanılan ad, ör. "iyzico".
        string Name { get; }

        // İstemcinin gönderdiği ödeme/makbuz belirtecini sağlayıcıyla doğrular.
        // Geçersizse DomainException fırlatır.
        Task<VerifiedPurchase> VerifyAsync(string purchaseToken, string userId, CancellationToken cancellationToken);

        // Webhook isteğini İMZASINI DOĞRULAYARAK çözer. İmza geçersizse
        // DomainException(401); ilgilenilmeyen olaysa null.
        Task<SubscriptionEvent?> ParseWebhookAsync(HttpRequest request, CancellationToken cancellationToken);
    }
}
