using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // DÜZELTME 1: Bu metot artık sadece okunmamışları değil, tüm bildirimleri getirir.
        [HttpGet]
        public async Task<ActionResult<List<NotificationDto>>> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId) // "&& !n.IsRead" kaldırıldı
                .OrderByDescending(n => n.CreatedAt)
                .Take(50) // Çok fazla bildirim olmasını önlemek için bir limit eklemek iyi bir pratik.
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Message = n.Message,
                    RelatedUrl = n.RelatedUrl,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return Ok(notifications);
        }

        // DÜZELTME 2: Frontend'in beklediği doğru rota ("mark-all-as-read") eklendi.
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

            return Ok(new StatusMessageDto("Tüm bildirimler okundu olarak işaretlendi."));
        }

        // YENİ EKLENEN METOT: Tek bir bildirimi okundu olarak işaretlemek için.
        [HttpPost("{notificationId}/mark-as-read")]
        public async Task<ActionResult<StatusMessageDto>> MarkAsRead(int notificationId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                return NotFound("Bildirim bulunamadı veya bu bildirime erişim yetkiniz yok.");
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Ok(new StatusMessageDto("Bildirim okundu olarak işaretlendi."));
        }
    }
}