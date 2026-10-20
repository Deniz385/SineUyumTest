using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using SineUyum.Api.Services.Payments;

namespace SineUyum.Api.Services
{
    public record SubscriptionState(bool IsActive, DateTime? ExpiresAt, string? Provider, bool WillRenew);

    // Abonelik durumunun tek karar noktası. "Abone mi?" sorusu her zaman
    // veritabanından ve bitiş tarihine bakılarak cevaplanır (JWT'deki claim
    // yalnızca istemcinin gösterimi içindir; en fazla access token ömrü kadar
    // eski olabilir).
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
            // Hak veren en geç biten abonelik esas alınır.
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

        // Sağlayıcının doğruladığı satın almayı kullanıcıya bağlar. Aynı satın
        // alma tekrar gönderilirse (istemci yeniden denedi) güncellenir; başka
        // bir hesaba bağlıysa reddedilir — bir ödeme iki hesabı açamaz.
        public async Task ApplyVerifiedPurchaseAsync(string userId, VerifiedPurchase purchase)
        {
            var now = DateTime.UtcNow;
            if (purchase.PeriodEnd <= now)
            {
                throw new DomainException("Bu satın almanın süresi dolmuş.");
            }

            var existing = await _context.Subscriptions.FirstOrDefaultAsync(s =>
                s.Provider == purchase.Provider && s.ProviderReference == purchase.Reference);

            if (existing != null && existing.UserId != userId)
            {
                _logger.LogWarning(
                    "Satın alma başka hesaba bağlı: {Provider}/{Reference} (istenen {UserId})",
                    purchase.Provider, purchase.Reference, userId);
                throw new DomainException("Bu satın alma başka bir hesaba bağlı.", StatusCodes.Status409Conflict);
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

        // Sağlayıcının webhook'u (yenileme, iptal, süre bitimi, iade).
        // Bilinmeyen bir satın alma için gelen olay yok sayılır (true döner ki
        // sağlayıcı tekrar tekrar göndermesin).
        public async Task ApplyProviderEventAsync(SubscriptionEvent change)
        {
            var subscription = await _context.Subscriptions.FirstOrDefaultAsync(s =>
                s.Provider == change.Provider && s.ProviderReference == change.Reference);
            if (subscription == null)
            {
                _logger.LogWarning("Bilinmeyen abonelik için olay: {Provider}/{Reference} {Kind}",
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
                    // Ödenen dönem sonuna kadar kullanılabilir.
                    subscription.Status = SubscriptionStatus.Canceled;
                    subscription.CanceledAt ??= now;
                    break;
                case SubscriptionEventKind.Expired:
                    subscription.Status = SubscriptionStatus.Expired;
                    break;
                case SubscriptionEventKind.Refunded:
                    // İade: hak hemen biter.
                    subscription.Status = SubscriptionStatus.Refunded;
                    subscription.CurrentPeriodEnd = now;
                    break;
            }
            subscription.UpdatedAt = now;
            await _context.SaveChangesAsync();
        }

        // Yönetici tanımlı abonelik (promosyon, destek, ödeme entegrasyonundan
        // önce premium özellikleri denemek).
        public async Task GrantManualAsync(string userId, int days, string grantedBy)
        {
            if (days is < 1 or > 366) throw new DomainException("Gün sayısı 1 ile 366 arasında olmalı.");
            if (!await _context.Users.AnyAsync(u => u.Id == userId)) throw DomainException.NotFound("Kullanıcı bulunamadı.");

            var now = DateTime.UtcNow;
            _context.Subscriptions.Add(new Subscription
            {
                UserId = userId,
                Provider = ManualProvider,
                ProviderReference = $"{grantedBy}:{Guid.NewGuid():N}",
                Status = SubscriptionStatus.Canceled, // yenilenmez
                StartedAt = now,
                CurrentPeriodEnd = now.AddDays(days),
                CanceledAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await _context.SaveChangesAsync();
            _logger.LogInformation("Elle abonelik: {UserId} {Days} gün (veren {AdminId})", userId, days, grantedBy);
        }
    }
}
