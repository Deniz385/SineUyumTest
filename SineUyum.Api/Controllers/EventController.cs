using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Filters;
using SineUyum.Api.Localization;
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
        private readonly IStringLocalizer _t;

        public EventController(ApplicationDbContext context, IStringLocalizer localizer)
        {
            _context = context;
            _t = localizer;
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
                
                var lang = SupportedLanguages.Current;
                var suggestedMovies = await _context.EventGroupSuggestions
                    .Where(s => s.EventGroupId == groupMember.Group.Id)
                    .OrderBy(s => s.Rank)
                    .Select(s => new MovieSummaryDto
                    {
                        Id = s.Movie.Id,
                        Title = s.Movie.Translations.Where(t => t.Language == lang).Select(t => t.Title).FirstOrDefault() ?? s.Movie.Title,
                        PosterPath = s.Movie.Translations.Where(t => t.Language == lang).Select(t => t.PosterPath).FirstOrDefault() ?? s.Movie.PosterPath
                    })
                    .ToListAsync();

                // Backward compatibility: "1,2,3" in the same order for old clients.
                groupMember.Group.SuggestedMovieIds = string.Join(",", suggestedMovies.Select(m => m.Id));
                
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
            
            return Ok(new EventStatusDto { Status = "NONE", Message = _t["events.noUpcoming"] });
        }
        
        [HttpPost("vote")]
        [RequiresSubscription]
        public async Task<ActionResult<StatusMessageDto>> VoteForMovie([FromBody] EventVoteDto voteDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var groupMember = await _context.EventGroupMembers.AnyAsync(gm => gm.EventGroupId == voteDto.GroupId && gm.UserId == userId);
            if (!groupMember)
            {
                // Forbid("...") took the text for an authentication scheme and returned 500.
                throw new DomainException("events.notGroupMember", StatusCodes.Status403Forbidden);
            }

            // Only the group's suggested movies can be voted for. Any id used to
            // be accepted (a missing movie gave a 500 via the FK error).
            var isSuggested = await _context.EventGroupSuggestions
                .AnyAsync(s => s.EventGroupId == voteDto.GroupId && s.MovieId == voteDto.MovieId);
            if (!isSuggested)
            {
                return BadRequest(new StatusMessageDto(_t["events.movieNotSuggested"]));
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
            return Ok(new StatusMessageDto(_t["events.voteSaved"]));
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
        [RequiresSubscription]
        public async Task<ActionResult<StatusMessageDto>> JoinEvent(int eventId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            // G13: the event's existence and date were not checked; a missing id
            // gave a 500 via the foreign key error and joining a past event succeeded.
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) return NotFound(new StatusMessageDto(_t["errors.eventNotFound"]));
            if (cinemaEvent.EventDate < DateTime.UtcNow)
            {
                return BadRequest(new StatusMessageDto(_t["events.eventPassed"]));
            }

            var alreadyJoined = await _context.EventParticipants.AnyAsync(p => p.UserId == userId && p.CinemaEvent.EventDate >= DateTime.UtcNow);
            if(alreadyJoined)
            {
                return BadRequest(new StatusMessageDto(_t["events.alreadyWaiting"]));
            }

            var participant = new EventParticipant
            {
                UserId = userId,
                CinemaEventId = eventId
            };
            await _context.EventParticipants.AddAsync(participant);
            await _context.SaveChangesAsync();

            return Ok(new StatusMessageDto(_t["events.joined"]));
        }
        
        [HttpPut("{eventId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<StatusMessageDto>> UpdateEvent(int eventId, [FromBody] CreateEventDto updateDto)
        {
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) return NotFound(_t["events.updateNotFound"].Value);
            
            cinemaEvent.LocationName = updateDto.LocationName;
            cinemaEvent.Address = updateDto.Address;
            cinemaEvent.EventDate = updateDto.EventDate;
            cinemaEvent.GroupSize = updateDto.GroupSize;
            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto(_t["events.updated"]));
        }

        [HttpDelete("{eventId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<StatusMessageDto>> DeleteEvent(int eventId)
        {
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) return NotFound(_t["events.deleteNotFound"].Value);
            
            _context.CinemaEvents.Remove(cinemaEvent);
            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto(_t["events.deleted"]));
        }
    }
}

