using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public AccountController(UserManager<AppUser> userManager, IConfiguration configuration, ApplicationDbContext context)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var user = new AppUser { UserName = registerDto.Username, Email = registerDto.Email };
            var result = await _userManager.CreateAsync(user, registerDto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, "User");

            return Ok(new { message = "Kullanıcı başarıyla oluşturuldu." });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var user = await _userManager.FindByNameAsync(loginDto.Username);
            if (user == null || !await _userManager.CheckPasswordAsync(user, loginDto.Password))
                return Unauthorized(new { message = "Kullanıcı adı veya şifre hatalı." });
            
            var token = await CreateToken(user);
            return Ok(new { token });
        }
        
        [HttpGet("search")]
        [Authorize]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(new List<object>());
            }

            var users = await _userManager.Users
                .Where(u => u.UserName != null && u.Id != currentUserId && u.UserName.ToLower().Contains(query.ToLower()))
                .Select(u => new {
                    u.Id,
                    u.UserName,
                    u.ProfileImageUrl,
                    u.Bio
                })
                .ToListAsync();

            var searchResults = new List<object>();

            foreach (var user in users)
            {
                var commonRatings = await (from r1 in _context.UserRatings.Where(r => r.UserId == currentUserId)
                                           join r2 in _context.UserRatings.Where(r => r.UserId == user.Id) on r1.MovieId equals r2.MovieId
                                           select new { r1.Rating, TargetRating = r2.Rating })
                                           .ToListAsync();

                double compatibilityScore = 0;
                if (commonRatings.Any())
                {
                    double totalWeightedScore = 0;
                    foreach (var ratingPair in commonRatings)
                    {
                        double difference = Math.Abs(ratingPair.Rating - ratingPair.TargetRating);
                        double weight = 1.0;
                        if ((ratingPair.Rating <= 3 && ratingPair.TargetRating <= 3) || (ratingPair.Rating >= 8 && ratingPair.TargetRating >= 8)) weight = 1.5;
                        else if ((ratingPair.Rating <= 3 && ratingPair.TargetRating >= 8) || (ratingPair.Rating >= 8 && ratingPair.TargetRating <= 3)) weight = 2.0;
                        totalWeightedScore += (10 - difference) * weight;
                    }
                    double maxPossibleScore = commonRatings.Count * 10 * 1.5;
                    compatibilityScore = (totalWeightedScore / maxPossibleScore) * 100;
                }

                searchResults.Add(new {
                    user.Id,
                    user.UserName,
                    user.ProfileImageUrl,
                    user.Bio,
                    CompatibilityScore = Math.Round(Math.Max(0, Math.Min(100, compatibilityScore)), 1)
                });
            }

            return Ok(searchResults.OrderByDescending(r => ((dynamic)r).CompatibilityScore));
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

            // --- DÜZELTME BAŞLANGICI ---
            // Anahtarı, Program.cs'teki mantıkla aynı şekilde oku.
            var jwtKey = _configuration["JWT_KEY"] ?? _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT anahtarı token oluşturma sırasında bulunamadı.");
            }
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            // --- DÜZELTME BİTİŞİ ---
            
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
