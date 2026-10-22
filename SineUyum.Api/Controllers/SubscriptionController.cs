using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
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
        private readonly IStringLocalizer _t;

        public SubscriptionController(
            UserManager<AppUser> userManager,
            TokenService tokenService,
            SubscriptionService subscriptions,
            IEnumerable<IPaymentProvider> providers,
            IStringLocalizer localizer)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _subscriptions = subscriptions;
            _providers = providers;
            _t = localizer;
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

        // Activation without payment was REMOVED (G4): anyone could subscribe
        // with a single request. Old clients get an understandable error.
        [HttpPost("activate")]
        public IActionResult ActivateSubscription() =>
            Problem(
                title: _t["subscriptions.freeActivationRemoved"],
                statusCode: StatusCodes.Status410Gone);

        // After completing the payment at the provider, the client sends the
        // token it received (iyzico payment token, App Store/Google Play
        // receipt); the server verifies it WITH THE PROVIDER. The response
        // carries a new token so the subscription claim updates right away.
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

            // The provider writes its own name; what the client says is not trusted.
            if (purchase.Provider != provider.Name)
            {
                throw new InvalidOperationException("The provider returned a different name.");
            }

            await _subscriptions.ApplyVerifiedPurchaseAsync(userId, purchase);
            return Ok(await _tokenService.CreateAuthResponseAsync(user, Request.Headers.UserAgent.ToString()));
        }

        // Provider-to-server notifications (renewal, cancellation, refund). There
        // is no authentication; trust rests on the provider's SIGNATURE, which
        // the provider class verifies.
        [HttpPost("webhooks/{provider}")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook(string provider, CancellationToken ct)
        {
            var handler = _providers.FirstOrDefault(p => p.Name == provider);
            if (handler == null) return NotFound();

            var change = await handler.ParseWebhookAsync(Request, ct);
            if (change != null)
            {
                if (change.Provider != handler.Name) throw new InvalidOperationException("The provider returned a different name.");
                await _subscriptions.ApplyProviderEventAsync(change);
            }
            return Ok();
        }

        private IPaymentProvider FindProvider(string name)
        {
            if (!_providers.Any())
            {
                throw new DomainException(
                    "subscriptions.paymentsNotConfigured",
                    StatusCodes.Status503ServiceUnavailable);
            }
            return _providers.FirstOrDefault(p => p.Name == name)
                ?? throw new DomainException("subscriptions.unknownProvider");
        }
    }
}
