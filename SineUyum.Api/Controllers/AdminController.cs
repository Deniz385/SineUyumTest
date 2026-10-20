using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SineUyum.Api.Dtos;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        [HttpPost("events/{eventId}/create-groups")]
        public async Task<ActionResult<StatusMessageDto>> TriggerMatching(int eventId, [FromServices] MatchingService matchingService)
        {
            // Hata yönetimi global handler'a bırakıldı: iş kuralı hataları
            // (DomainException) doğru durum koduyla, beklenmeyen hatalar ise
            // detay sızdırmadan 500 ile döner. Önceden ex.Message doğrudan
            // istemciye yazılıyordu.
            await matchingService.CreateGroupsForEventAsync(eventId);
            return Ok(new StatusMessageDto("Eşleştirme başarıyla tamamlandı ve gruplar oluşturuldu."));
        }

        // Elle abonelik (promosyon, destek; ödeme entegrasyonu öncesi deneme).
        [HttpPost("users/{userId}/subscription")]
        public async Task<ActionResult<StatusMessageDto>> GrantSubscription(
            string userId,
            [FromBody] GrantSubscriptionDto dto,
            [FromServices] SubscriptionService subscriptions)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
            await subscriptions.GrantManualAsync(userId, dto.Days, adminId);
            return Ok(new StatusMessageDto($"{dto.Days} günlük abonelik tanımlandı."));
        }
    }
}