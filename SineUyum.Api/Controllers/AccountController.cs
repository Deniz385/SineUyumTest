using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
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

        public AccountController(UserManager<AppUser> userManager, ApplicationDbContext context, TokenService tokenService, SubscriptionService subscriptions, PrivacyService privacy)
        {
            _userManager = userManager;
            _context = context;
            _tokenService = tokenService;
            _subscriptions = subscriptions;
            _privacy = privacy;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<StatusMessageDto>> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var user = new AppUser { UserName = registerDto.Username, Email = registerDto.Email };
            var result = await _userManager.CreateAsync(user, registerDto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, "User");

            return Ok(new StatusMessageDto("Kullanıcı başarıyla oluşturuldu."));
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
                return Unauthorized(new StatusMessageDto("Kullanıcı adı veya şifre hatalı."));

            return Ok(await _tokenService.CreateAuthResponseAsync(user, DeviceInfo()));
        }

        // Mobil istemci access token'ı süresi dolduğunda bu endpoint'i çağırır;
        // kullanıcıdan tekrar giriş istemek gerekmez.
        [HttpPost("refresh")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _tokenService.RefreshAsync(dto.RefreshToken, DeviceInfo());
            if (response == null)
                return Unauthorized(new StatusMessageDto("Yenileme anahtarı geçersiz veya süresi dolmuş."));

            return Ok(response);
        }

        // Çıkış: refresh token sunucu tarafında iptal edilir.
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult<StatusMessageDto>> Logout([FromBody] RefreshRequestDto dto)
        {
            await _tokenService.RevokeAsync(dto.RefreshToken);
            return Ok(new StatusMessageDto("Çıkış yapıldı."));
        }

        // Mobil uygulama açılışında token'ın hâlâ geçerli olduğunu doğrulamak ve
        // güncel kullanıcı bilgisini (abonelik durumu dahil) almak için kullanılır.
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
                Roles = roles.ToList()
            });
        }

        // Uygulamada parola değiştirmenin hiçbir yolu yoktu; bir kullanıcı
        // parolasını değiştirmek istediğinde yapabileceği bir şey olmuyordu.
        //
        // Parola değişince kullanıcının diğer tüm oturumları kapatılır (çalınmış
        // bir refresh token'ın geçerli kalmaması için), isteği yapan cihaz için
        // yeni bir token çifti döner ki kullanıcı kendi oturumundan düşmesin.
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

        // Gizlilik ayarları (G12). Her biri bağımsız açılıp kapatılır.
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
            var loweredQuery = query.ToLower();

            // Aramada görünmemeyi seçenler ve engel olan kişiler (iki yönde) çıkmaz.
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

            // Önceden her kullanıcı için ayrı sorgu atılıyordu (N+1). Artık tüm
            // eşleşen kullanıcıların ortak puanları tek sorguda çekiliyor.
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
