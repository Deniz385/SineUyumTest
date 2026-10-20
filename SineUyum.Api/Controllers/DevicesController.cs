using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    // Push bildirimi için cihaz (FCM token) kaydı.
    [ApiController]
    [Route("api/devices")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        // Bir hesabın kayıtlı cihaz sınırı; aşılınca en uzun süredir görülmeyen
        // kayıt silinir. Sınırsız sahte kayıtla tablonun şişmesini önler.
        public const int MaxDevicesPerUser = 20;

        private readonly ApplicationDbContext _context;

        public DevicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Uygulama oturum açınca ve FCM token'ı yenilenince çağırır; tekrar
        // çağrılması güvenlidir (aynı token güncellenir).
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterDeviceDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                await UpsertAsync(userId, dto);
            }
            catch (DbUpdateException)
            {
                // Aynı token için eşzamanlı iki kayıt: benzersiz indeks ikincisini
                // reddeder. Kazanan kaydı bu kullanıcıya güncellemek yeterli.
                _context.ChangeTracker.Clear();
                await UpsertAsync(userId, dto);
            }

            return NoContent();
        }

        // Çıkışta (ve oturum süresi dolduğunda) çağrılır. Kimlik doğrulama
        // istemez: oturumu düşmüş bir cihaz da önceki kullanıcının bildirimlerini
        // almayı durdurabilmeli. Token'ı yalnızca cihazın kendisi bilir.
        [HttpPost("unregister")]
        [AllowAnonymous]
        public async Task<IActionResult> Unregister([FromBody] UnregisterDeviceDto dto)
        {
            await _context.DeviceTokens.Where(d => d.Token == dto.Token).ExecuteDeleteAsync();
            return NoContent();
        }

        private async Task UpsertAsync(string userId, RegisterDeviceDto dto)
        {
            var now = DateTime.UtcNow;
            var device = await _context.DeviceTokens.FirstOrDefaultAsync(d => d.Token == dto.Token);

            if (device != null)
            {
                // Aynı cihazda başka hesapla giriş yapılmış olabilir: kayıt yeni
                // kullanıcıya taşınır, önceki kullanıcının bildirimleri buraya gitmez.
                device.UserId = userId;
                device.Platform = dto.Platform;
                device.LastSeenAt = now;
            }
            else
            {
                _context.DeviceTokens.Add(new DeviceToken
                {
                    UserId = userId,
                    Token = dto.Token,
                    Platform = dto.Platform,
                    CreatedAt = now,
                    LastSeenAt = now,
                });
            }

            await _context.SaveChangesAsync();

            var stale = await _context.DeviceTokens
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.LastSeenAt)
                .Skip(MaxDevicesPerUser)
                .Select(d => d.Id)
                .ToListAsync();
            if (stale.Count > 0)
            {
                await _context.DeviceTokens.Where(d => stale.Contains(d.Id)).ExecuteDeleteAsync();
            }
        }
    }
}
