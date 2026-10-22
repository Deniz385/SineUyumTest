using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using SineUyum.Api.Services.Payments;

namespace SineUyum.Api.Services
{
    public record SubscriptionState(bool IsActive, DateTime? ExpiresAt, string? Provider, bool WillRenew);

    // The single decision point for the subscription state. "Is subscribed?" is
    // always answered from the database by the end date (the JWT claim is only
    // for the client to display; it can be at most one access token lifetime
    // out of date).
    public class SubscriptionService
    {
        public const string ManualProvider = "manual";

        private readonly ApplicationDbContext _context;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(ApplicationDbContext context, ILogger<SubscriptionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SubscriptionState> GetStateAsync(string userId)
        {
            var now = DateTime.UtcNow;
            // The entitling subscription that ends last wins.
            var current = await _context.Subscriptions
                .Where(s => s.UserId == userId
                            && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Canceled)
                            && s.CurrentPeriodEnd > now)
                .OrderByDescending(s => s.CurrentPeriodEnd)
                .FirstOrDefaultAsync();

            return current == null
                ? new SubscriptionState(false, null, null, false)
                : new SubscriptionState(true, current.CurrentPeriodEnd, current.Provider,
                    current.Status == SubscriptionStatus.Active);
        }

        public async Task<bool> IsActiveAsync(string userId) => (await GetStateAsync(userId)).IsActive;

        // Links a purchase verified by the provider to the user. The same purchase
        // sent again (client retried) is updated; if it belongs to another account it
        // is rejected — one payment cannot unlock two accounts.
        public async Task ApplyVerifiedPurchaseAsync(string userId, VerifiedPurchase purchase)
        {
            var now = DateTime.UtcNow;
            if (purchase.PeriodEnd <= now)
            {
                throw new DomainException("subscriptions.purchaseExpired");
            }

            var existing = await _context.Subscriptions.FirstOrDefaultAsync(s =>
                s.Provider == purchase.Provider && s.ProviderReference == purchase.Reference);

            if (existing != null && existing.UserId != userId)
            {
                _logger.LogWarning(
                    "Purchase belongs to another account: {Provider}/{Reference} (requested by {UserId})",
                    purchase.Provider, purchase.Reference, userId);
                throw new DomainException("subscriptions.purchaseLinkedToOtherAccount", StatusCodes.Status409Conflict);
            }

            if (existing == null)
            {
                existing = new Subscription
                {
                    UserId = userId,
                    Provider = purchase.Provider,
                    ProviderReference = purchase.Reference,
                    StartedAt = now,
                    CreatedAt = now,
                };
                _context.Subscriptions.Add(existing);
            }

            existing.Status = purchase.WillRenew ? SubscriptionStatus.Active : SubscriptionStatus.Canceled;
            existing.CurrentPeriodEnd = purchase.PeriodEnd;
            existing.CanceledAt = purchase.WillRenew ? null : existing.CanceledAt ?? now;
            existing.UpdatedAt = now;
            await _context.SaveChangesAsync();
        }

        // The provider's webhook (renewal, cancellation, expiry, refund).
        // Events for an unknown purchase are ignored (returning normally so the
        // provider does not keep resending them).
        public async Task ApplyProviderEventAsync(SubscriptionEvent change)
        {
            var subscription = await _context.Subscriptions.FirstOrDefaultAsync(s =>
                s.Provider == change.Provider && s.ProviderReference == change.Reference);
            if (subscription == null)
            {
                _logger.LogWarning("Event for an unknown subscription: {Provider}/{Reference} {Kind}",
                    change.Provider, change.Reference, change.Kind);
                return;
            }

            var now = DateTime.UtcNow;
            switch (change.Kind)
            {
                case SubscriptionEventKind.Renewed:
                    subscription.Status = SubscriptionStatus.Active;
                    subscription.CanceledAt = null;
                    if (change.PeriodEnd is { } renewedUntil) subscription.CurrentPeriodEnd = renewedUntil;
                    break;
                case SubscriptionEventKind.Canceled:
                    // Usable until the end of the paid period.
                    subscription.Status = SubscriptionStatus.Canceled;
                    subscription.CanceledAt ??= now;
                    break;
                case SubscriptionEventKind.Expired:
                    subscription.Status = SubscriptionStatus.Expired;
                    break;
                case SubscriptionEventKind.Refunded:
                    // Refund: the entitlement ends immediately.
                    subscription.Status = SubscriptionStatus.Refunded;
                    subscription.CurrentPeriodEnd = now;
                    break;
            }
            subscription.UpdatedAt = now;
            await _context.SaveChangesAsync();
        }

        // Admin-granted subscription (promotions, support, trying premium features
        // before payment integration).
        public async Task GrantManualAsync(string userId, int days, string grantedBy)
        {
            if (days is < 1 or > 366) throw new DomainException("subscriptions.invalidDays");
            if (!await _context.Users.AnyAsync(u => u.Id == userId)) throw DomainException.NotFound("errors.userNotFound");

            var now = DateTime.UtcNow;
            _context.Subscriptions.Add(new Subscription
            {
                UserId = userId,
                Provider = ManualProvider,
                ProviderReference = $"{grantedBy}:{Guid.NewGuid():N}",
                Status = SubscriptionStatus.Canceled, // does not renew
                StartedAt = now,
                CurrentPeriodEnd = now.AddDays(days),
                CanceledAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await _context.SaveChangesAsync();
            _logger.LogInformation("Manual subscription: {UserId} {Days} days (granted by {AdminId})", userId, days, grantedBy);
        }
    }
}
