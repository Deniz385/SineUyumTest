// SineUyum.Api/Controllers/SubscriptionController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SineUyum.Api.Data;
using SineUyum.Api.Models;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ApplicationDbContext _context;

        public SubscriptionController(UserManager<AppUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // Bu endpoint, test amacıyla kullanıcının aboneliğini başlatır.
        [HttpPost("activate")]
        public async Task<IActionResult> ActivateSubscription()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            // Kullanıcının aboneliğini aktifleştir ve 1 ay süre ver
            user.IsSubscribed = true;
            user.SubscriptionExpires = DateTime.UtcNow.AddMonths(1);

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                // ÖNEMLİ: Frontend'in güncel durumu bilmesi için yeni bir token oluşturup göndermeliyiz.
                // AccountController'daki CreateToken metodunu buraya kopyalayabilir veya
                // daha sonra refactor edip ortak bir yerden çağırabiliriz. Şimdilik geçici bir çözüm yapalım.
                // Not: Bu kısım normalde AccountController'da olmalı.
                // Ancak hızlı test için şimdilik burada bırakabiliriz.
                return Ok(new { message = "Abonelik başarıyla aktifleştirildi. Lütfen yeniden giriş yapın." });
            }

            return BadRequest("Abonelik aktifleştirilirken bir hata oluştu.");
        }
    }
}