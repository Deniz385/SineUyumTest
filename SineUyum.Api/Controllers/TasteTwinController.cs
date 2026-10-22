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

        // This week's taste twin; computed now if there is none yet (a new
        // user should not wait for the weekly job). 204 if no candidate fits.
        [HttpGet]
        public async Task<ActionResult<TasteTwinDto>> Get()
        {
            var me = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var dto = await _twins.ToDtoAsync(await _twins.ForThisWeekAsync(me, DateTime.UtcNow), me);
            return dto == null ? NoContent() : Ok(dto);
        }
    }
}
