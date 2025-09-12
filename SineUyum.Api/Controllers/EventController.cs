using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/event")]
    [Authorize]
    public class EventController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EventController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto createEventDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var newEvent = new CinemaEvent
            {
                EventDate = createEventDto.EventDate,
                LocationName = createEventDto.LocationName,
                Address = createEventDto.Address,
                GroupSize = createEventDto.GroupSize
            };

            await _context.CinemaEvents.AddAsync(newEvent);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMyEventStatus), new { id = newEvent.Id }, newEvent);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllEvents()
        {
            var events = await _context.CinemaEvents
                .OrderByDescending(e => e.EventDate)
                .Select(e => new {
                    e.Id,
                    e.LocationName,
                    e.EventDate,
                    e.GroupSize,
                    ParticipantCount = _context.EventParticipants.Count(p => p.CinemaEventId == e.Id)
                })
                .ToListAsync();

            return Ok(events);
        }

        [HttpGet("my-status")]
        public async Task<IActionResult> GetMyEventStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var groupMember = await _context.EventGroupMembers
                .Include(gm => gm.EventGroup)
                    .ThenInclude(eg => eg.CinemaEvent)
                .Include(gm => gm.EventGroup)
                    .ThenInclude(eg => eg.Members)
                        .ThenInclude(m => m.User)
                .Where(gm => gm.UserId == userId && gm.EventGroup.CinemaEvent.EventDate >= DateTime.UtcNow)
                .OrderBy(gm => gm.EventGroup.CinemaEvent.EventDate)
                .FirstOrDefaultAsync();

            if (groupMember != null)
            {
                var group = groupMember.EventGroup;
                var votes = await _context.EventVotes.Where(v => v.EventGroupId == group.Id).ToListAsync();
                
                var suggestedMovieIds = new List<int>();
                if (!string.IsNullOrWhiteSpace(group.SuggestedMovieIds))
                {
                    suggestedMovieIds = group.SuggestedMovieIds.Split(',').Select(int.Parse).ToList();
                }
                
                var suggestedMovies = await _context.Movies
                    .Where(m => suggestedMovieIds.Contains(m.Id))
                    .Select(m => new { m.Id, m.Title, m.PosterPath })
                    .ToListAsync();
                
                var groupInfoResponse = new {
                    Status = "MATCHED",
                    Event = group.CinemaEvent,
                    Group = new {
                        group.Id,
                        group.SuggestedMovieIds,
                        Members = group.Members.Select(m => new { Id = m.UserId, m.User.UserName, m.User.ProfileImageUrl }).ToList()
                    },
                    Votes = votes,
                    SuggestedMovies = suggestedMovies
                };

                // --- DÜZELTME BURADA ---
                // Yanıtı fazladan bir katmana sarmadan, doğrudan gönderiyoruz.
                return Ok(new { groupInfo = groupInfoResponse });
            }

            var participant = await _context.EventParticipants
                .Include(p => p.CinemaEvent)
                .Where(p => p.UserId == userId && p.CinemaEvent.EventDate >= DateTime.UtcNow)
                .OrderBy(p => p.CinemaEvent.EventDate)
                .FirstOrDefaultAsync();
            
            if (participant != null)
            {
                return Ok(new {
                    Status = "PENDING",
                    Event = participant.CinemaEvent
                });
            }

            var nextEvent = await _context.CinemaEvents
                .Where(e => e.EventDate >= DateTime.UtcNow && !_context.EventParticipants.Any(p => p.CinemaEventId == e.Id && p.UserId == userId))
                .OrderBy(e => e.EventDate)
                .FirstOrDefaultAsync();

            if (nextEvent != null)
            {
                return Ok(new {
                    Status = "AVAILABLE",
                    Event = nextEvent
                });
            }
            
            return Ok(new { Status = "NONE", Message = "Katılabileceğin yeni bir etkinlik bulunmuyor." });
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
        
        [HttpPut("{eventId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateEvent(int eventId, [FromBody] CreateEventDto updateDto)
        {
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) return NotFound("Güncellenecek etkinlik bulunamadı.");
            
            cinemaEvent.LocationName = updateDto.LocationName;
            cinemaEvent.Address = updateDto.Address;
            cinemaEvent.EventDate = updateDto.EventDate;
            cinemaEvent.GroupSize = updateDto.GroupSize;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Etkinlik başarıyla güncellendi." });
        }

        [HttpDelete("{eventId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEvent(int eventId)
        {
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) return NotFound("Silinecek etkinlik bulunamadı.");
            
            _context.CinemaEvents.Remove(cinemaEvent);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Etkinlik başarıyla silindi." });
        }
    }

    // DTO'yu buraya ekleyerek bağımlılık sorununu önleyelim
    public class CreateEventDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        public System.DateTime EventDate { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(200)]
        public string LocationName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.MaxLength(500)]
        public string? Address { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Range(2, 10)]
        public int GroupSize { get; set; }
    }
}