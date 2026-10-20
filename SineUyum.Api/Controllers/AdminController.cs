using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        // Eşleştirme artık istek içinde çalışmaz (M7): TMDb çağrıları ve büyük
        // etkinlikler isteği zaman aşımına düşürüyordu. Ön kontrol hemen
        // yapılır (hatalar 404/400 olarak döner), iş kuyruğa alınır ve 202
        // döner; sonuç GET .../matching ile izlenir.
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
                throw new DomainException("Bu etkinlik için eşleştirme zaten sürüyor.", StatusCodes.Status409Conflict);
            }

            var run = new MatchingRun
            {
                CinemaEventId = eventId,
                RequestedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
            };
            context.MatchingRuns.Add(run);
            await context.SaveChangesAsync();
            queue.Signal();

            return AcceptedAtAction(nameof(GetMatching), new { eventId }, MatchingRunDto.From(run));
        }

        // Etkinliğin en son eşleştirme işi.
        [HttpGet("events/{eventId}/matching")]
        public async Task<ActionResult<MatchingRunDto>> GetMatching(int eventId, [FromServices] ApplicationDbContext context)
        {
            var run = await context.MatchingRuns
                .Where(r => r.CinemaEventId == eventId)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .FirstOrDefaultAsync();
            if (run == null) throw DomainException.NotFound("Bu etkinlik için eşleştirme başlatılmamış.");
            return Ok(MatchingRunDto.From(run));
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