using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly TokenService _tokenService;

        public SubscriptionController(UserManager<AppUser> userManager, TokenService tokenService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
        }

        // UYARI: Bu endpoint hiçbir ödeme doğrulaması yapmıyor — herhangi bir
        // kullanıcı tek istekle abone olabilir. Üretime çıkmadan önce ödeme
        // sağlayıcı (Iyzipay/Stripe) webhook doğrulaması eklenmeli.
        // Bkz. docs/ANALIZ-VE-YOL-HARITASI.md → G4
        [HttpPost("activate")]
        public async Task<ActionResult<AuthResponseDto>> ActivateSubscription()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            user.IsSubscribed = true;
            user.SubscriptionExpires = DateTime.UtcNow.AddMonths(1);

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest("Abonelik aktifleştirilirken bir hata oluştu.");
            }

            // Abonelik durumu token claim'inde taşındığı için yeni token üretilir.
            // Önceki sürüm burada yalnızca "Jwt:Key" okuyordu; JWT_KEY ortam
            // değişkeniyle çalışan kurulumlarda NullReferenceException atıyordu.
            return Ok(await _tokenService.CreateAuthResponseAsync(user, Request.Headers.UserAgent.ToString()));
        }
    }
}
