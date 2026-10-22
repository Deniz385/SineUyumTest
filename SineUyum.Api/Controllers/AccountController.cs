using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Localization;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly TokenService _tokenService;
        private readonly SubscriptionService _subscriptions;
        private readonly PrivacyService _privacy;
        private readonly SupportedLanguages _languages;
        private readonly IStringLocalizer _t;

        public AccountController(UserManager<AppUser> userManager, ApplicationDbContext context, TokenService tokenService, SubscriptionService subscriptions, PrivacyService privacy, SupportedLanguages languages, IStringLocalizer localizer)
        {
            _userManager = userManager;
            _context = context;
            _tokenService = tokenService;
            _subscriptions = subscriptions;
            _privacy = privacy;
            _languages = languages;
            _t = localizer;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<StatusMessageDto>> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            // New accounts start in the language the client is using.
            var user = new AppUser
            {
                UserName = registerDto.Username,
                Email = registerDto.Email,
                Language = _languages.Resolve(SupportedLanguages.Current)
            };
            var result = await _userManager.CreateAsync(user, registerDto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, "User");

            return Ok(new StatusMessageDto(_t["account.registered"]));
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var user = await _userManager.FindByNameAsync(loginDto.Username);
            if (user == null || !await _userManager.CheckPasswordAsync(user, loginDto.Password))
                return Unauthorized(new StatusMessageDto(_t["account.invalidCredentials"]));

            return Ok(await _tokenService.CreateAuthResponseAsync(user, DeviceInfo(), HttpContext.ExplicitRequestLanguage(_languages)));
        }

        // The mobile client calls this when its access token expires; the user
        // does not have to log in again.
        [HttpPost("refresh")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _tokenService.RefreshAsync(dto.RefreshToken, DeviceInfo(), HttpContext.ExplicitRequestLanguage(_languages));
            if (response == null)
                return Unauthorized(new StatusMessageDto(_t["account.invalidRefreshToken"]));

            return Ok(response);
        }

        // Logout: the refresh token is revoked on the server.
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult<StatusMessageDto>> Logout([FromBody] RefreshRequestDto dto)
        {
            await _tokenService.RevokeAsync(dto.RefreshToken);
            return Ok(new StatusMessageDto(_t["account.loggedOut"]));
        }

        // Used when the mobile app starts, to check that the token is still
        // valid and to get fresh user details (subscription state included).
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserSummaryDto>> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            var subscription = await _subscriptions.GetStateAsync(user.Id);
            return Ok(new UserSummaryDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email,
                Bio = user.Bio,
                ProfileImageUrl = user.ProfileImageUrl,
                IsSubscribed = subscription.IsActive,
                SubscriptionExpires = subscription.ExpiresAt,
                Roles = roles.ToList(),
                Language = user.Language
            });
        }

        // The user's language: notifications (in-app and push) are rendered in
        // it. Clients set it when the user picks a language.
        [HttpPut("language")]
        [Authorize]
        public async Task<ActionResult<LanguageDto>> UpdateLanguage([FromBody] LanguageDto dto)
        {
            var user = await CurrentUserAsync();
            if (user == null) return Unauthorized();

            var language = _languages.Match(dto.Language)
                ?? throw new DomainException("account.unsupportedLanguage", [dto.Language, string.Join(", ", _languages.All)]);

            user.Language = language;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(new LanguageDto { Language = language });
        }

        // There was no way to change the password in the app; a user who wanted
        // to change theirs could do nothing about it.
        //
        // After a password change all of the user's other sessions are closed
        // (so a stolen refresh token stops working), and the requesting device
        // gets a new token pair so the user stays signed in there.
        [HttpPost("change-password")]
        [Authorize]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<AuthResponseDto>> ChangePassword(
            [FromBody] ChangePasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            var result = await _userManager.ChangePasswordAsync(
                user, dto.CurrentPassword, dto.NewPassword);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _tokenService.RevokeAllForUserAsync(userId);

            return Ok(await _tokenService.CreateAuthResponseAsync(user, DeviceInfo()));
        }

        // Privacy settings (G12). Each one is switched on and off independently.
        [HttpGet("privacy")]
        [Authorize]
        public async Task<ActionResult<PrivacySettingsDto>> GetPrivacy()
        {
            var user = await CurrentUserAsync();
            if (user == null) return Unauthorized();
            return Ok(ToPrivacyDto(user));
        }

        [HttpPut("privacy")]
        [Authorize]
        public async Task<ActionResult<PrivacySettingsDto>> UpdatePrivacy([FromBody] PrivacySettingsDto dto)
        {
            var user = await CurrentUserAsync();
            if (user == null) return Unauthorized();

            user.ProfileVisibility = dto.ProfileVisibility!.Value;
            user.MessagePermission = dto.MessagePermission!.Value;
            user.HiddenFromSearch = dto.HiddenFromSearch;
            if (dto.ShowReadReceipts.HasValue) user.ShowReadReceipts = dto.ShowReadReceipts.Value;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(ToPrivacyDto(user));
        }

        private static PrivacySettingsDto ToPrivacyDto(AppUser user) => new()
        {
            ProfileVisibility = user.ProfileVisibility,
            MessagePermission = user.MessagePermission,
            HiddenFromSearch = user.HiddenFromSearch,
            ShowReadReceipts = user.ShowReadReceipts
        };

        private async Task<AppUser?> CurrentUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId == null ? null : await _userManager.FindByIdAsync(userId);
        }

        private string? DeviceInfo() => Request.Headers.UserAgent.ToString();

        [HttpGet("search")]
        [Authorize]
        public async Task<ActionResult<List<UserSearchResultDto>>> SearchUsers(
            [FromQuery] string query,
            [FromQuery] int limit = 30)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(new List<UserSearchResultDto>());
            }

            limit = Math.Clamp(limit, 1, 100);
            // Invariant: in a Turkish request culture "I" would lower to a dotless i.
            var loweredQuery = query.ToLowerInvariant();

            // People who chose to be hidden from search and blocked people (both ways) are excluded.
            var blockedIds = _privacy.BlockedPartnerIds(currentUserId);
            var users = await _userManager.Users
                .Where(u => u.UserName != null && u.Id != currentUserId && u.UserName.ToLower().Contains(loweredQuery))
                .Where(u => !u.HiddenFromSearch && !blockedIds.Contains(u.Id))
                .OrderBy(u => u.UserName)
                .Take(limit)
                .Select(u => new UserSearchResultDto
                {
                    Id = u.Id,
                    UserName = u.UserName!,
                    ProfileImageUrl = u.ProfileImageUrl,
                    Bio = u.Bio
                })
                .ToListAsync();

            if (users.Count == 0) return Ok(users);

            // This used to run one query per user (N+1). Now the common ratings
            // of all matching users are fetched in a single query.
            var userIds = users.Select(u => u.Id).ToList();
            var commonRatings = await (
                from mine in _context.UserRatings.Where(r => r.UserId == currentUserId)
                join theirs in _context.UserRatings.Where(r => userIds.Contains(r.UserId))
                    on mine.MovieId equals theirs.MovieId
                select new { theirs.UserId, MyRating = mine.Rating, TheirRating = theirs.Rating })
                .ToListAsync();

            var ratingsByUser = commonRatings
                .GroupBy(r => r.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<(int, int)>)g.Select(r => (r.MyRating, r.TheirRating)).ToList());

            foreach (var user in users)
            {
                var pairs = ratingsByUser.TryGetValue(user.Id, out var found)
                    ? found
                    : Array.Empty<(int, int)>();

                user.CommonMovieCount = pairs.Count;
                user.CompatibilityScore = Math.Round(CompatibilityCalculator.Score(pairs), 1);
            }

            return Ok(users.OrderByDescending(u => u.CompatibilityScore).ToList());
        }
    }
}
