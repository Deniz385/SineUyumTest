using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SineUyum.Api.Services
{
    // Token üretimi daha önce AccountController ve SubscriptionController içinde
    // iki kez kopyalanmıştı (ve SubscriptionController JWT_KEY ortam değişkenini
    // okumadığı için çöküyordu). Tek kaynağa taşındı.
    public class TokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;

        public TokenService(ApplicationDbContext context, UserManager<AppUser> userManager, IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
        }

        private TimeSpan AccessTokenLifetime =>
            TimeSpan.FromMinutes(_configuration.GetValue("Jwt:AccessTokenMinutes", 60));

        private TimeSpan RefreshTokenLifetime =>
            TimeSpan.FromDays(_configuration.GetValue("Jwt:RefreshTokenDays", 60));

        // Access token + yeni bir refresh token üretir ve eski oturum kaydını
        // temizler. Mobil istemci bu yanıtı doğrudan secure storage'a yazar.
        public async Task<AuthResponseDto> CreateAuthResponseAsync(AppUser user, string? deviceInfo = null)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var expiresAt = DateTime.UtcNow.Add(AccessTokenLifetime);
            var accessToken = CreateAccessToken(user, roles, expiresAt);
            var refreshToken = await CreateRefreshTokenAsync(user, deviceInfo);

            return new AuthResponseDto
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = new UserSummaryDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email,
                    Bio = user.Bio,
                    ProfileImageUrl = user.ProfileImageUrl,
                    IsSubscribed = user.IsSubscribed,
                    SubscriptionExpires = user.SubscriptionExpires,
                    Roles = roles.ToList()
                }
            };
        }

        // Sunulan refresh token'ı doğrular, iptal eder ve yerine yenisini verir
        // (refresh token rotation). Geçersizse null döner.
        public async Task<AuthResponseDto?> RefreshAsync(string refreshToken, string? deviceInfo = null)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return null;

            var hash = HashToken(refreshToken);
            var stored = await _context.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (stored == null || !stored.IsActive) return null;

            stored.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await CreateAuthResponseAsync(stored.User, deviceInfo ?? stored.DeviceInfo);
        }

        // Parola değişikliği gibi durumlarda çağrılır: kullanıcının tüm aktif
        // oturumları iptal edilir, böylece çalınmış bir refresh token geçersiz olur.
        public async Task RevokeAllForUserAsync(string userId)
        {
            var active = await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync();

            if (active.Count == 0) return;

            var now = DateTime.UtcNow;
            foreach (var token in active)
            {
                token.RevokedAt = now;
            }
            await _context.SaveChangesAsync();
        }

        // Çıkışta çağrılır: verilen refresh token iptal edilir.
        public async Task RevokeAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return;

            var hash = HashToken(refreshToken);
            var stored = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (stored is { RevokedAt: null })
            {
                stored.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        private string CreateAccessToken(AppUser user, IEnumerable<string> roles, DateTime expiresAt)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new("IsSubscribed", user.IsSubscribed.ToString())
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var jwtKey = _configuration["JWT_KEY"] ?? _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT anahtarı yapılandırılmamış.");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }

        private async Task<string> CreateRefreshTokenAsync(AppUser user, string? deviceInfo)
        {
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            // Süresi geçmiş veya iptal edilmiş kayıtları biriktirmemek için temizle.
            var stale = await _context.RefreshTokens
                .Where(t => t.UserId == user.Id && (t.RevokedAt != null || t.ExpiresAt <= DateTime.UtcNow))
                .ToListAsync();
            if (stale.Count > 0)
            {
                _context.RefreshTokens.RemoveRange(stale);
            }

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken(raw),
                ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime),
                DeviceInfo = deviceInfo is { Length: > 200 } ? deviceInfo[..200] : deviceInfo
            });

            await _context.SaveChangesAsync();
            return raw;
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }
    }
}
