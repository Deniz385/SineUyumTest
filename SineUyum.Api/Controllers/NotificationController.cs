using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer _t;

        public NotificationController(ApplicationDbContext context, IStringLocalizer localizer)
        {
            _context = context;
            _t = localizer;
        }

        // All notifications (read and unread), newest first, in the request language.
        [HttpGet]
        public async Task<ActionResult<List<NotificationDto>>> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50) // a limit so the list cannot grow without bound
                .AsNoTracking()
                .ToListAsync();

            return Ok(notifications.Select(n => NotificationDto.From(n, _t)).ToList());
        }

        // The route the frontend expects ("mark-all-as-read").
        [HttpPost("mark-all-as-read")]
        public async Task<ActionResult<StatusMessageDto>> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            return Ok(new StatusMessageDto(_t["notifications.allMarkedAsRead"]));
        }

        // Marks a single notification as read.
        [HttpPost("{notificationId}/mark-as-read")]
        public async Task<ActionResult<StatusMessageDto>> MarkAsRead(int notificationId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                return NotFound(_t["notifications.notFound"].Value);
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Ok(new StatusMessageDto(_t["notifications.markedAsRead"]));
        }
    }
}
