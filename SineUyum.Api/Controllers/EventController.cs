using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos; // <-- EKSİK OLAN SATIR BUYDU
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

        [HttpGet("my-status")]
        public async Task<IActionResult> GetMyEventStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var groupMember = await _context.EventGroupMembers
                .Where(gm => gm.UserId == userId && gm.EventGroup.CinemaEvent.EventDate >= DateTime.UtcNow)
                .OrderBy(gm => gm.EventGroup.CinemaEvent.EventDate)
                .Select(gm => new {
                    Event = gm.EventGroup.CinemaEvent,
                    Group = new {
                        gm.EventGroup.Id,
                        gm.EventGroup.SuggestedMovieIds,
                        Members = gm.EventGroup.Members.Select(m => new {
                            Id = m.UserId,
                            m.User.UserName,
                            m.User.ProfileImageUrl
                        }).ToList()
                    }
                })
                .FirstOrDefaultAsync();

            if (groupMember != null)
            {
                var votes = await _context.EventVotes
                    .Where(v => v.EventGroupId == groupMember.Group.Id)
                    .Select(v => new { v.UserId, v.MovieId }) 
                    .ToListAsync();
                
                var suggestedMovieIds = new List<int>();
                if (!string.IsNullOrWhiteSpace(groupMember.Group.SuggestedMovieIds))
                {
                    suggestedMovieIds = groupMember.Group.SuggestedMovieIds.Split(',').Select(int.Parse).ToList();
                }
                
                var suggestedMovies = await _context.Movies
                    .Where(m => suggestedMovieIds.Contains(m.Id))
                    .Select(m => new { m.Id, m.Title, m.PosterPath })
                    .ToListAsync();
                
                return Ok(new {
                    Status = "MATCHED",
                    Event = groupMember.Event,
                    Group = groupMember.Group,
                    Votes = votes,
                    SuggestedMovies = suggestedMovies
                });
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
        
        [HttpPost("vote")]
        public async Task<IActionResult> VoteForMovie([FromBody] EventVoteDto voteDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var groupMember = await _context.EventGroupMembers.AnyAsync(gm => gm.EventGroupId == voteDto.GroupId && gm.UserId == userId);
            if (!groupMember)
            {
                return Forbid("Bu grupta oy kullanma yetkiniz yok.");
            }

            var existingVote = await _context.EventVotes.FirstOrDefaultAsync(v => v.EventGroupId == voteDto.GroupId && v.UserId == userId);

            if (existingVote != null)
            {
                if (existingVote.MovieId == voteDto.MovieId)
                {
                    _context.EventVotes.Remove(existingVote);
                }
                else
                {
                    existingVote.MovieId = voteDto.MovieId;
                }
            }
            else
            {
                var newVote = new EventVote
                {
                    EventGroupId = voteDto.GroupId,
                    UserId = userId,
                    MovieId = voteDto.MovieId
                };
                await _context.EventVotes.AddAsync(newVote);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Oyunuz başarıyla kaydedildi." });
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

    public class EventVoteDto
    {
        public int GroupId { get; set; }
        public int MovieId { get; set; }
    }
}

