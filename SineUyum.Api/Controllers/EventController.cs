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
    [Route("api/event")] // <-- DEĞİŞİKLİK BURADA! "[controller]" yerine "event" yazdık.
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
        
        [HttpGet("my-event")]
        public async Task<IActionResult> GetMyCurrentEvent()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var groupInfo = await _context.EventGroupMembers
                .Where(gm => gm.UserId == userId && gm.EventGroup.CinemaEvent.EventDate >= DateTime.UtcNow)
                .OrderBy(gm => gm.EventGroup.CinemaEvent.EventDate)
                .Select(gm => new {
                    Event = new {
                        Id = gm.EventGroup.CinemaEvent.Id,
                        EventDate = gm.EventGroup.CinemaEvent.EventDate,
                        LocationName = gm.EventGroup.CinemaEvent.LocationName,
                        Address = gm.EventGroup.CinemaEvent.Address
                    },
                    Group = new {
                        Id = gm.EventGroupId,
                        Members = gm.EventGroup.Members.Select(m => new {
                            m.User.Id,
                            m.User.UserName,
                            m.User.ProfileImageUrl
                        }).ToList()
                    }
                })
                .FirstOrDefaultAsync();

            if (groupInfo == null)
            {
                return NotFound(new { message = "Yaklaşan bir etkinliğe atanmadınız." });
            }

            return Ok(groupInfo);
        }

        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto dto)
        {
            var newEvent = new CinemaEvent {
                EventDate = dto.EventDate,
                LocationName = dto.LocationName,
                Address = dto.Address,
                GroupSize = dto.GroupSize
            };
            await _context.CinemaEvents.AddAsync(newEvent);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetEventById), new { eventId = newEvent.Id }, newEvent);
        }
        
        [HttpPost("{eventId}/create-groups")]
        public async Task<IActionResult> CreateGroups(int eventId)
        {
            try {
                await _matchingService.CreateGroupsForEventAsync(eventId);
                return Ok(new { message = "Gruplar başarıyla oluşturuldu." });
            } catch (Exception ex) {
                return BadRequest(new { message = ex.Message });
            }
        }
        
        [HttpGet("{eventId}")]
        public async Task<IActionResult> GetEventById(int eventId)
        {
            var singleEvent = await _context.CinemaEvents
                .Where(e => e.Id == eventId)
                .Select(e => new { e.Id, e.EventDate, e.LocationName, e.Address, e.GroupSize })
                .FirstOrDefaultAsync();
            if (singleEvent == null) return NotFound();
            return Ok(singleEvent);
        }
    }
    
    public class CreateEventDto
    {
        [Required]
        public DateTime EventDate { get; set; }
        [Required]
        public string LocationName { get; set; } = string.Empty;
        public string? Address { get; set; }
        [Required]
        [Range(2, 12)]
        public int GroupSize { get; set; }
    }
}