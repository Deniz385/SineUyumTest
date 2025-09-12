using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SineUyum.Api.Services;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        [HttpPost("events/{eventId}/create-groups")]
        public async Task<IActionResult> TriggerMatching(int eventId, [FromServices] MatchingService matchingService)
        {
            try
            {
                await matchingService.CreateGroupsForEventAsync(eventId);
                return Ok(new { message = "Eşleştirme başarıyla tamamlandı ve gruplar oluşturuldu." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Eşleştirme sırasında bir hata oluştu: {ex.Message}" });
            }
        }
    }
}