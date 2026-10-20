using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using SineUyum.Api.Services.Payments;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly TokenService _tokenService;
        private readonly SubscriptionService _subscriptions;
        private readonly IEnumerable<IPaymentProvider> _providers;

        public SubscriptionController(
            UserManager<AppUser> userManager,
            TokenService tokenService,
            SubscriptionService subscriptions,
            IEnumerable<IPaymentProvider> providers)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _subscriptions = subscriptions;
            _providers = providers;
        }

        [HttpGet]
        public async Task<ActionResult<SubscriptionStatusDto>> GetStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var state = await _subscriptions.GetStateAsync(userId);
            return Ok(new SubscriptionStatusDto
            {
                IsActive = state.IsActive,
                ExpiresAt = state.ExpiresAt,
                Provider = state.Provider,
                WillRenew = state.WillRenew,
                AvailableProviders = _providers.Select(p => p.Name).ToList(),
            });
        }

        // Ödemesiz aktivasyon KALDIRILDI (G4): herkes tek istekle abone
        // olabiliyordu. Eski istemciler anlaşılır bir hata alır.
        [HttpPost("activate")]
        public IActionResult ActivateSubscription() =>
            Problem(
                title: "Ödemesiz abonelik kaldırıldı. Abonelik ödeme ile başlatılır.",
                statusCode: StatusCodes.Status410Gone);

        // İstemci ödemeyi sağlayıcıda tamamladıktan sonra aldığı belirteci
        // (iyzico ödeme belirteci, App Store/Google Play makbuzu) gönderir;
        // sunucu bunu SAĞLAYICIYLA doğrular. Yanıt yeni token içerir ki
        // abonelik claim'i hemen güncellensin.
        [HttpPost("verify")]
        [EnableRateLimiting("payment")]
        public async Task<ActionResult<AuthResponseDto>> Verify([FromBody] VerifyPurchaseDto dto, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            var provider = FindProvider(dto.Provider);
            var purchase = await provider.VerifyAsync(dto.PurchaseToken, userId, ct);

            // Sağlayıcı kendi adını yazar; istemcinin söylediğine güvenilmez.
            if (purchase.Provider != provider.Name)
            {
                throw new InvalidOperationException("Sağlayıcı farklı bir ad döndürdü.");
            }

            await _subscriptions.ApplyVerifiedPurchaseAsync(userId, purchase);
            return Ok(await _tokenService.CreateAuthResponseAsync(user, Request.Headers.UserAgent.ToString()));
        }

        // Sağlayıcıdan sunucuya bildirim (yenileme, iptal, iade). Kimlik
        // doğrulaması yoktur; güven sağlayıcının İMZASINA dayanır ve imza
        // sağlayıcı sınıfında doğrulanır.
        [HttpPost("webhooks/{provider}")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook(string provider, CancellationToken ct)
        {
            var handler = _providers.FirstOrDefault(p => p.Name == provider);
            if (handler == null) return NotFound();

            var change = await handler.ParseWebhookAsync(Request, ct);
            if (change != null)
            {
                if (change.Provider != handler.Name) throw new InvalidOperationException("Sağlayıcı farklı bir ad döndürdü.");
                await _subscriptions.ApplyProviderEventAsync(change);
            }
            return Ok();
        }

        private IPaymentProvider FindProvider(string name)
        {
            if (!_providers.Any())
            {
                throw new DomainException(
                    "Ödeme henüz yapılandırılmadı; şu an abonelik satın alınamıyor.",
                    StatusCodes.Status503ServiceUnavailable);
            }
            return _providers.FirstOrDefault(p => p.Name == name)
                ?? throw new DomainException("Bilinmeyen ödeme sağlayıcısı.");
        }
    }
}
