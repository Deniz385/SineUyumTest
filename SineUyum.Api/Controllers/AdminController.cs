using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IStringLocalizer _t;

        public AdminController(IStringLocalizer localizer) => _t = localizer;

        // Matching no longer runs inside the request (M7): TMDb calls and large
        // events made requests time out. The pre-checks run right away (errors
        // come back as 404/400), the job is queued and 202 is returned; the
        // result is followed with GET .../matching.
        [HttpPost("events/{eventId}/create-groups")]
        public async Task<ActionResult<MatchingRunDto>> TriggerMatching(
            int eventId,
            [FromServices] MatchingService matchingService,
            [FromServices] ApplicationDbContext context,
            [FromServices] MatchingQueue queue)
        {
            await matchingService.ValidateAsync(eventId);

            var active = await context.MatchingRuns.AnyAsync(r =>
                r.CinemaEventId == eventId &&
                (r.Status == MatchingRunStatus.Queued || r.Status == MatchingRunStatus.Running));
            if (active)
            {
                throw new DomainException("matching.alreadyRunning", StatusCodes.Status409Conflict);
            }

            var run = new MatchingRun
            {
                CinemaEventId = eventId,
                RequestedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
            };
            context.MatchingRuns.Add(run);
            await context.SaveChangesAsync();
            queue.Signal();

            return AcceptedAtAction(nameof(GetMatching), new { eventId }, MatchingRunDto.From(run, _t));
        }

        // The event's latest matching job.
        [HttpGet("events/{eventId}/matching")]
        public async Task<ActionResult<MatchingRunDto>> GetMatching(int eventId, [FromServices] ApplicationDbContext context)
        {
            var run = await context.MatchingRuns
                .Where(r => r.CinemaEventId == eventId)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .FirstOrDefaultAsync();
            if (run == null) throw DomainException.NotFound("matching.notStarted");
            return Ok(MatchingRunDto.From(run, _t));
        }

        // Manual subscription (promotions, support; trying out before payments exist).
        [HttpPost("users/{userId}/subscription")]
        public async Task<ActionResult<StatusMessageDto>> GrantSubscription(
            string userId,
            [FromBody] GrantSubscriptionDto dto,
            [FromServices] SubscriptionService subscriptions)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
            await subscriptions.GrantManualAsync(userId, dto.Days, adminId);
            return Ok(new StatusMessageDto(_t["subscriptions.granted", dto.Days]));
        }
    }
}
