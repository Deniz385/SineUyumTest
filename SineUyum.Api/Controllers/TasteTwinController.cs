using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SineUyum.Api.Dtos;
using SineUyum.Api.Services;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/taste-twin")]
    [Authorize]
    public class TasteTwinController : ControllerBase
    {
        private readonly TasteTwinService _twins;

        public TasteTwinController(TasteTwinService twins) => _twins = twins;

        // Bu haftanın zevk ikizi; henüz yoksa şimdi hesaplanır (yeni kullanıcı
        // haftalık işi beklemesin). Uygun aday yoksa 204.
        [HttpGet]
        public async Task<ActionResult<TasteTwinDto>> Get()
        {
            var me = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var dto = await _twins.ToDtoAsync(await _twins.ForThisWeekAsync(me, DateTime.UtcNow), me);
            return dto == null ? NoContent() : Ok(dto);
        }
    }
}
