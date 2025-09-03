// Controllers/AccountController.cs
using Microsoft.AspNetCore.Authorization; // <-- EKLENDİ (Hata 1 için)
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // <-- EKLENDİ (Hata 2 için)
using Microsoft.IdentityModel.Tokens;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController(UserManager<AppUser> userManager, IConfiguration configuration) : ControllerBase
    {
        // Primary constructor kullandığımız için bu gereksiz alanlar SİLİNDİ (Uyarı 3 için)
        // private readonly UserManager<AppUser> _userManager = userManager;
        // private readonly IConfiguration _configuration = configuration;

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto registerDto)
        {
            var user = new AppUser { UserName = registerDto.Username, Email = registerDto.Email };
            var result = await userManager.CreateAsync(user, registerDto.Password);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok("Kullanıcı başarıyla oluşturuldu.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            var user = await userManager.FindByNameAsync(loginDto.Username);

            if (user != null && await userManager.CheckPasswordAsync(user, loginDto.Password))
            {
                var token = CreateToken(user);
                return Ok(new { token });
            }

            return Unauthorized("Kullanıcı adı veya şifre hatalı.");
        }
        
        // YENİ EKLENEN KULLANICI LİSTELEME METODU
        [HttpGet("users")]
        [Authorize] // Sadece giriş yapmış kullanıcılar diğer kullanıcıları görebilir
        public async Task<IActionResult> GetAllUsers()
        {
            // UserManager'ı kullanarak veritabanındaki tüm kullanıcıları alıyoruz.
            // Güvenlik için sadece Id ve UserName alanlarını seçip geri döndürüyoruz.
            var users = await userManager.Users
                .Select(u => new { u.Id, u.UserName })
                .ToListAsync();
        
            return Ok(users);
        }

        private string CreateToken(AppUser user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName!)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(7),
                SigningCredentials = creds,
                Issuer = configuration["Jwt:Issuer"],
                Audience = configuration["Jwt:Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}