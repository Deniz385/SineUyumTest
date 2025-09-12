using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SineUyum.Api.Data;
using SineUyum.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;

        public SubscriptionController(UserManager<AppUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpPost("activate")]
        public async Task<IActionResult> ActivateSubscription()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            user.IsSubscribed = true;
            user.SubscriptionExpires = DateTime.UtcNow.AddMonths(1);

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var token = await CreateToken(user);
                return Ok(new { message = "Abonelik başarıyla aktifleştirildi.", token });
            }

            return BadRequest("Abonelik aktifleştirilirken bir hata oluştu.");
        }

        private async Task<string> CreateToken(AppUser user)
        {
           var claims = new List<Claim>
           {
               new(ClaimTypes.NameIdentifier, user.Id),
               new(ClaimTypes.Name, user.UserName ?? string.Empty),
               new("IsSubscribed", user.IsSubscribed.ToString())
           };

           var roles = await _userManager.GetRolesAsync(user);
           foreach (var role in roles)
           {
               claims.Add(new Claim(ClaimTypes.Role, role));
           }

           var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
           var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

           var tokenDescriptor = new SecurityTokenDescriptor
           {
               Subject = new ClaimsIdentity(claims),
               Expires = DateTime.UtcNow.AddDays(7),
               SigningCredentials = creds,
               Issuer = _configuration["Jwt:Issuer"]!,
               Audience = _configuration["Jwt:Audience"]!
           };

           var tokenHandler = new JwtSecurityTokenHandler();
           var token = tokenHandler.CreateToken(tokenDescriptor);

           return tokenHandler.WriteToken(token);
        }
    }
}