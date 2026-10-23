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
    // Token creation used to be copied twice, in AccountController and
    // SubscriptionController (and SubscriptionController crashed because it did
    // not read the JWT_KEY environment variable). Moved to a single source.
    public class TokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly SubscriptionService _subscriptions;

        public TokenService(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            IConfiguration configuration,
            SubscriptionService subscriptions)
        {
            _context = context;
            _subscriptions = subscriptions;
            _userManager = userManager;
            _configuration = configuration;
        }

        private TimeSpan AccessTokenLifetime =>
            TimeSpan.FromMinutes(_configuration.GetValue("Jwt:AccessTokenMinutes", 60));

        private TimeSpan RefreshTokenLifetime =>
            TimeSpan.FromDays(_configuration.GetValue("Jwt:RefreshTokenDays", 60));

        // Creates an access token + a new refresh token and cleans up old
        // session rows. The mobile client writes this response straight to secure storage.
        // A language the client asked for explicitly becomes the user's stored
        // language (saved together with the refresh token), keeping push
        // notifications in the language the user currently uses.
        public async Task<AuthResponseDto> CreateAuthResponseAsync(AppUser user, string? deviceInfo = null, string? language = null)
        {
            if (language != null && user.Language != language)
            {
                user.Language = language;
            }

            var roles = await _userManager.GetRolesAsync(user);
            var expiresAt = DateTime.UtcNow.Add(AccessTokenLifetime);
            var subscription = await _subscriptions.GetStateAsync(user.Id);
            var accessToken = CreateAccessToken(user, roles, expiresAt, subscription.IsActive);
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
                    IsSubscribed = subscription.IsActive,
                    SubscriptionExpires = subscription.ExpiresAt,
                    Roles = roles.ToList(),
                    Language = user.Language,
                    OnboardingCompleted = user.OnboardingCompletedAt != null
                }
            };
        }

        // Validates the given refresh token, revokes it and issues a new one
        // (refresh token rotation). Returns null if it is invalid.
        public async Task<AuthResponseDto?> RefreshAsync(string refreshToken, string? deviceInfo = null, string? language = null)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return null;

            var hash = HashToken(refreshToken);
            var stored = await _context.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (stored == null || !stored.IsActive) return null;

            stored.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await CreateAuthResponseAsync(stored.User, deviceInfo ?? stored.DeviceInfo, language);
        }

        // Called e.g. after a password change: all of the user's active sessions are
        // revoked, so a stolen refresh token stops working.
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

        // Called on logout: the given refresh token is revoked.
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

        // The IsSubscribed claim is only for the client's UI; paid features are
        // checked on the server against the database ([RequiresSubscription]).
        private string CreateAccessToken(AppUser user, IEnumerable<string> roles, DateTime expiresAt, bool isSubscribed)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new("IsSubscribed", isSubscribed.ToString())
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var jwtKey = _configuration["JWT_KEY"] ?? _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("The JWT key is not configured.");
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

            // Remove expired or revoked rows so they do not pile up.
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
