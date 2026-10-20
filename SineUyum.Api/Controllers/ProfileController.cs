// SineUyum.Api/Controllers/ProfileController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using Microsoft.AspNetCore.RateLimiting;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using SineUyum.Api.Services.Media;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        // Telefon fotoğrafları için yeterli; daha büyükleri istemci küçültmeli.
        public const long MaxPhotoBytes = 10 * 1024 * 1024;
        // Multipart sınırları ve başlıklar için pay.
        private const long MaxPhotoRequestBytes = MaxPhotoBytes + 64 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IMediaStorage _media;
        private readonly PrivacyService _privacy;

        public ProfileController(ApplicationDbContext context, UserManager<AppUser> userManager, IMediaStorage media, PrivacyService privacy)
        {
            _context = context;
            _userManager = userManager;
            _media = media;
            _privacy = privacy;
        }

        private static string AvatarDirectory(string userId) => $"avatars/{userId}";
        [HttpGet("{userId}")]
        public async Task<ActionResult<UserProfileDto>> GetUserProfile(string userId)
        {
            var viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (viewerId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);

            // Engelleyen kişi görüntüleyene hiç yokmuş gibi görünür.
            if (user == null || (viewerId != userId && await _privacy.HasBlockedAsync(userId, viewerId)))
            {
                return NotFound("Kullanıcı bulunamadı.");
            }

            // Engellediği birinin profilini açan kişi yalnızca kimliği görür
            // (engeli kaldırabilsin diye); gizli profilde de ayrıntı dönmez.
            var isBlockedByMe = viewerId != userId && await _privacy.HasBlockedAsync(viewerId, userId);
            if (isBlockedByMe || !await _privacy.CanViewDetailsAsync(viewerId, user, User.IsInRole("Admin")))
            {
                return Ok(new UserProfileDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Bio = user.Bio,
                    ProfileImageUrl = user.ProfileImageUrl,
                    IsRestricted = true,
                    IsBlockedByMe = isBlockedByMe
                });
            }

            var ratingsQuery = _context.UserRatings.Where(r => r.UserId == userId);

            // İstatistikler veritabanında hesaplanır — tüm puanlar belleğe
            // çekilmez (M5). Sayı ve ortalama tek sorguyla.
            var totalRatings = await ratingsQuery.CountAsync();
            var averageRating = totalRatings == 0
                ? 0
                : Math.Round(await ratingsQuery.AverageAsync(r => (double)r.Rating), 1);

            // Döndürülen liste sınırlı: en yüksek puanlı ilk 100. Bir kullanıcının
            // binlerce puanı olsa bile yanıt ve bellek sınırlı kalır.
            const int maxRatingsReturned = 100;
            var userRatings = await ratingsQuery
                .OrderByDescending(r => r.Rating)
                .ThenByDescending(r => r.MovieId)
                .Take(maxRatingsReturned)
                .Select(r => new ProfileRatingDto
                {
                    MovieId = r.MovieId,
                    Rating = r.Rating,
                    Title = r.Movie.Title,
                    PosterPath = r.Movie.PosterPath
                })
                .ToListAsync();

            var totalMoviesInWatchlists = await _context.Watchlists
                .Where(w => w.UserId == userId)
                .SelectMany(w => w.Items)
                .CountAsync();

            var stats = new ProfileStatisticsDto
            {
                TotalRatings = totalRatings,
                AverageRating = averageRating,
                TotalMoviesInWatchlists = totalMoviesInWatchlists,
                // En yüksek puanlı (10) filmlerden ilk 5'i (liste zaten puana göre sıralı).
                TopRatedMovies = userRatings.Where(r => r.Rating == 10).Take(5).ToList()
            };

            var profileData = new UserProfileDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Bio = user.Bio,
                ProfileImageUrl = user.ProfileImageUrl,
                Ratings = userRatings,
                Statistics = stats
            };

            return Ok(profileData);
        }

        [HttpPut]
        public async Task<ActionResult<StatusMessageDto>> UpdateProfile([FromBody] UpdateProfileDto updateDto)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(currentUserId);
            if (user == null)
            {
                return NotFound("Kullanıcı bulunamadı.");
            }

            // Fotoğraf buradan yalnızca KALDIRILABİLİR; değiştirmek için yükleme
            // ucu kullanılır. Önceden herhangi bir URL kabul ediliyordu: başka
            // bir sunucudaki resim, profili görüntüleyen herkesin IP'sini o
            // sunucuya sızdırıyordu (izleme pikseli) ve içerik denetlenemiyordu.
            // Mevcut değeri geri gönderen istemciler (değişmedi) etkilenmez.
            var requestedImage = string.IsNullOrWhiteSpace(updateDto.ProfileImageUrl)
                ? null
                : updateDto.ProfileImageUrl.Trim();
            var removePhoto = false;
            if (requestedImage != user.ProfileImageUrl)
            {
                if (requestedImage != null)
                {
                    return BadRequest(new StatusMessageDto(
                        "Profil fotoğrafı yalnızca yüklenerek değiştirilebilir (POST /api/v1/profile/photo)."));
                }
                user.ProfileImageUrl = null;
                removePhoto = true;
            }

            user.Bio = updateDto.Bio;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            if (removePhoto) _media.DeleteAllExcept(AvatarDirectory(user.Id), keepRelativePath: null);

            return Ok(new StatusMessageDto("Profil başarıyla güncellendi."));
        }

        // Profil fotoğrafı yükler. Dosya olduğu gibi saklanmaz; doğrulanıp
        // 512x512 JPEG olarak yeniden kodlanır (EXIF/GPS silinir).
        //
        // Form, IFormFile bağlamasıyla değil burada okunur: bağlama sırasında
        // Kestrel'in gövde sınırı aşılırsa MVC bunu genel bir doğrulama hatası
        // (400) yapıyordu; istemci "dosya çok büyük"ü "dosya yok"tan ayıramıyordu.
        [HttpPost("photo")]
        [Consumes("multipart/form-data")]
        [EnableRateLimiting("upload")]
        [RequestSizeLimit(MaxPhotoRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoRequestBytes)]
        public async Task<ActionResult<ProfilePhotoDto>> UploadPhoto()
        {
            var user = await CurrentUserAsync();
            if (user == null) return Unauthorized();

            // Content-Length biliniyorsa gövdeyi hiç okumadan reddet.
            if (Request.ContentLength > MaxPhotoRequestBytes) throw PhotoTooLarge();

            IFormFile? file;
            try
            {
                file = (await Request.ReadFormAsync()).Files.GetFile("file");
            }
            catch (Exception ex) when (ex is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge }
                                          or InvalidDataException)
            {
                // Uzunluğu bildirmeyen (chunked) istekler sınırı okurken aşar.
                throw PhotoTooLarge();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new StatusMessageDto("Dosya seçilmedi (form alanı: \"file\")."));
            }
            if (file.Length > MaxPhotoBytes) throw PhotoTooLarge();

            byte[] input;
            using (var buffer = new MemoryStream((int)file.Length))
            {
                await file.CopyToAsync(buffer);
                input = buffer.ToArray();
            }

            var jpeg = AvatarImageProcessor.Process(input);

            // Dosya adı her yüklemede yeni ve tahmin edilemez: istemciler eski
            // fotoğrafı önbellekten göstermez, URL'ler kalıcı önbelleklenebilir.
            var relativePath = $"{AvatarDirectory(user.Id)}/{Guid.NewGuid():N}.jpg";
            await _media.SaveAsync(relativePath, jpeg);

            user.ProfileImageUrl = $"{_media.PublicPrefix}/{relativePath}";
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _media.DeleteAllExcept(AvatarDirectory(user.Id), keepRelativePath: null);
                return BadRequest(result.Errors);
            }

            // Eski fotoğraf(lar) ve eşzamanlı yüklemelerden kalan yetimler silinir.
            _media.DeleteAllExcept(AvatarDirectory(user.Id), relativePath);

            return Ok(new ProfilePhotoDto { ProfileImageUrl = user.ProfileImageUrl });
        }

        [HttpDelete("photo")]
        public async Task<ActionResult<ProfilePhotoDto>> DeletePhoto()
        {
            var user = await CurrentUserAsync();
            if (user == null) return Unauthorized();

            user.ProfileImageUrl = null;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) return BadRequest(result.Errors);

            _media.DeleteAllExcept(AvatarDirectory(user.Id), keepRelativePath: null);
            return Ok(new ProfilePhotoDto());
        }

        private static DomainException PhotoTooLarge() =>
            new("Fotoğraf en fazla 10 MB olabilir.", StatusCodes.Status413PayloadTooLarge);

        private async Task<AppUser?> CurrentUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId == null ? null : await _userManager.FindByIdAsync(userId);
        }
    }
}