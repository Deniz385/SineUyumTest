using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SineUyum.Api.Dtos;
using SineUyum.Api.Services;

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
    }
}