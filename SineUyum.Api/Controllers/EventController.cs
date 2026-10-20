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

        [HttpGet("my-status")]
        public async Task<ActionResult<EventStatusDto>> GetMyEventStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var groupMember = await _context.EventGroupMembers
                .Where(gm => gm.UserId == userId && gm.EventGroup.CinemaEvent.EventDate >= DateTime.UtcNow)
                .OrderBy(gm => gm.EventGroup.CinemaEvent.EventDate)
                .Select(gm => new
                {
                    Event = new CinemaEventDto
                    {
                        Id = gm.EventGroup.CinemaEvent.Id,
                        EventDate = gm.EventGroup.CinemaEvent.EventDate,
                        LocationName = gm.EventGroup.CinemaEvent.LocationName,
                        Address = gm.EventGroup.CinemaEvent.Address,
                        GroupSize = gm.EventGroup.CinemaEvent.GroupSize,
                        SelectedMovieId = gm.EventGroup.CinemaEvent.SelectedMovieId
                    },
                    Group = new EventGroupDto
                    {
                        Id = gm.EventGroup.Id,
                        SuggestedMovieIds = gm.EventGroup.SuggestedMovieIds,
                        Members = gm.EventGroup.Members.Select(m => new EventGroupMemberDto
                        {
                            Id = m.UserId,
                            UserName = m.User.UserName,
                            ProfileImageUrl = m.User.ProfileImageUrl
                        }).ToList()
                    }
                })
                .FirstOrDefaultAsync();

            if (groupMember != null)
            {
                var votes = await _context.EventVotes
                    .Where(v => v.EventGroupId == groupMember.Group.Id)
                    .Select(v => new EventGroupVoteDto { UserId = v.UserId, MovieId = v.MovieId })
                    .ToListAsync();
                
                var suggestedMovieIds = new List<int>();
                if (!string.IsNullOrWhiteSpace(groupMember.Group.SuggestedMovieIds))
                {
                    suggestedMovieIds = groupMember.Group.SuggestedMovieIds.Split(',').Select(int.Parse).ToList();
                }
                
                var suggestedMovies = await _context.Movies
                    .Where(m => suggestedMovieIds.Contains(m.Id))
                    .Select(m => new MovieSummaryDto { Id = m.Id, Title = m.Title, PosterPath = m.PosterPath })
                    .ToListAsync();
                
                return Ok(new EventStatusDto
                {
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
                return Ok(new EventStatusDto
                {
                    Status = "PENDING",
                    Event = CinemaEventDto.From(participant.CinemaEvent)
                });
            }

            var nextEvent = await _context.CinemaEvents
                .Where(e => e.EventDate >= DateTime.UtcNow && !_context.EventParticipants.Any(p => p.CinemaEventId == e.Id && p.UserId == userId))
                .OrderBy(e => e.EventDate)
                .FirstOrDefaultAsync();

            if (nextEvent != null)
            {
                return Ok(new EventStatusDto
                {
                    Status = "AVAILABLE",
                    Event = CinemaEventDto.From(nextEvent)
                });
            }
            
            return Ok(new EventStatusDto { Status = "NONE", Message = "Katılabileceğin yeni bir etkinlik bulunmuyor." });
        }
        
        [HttpPost("vote")]
        public async Task<ActionResult<StatusMessageDto>> VoteForMovie([FromBody] EventVoteDto voteDto)
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
            return Ok(new StatusMessageDto("Oyunuz başarıyla kaydedildi."));
        }
        
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<CinemaEventDto>> CreateEvent([FromBody] CreateEventDto createEventDto)
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

            return CreatedAtAction(nameof(GetMyEventStatus), new { id = newEvent.Id }, CinemaEventDto.From(newEvent));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<EventListItemDto>>> GetAllEvents()
        {
            var events = await _context.CinemaEvents
                .OrderByDescending(e => e.EventDate)
                .Select(e => new EventListItemDto
                {
                    Id = e.Id,
                    LocationName = e.LocationName,
                    EventDate = e.EventDate,
                    GroupSize = e.GroupSize,
                    ParticipantCount = _context.EventParticipants.Count(p => p.CinemaEventId == e.Id)
                })
                .ToListAsync();

            return Ok(events);
        }
        
        [HttpPost("{eventId}/join")]
        public async Task<ActionResult<StatusMessageDto>> JoinEvent(int eventId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var alreadyJoined = await _context.EventParticipants.AnyAsync(p => p.UserId == userId && p.CinemaEvent.EventDate >= DateTime.UtcNow);
            if(alreadyJoined)
            {
                return BadRequest(new StatusMessageDto("Zaten yaklaşan bir etkinlik için bekleme listesindesiniz."));
            }

            var participant = new EventParticipant
            {
                UserId = userId,
                CinemaEventId = eventId
            };
            await _context.EventParticipants.AddAsync(participant);
            await _context.SaveChangesAsync();

            return Ok(new StatusMessageDto("Etkinliğe başarıyla katıldınız! Eşleştirme için beklemede kalın."));
        }
        
        [HttpPut("{eventId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<StatusMessageDto>> UpdateEvent(int eventId, [FromBody] CreateEventDto updateDto)
        {
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) return NotFound("Güncellenecek etkinlik bulunamadı.");
            
            cinemaEvent.LocationName = updateDto.LocationName;
            cinemaEvent.Address = updateDto.Address;
            cinemaEvent.EventDate = updateDto.EventDate;
            cinemaEvent.GroupSize = updateDto.GroupSize;
            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto("Etkinlik başarıyla güncellendi."));
        }

        [HttpDelete("{eventId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<StatusMessageDto>> DeleteEvent(int eventId)
        {
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) return NotFound("Silinecek etkinlik bulunamadı.");
            
            _context.CinemaEvents.Remove(cinemaEvent);
            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto("Etkinlik başarıyla silindi."));
        }
    }
}

