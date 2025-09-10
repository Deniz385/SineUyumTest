// SineUyum.Api/Controllers/EventController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/event")] // <-- DEĞİŞİKLİK BURADA! Adresi küçük harflerle sabitledik.
    [Authorize]
    public class EventController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly MatchingService _matchingService;

        public EventController(ApplicationDbContext context, MatchingService matchingService)
        {
            _context = context;
            _matchingService = matchingService;
        }

        [HttpGet("my-status")]
        public async Task<IActionResult> GetMyEventStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var groupInfo = await _context.EventGroupMembers
                .Where(gm => gm.UserId == userId && gm.EventGroup.CinemaEvent.EventDate >= DateTime.UtcNow)
                .OrderBy(gm => gm.EventGroup.CinemaEvent.EventDate)
                .Select(gm => new {
                    Status = "MATCHED",
                    Event = gm.EventGroup.CinemaEvent,
                    Group = gm.EventGroup,
                    Votes = _context.EventVotes.Where(v => v.EventGroupId == gm.EventGroupId).ToList()
                }).FirstOrDefaultAsync();

            if (groupInfo != null)
            {
                var suggestedMovieIds = groupInfo.Group.SuggestedMovieIds?.Split(',').Select(int.Parse).ToList() ?? new List<int>();
                var suggestedMovies = await _context.Movies
                                                    .Where(m => suggestedMovieIds.Contains(m.Id))
                                                    .Select(m => new { m.Id, m.Title, m.PosterPath })
                                                    .ToListAsync();
                return Ok(new { groupInfo, suggestedMovies });
            }

            var participationInfo = await _context.EventParticipants
                .Where(p => p.UserId == userId && p.CinemaEvent.EventDate >= DateTime.UtcNow)
                .OrderBy(p => p.CinemaEvent.EventDate)
                .Select(p => new {
                    Status = "PENDING",
                    Event = p.CinemaEvent
                }).FirstOrDefaultAsync();

            if (participationInfo != null)
            {
                return Ok(participationInfo);
            }
            
            var nextEvent = await _context.CinemaEvents
                .Where(e => e.EventDate >= DateTime.UtcNow && !_context.EventParticipants.Any(p => p.CinemaEventId == e.Id && p.UserId == userId))
                .OrderBy(e => e.EventDate)
                .Select(e => new {
                    Status = "AVAILABLE",
                    Event = e
                }).FirstOrDefaultAsync();

            if (nextEvent != null)
            {
                return Ok(nextEvent);
            }

            return NotFound(new { message = "Yaklaşan bir etkinlik bulunmuyor veya zaten birine katılmışsınız." });
        }
        
        [HttpPost("{eventId}/join")]
        public async Task<IActionResult> JoinEvent(int eventId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var alreadyJoined = await _context.EventParticipants.AnyAsync(p => p.UserId == userId && p.CinemaEvent.EventDate >= DateTime.UtcNow);
            if(alreadyJoined)
            {
                return BadRequest(new { message = "Zaten yaklaşan bir etkinlik için bekleme listesindesiniz."});
            }

            var participant = new EventParticipant
            {
                UserId = userId,
                CinemaEventId = eventId
            };
            await _context.EventParticipants.AddAsync(participant);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Etkinliğe başarıyla katıldınız! Eşleştirme için beklemede kalın."});
        }
        
        // ... Diğer metodlar (CreateEvent, CreateGroups, Vote, GetEventById) ...
    }
    
    // ... DTO ...
}