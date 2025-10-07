using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
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
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId) // "&& !n.IsRead" kaldırıldı
                .OrderByDescending(n => n.CreatedAt)
                .Take(50) // Çok fazla bildirim olmasını önlemek için bir limit eklemek iyi bir pratik.
                .ToListAsync();

            return Ok(notifications);
        }

        // DÜZELTME 2: Frontend'in beklediği doğru rota ("mark-all-as-read") eklendi.
        [HttpPost("mark-all-as-read")]
        public async Task<IActionResult> MarkAllAsRead()
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

            return Ok(new { message = "Tüm bildirimler okundu olarak işaretlendi." });
        }

        // YENİ EKLENEN METOT: Tek bir bildirimi okundu olarak işaretlemek için.
        [HttpPost("{notificationId}/mark-as-read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
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

            return Ok(new { message = "Bildirim okundu olarak işaretlendi." });
        }
    }
}