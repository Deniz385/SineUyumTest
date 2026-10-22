namespace SineUyum.Api.Services.Payments
{
    // A purchase VERIFIED by the provider on the server side. The client saying
    // "I paid" is not enough; it is confirmed with the provider's own API.
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

    // A state change from the provider's webhook (renewal, cancellation, refund).
    public record SubscriptionEvent(
        string Provider,
        string Reference,
        SubscriptionEventKind Kind,
        DateTime? PeriodEnd);

    // A payment provider (web: iyzico; mobile: App Store / Google Play).
    // Real providers are added through this interface; if none is configured,
    // subscriptions cannot be bought (free activation was removed).
    public interface IPaymentProvider
    {
        // Name used in the URL and the stored row, e.g. "iyzico".
        string Name { get; }

        // Verifies the payment/receipt token sent by the client with the provider.
        // Throws DomainException if it is invalid.
        Task<VerifiedPurchase> VerifyAsync(string purchaseToken, string userId, CancellationToken cancellationToken);

        // Parses the webhook request, VERIFYING ITS SIGNATURE. Invalid signature:
        // DomainException(401); an event of no interest: null.
        Task<SubscriptionEvent?> ParseWebhookAsync(HttpRequest request, CancellationToken cancellationToken);
    }
}
