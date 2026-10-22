// SineUyum.Api/Controllers/ProfileController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using Microsoft.AspNetCore.RateLimiting;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Localization;
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
        // Enough for phone photos; clients should shrink larger ones.
        public const long MaxPhotoBytes = 10 * 1024 * 1024;
        // Headroom for multipart boundaries and headers.
        private const long MaxPhotoRequestBytes = MaxPhotoBytes + 64 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IMediaStorage _media;
        private readonly PrivacyService _privacy;
        private readonly IStringLocalizer _t;

        public ProfileController(ApplicationDbContext context, UserManager<AppUser> userManager, IMediaStorage media, PrivacyService privacy, IStringLocalizer localizer)
        {
            _context = context;
            _userManager = userManager;
            _media = media;
            _privacy = privacy;
            _t = localizer;
        }

        private static string AvatarDirectory(string userId) => $"avatars/{userId}";
        [HttpGet("{userId}")]
        public async Task<ActionResult<UserProfileDto>> GetUserProfile(string userId)
        {
            var viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (viewerId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);

            // To the viewer, a user who blocked them does not exist at all.
            if (user == null || (viewerId != userId && await _privacy.HasBlockedAsync(userId, viewerId)))
            {
                return NotFound(_t["errors.userNotFound"].Value);
            }

            // Someone opening the profile of a person they blocked only sees the
            // identity (so they can unblock); a private profile returns no details either.
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

            // Statistics are computed in the database — the ratings are not all
            // loaded into memory (M5). Count and average in one query each.
            var totalRatings = await ratingsQuery.CountAsync();
            var averageRating = totalRatings == 0
                ? 0
                : Math.Round(await ratingsQuery.AverageAsync(r => (double)r.Rating), 1);

            // The returned list is capped: the top 100 by rating. Even a user
            // with thousands of ratings keeps the response and memory bounded.
            const int maxRatingsReturned = 100;
            var lang = SupportedLanguages.Current;
            var userRatings = await ratingsQuery
                .OrderByDescending(r => r.Rating)
                .ThenByDescending(r => r.MovieId)
                .Take(maxRatingsReturned)
                .Select(r => new ProfileRatingDto
                {
                    MovieId = r.MovieId,
                    Rating = r.Rating,
                    Title = r.Movie.Translations.Where(t => t.Language == lang).Select(t => t.Title).FirstOrDefault() ?? r.Movie.Title,
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
                // The first 5 movies rated 10 (the list is already sorted by rating).
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

        // Shareable film identity (Phase 5). Derived from ratings, so the same
        // visibility rule as profile details.
        [HttpGet("{userId}/identity")]
        public async Task<ActionResult<FilmIdentityDto>> GetIdentity(string userId)
        {
            var viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (viewerId == null) return Unauthorized();
            var owner = await _privacy.EnsureCanViewDetailsAsync(viewerId, userId, User.IsInRole("Admin"));

            var lang = SupportedLanguages.Current;
            var rows = await _context.UserRatings
                .Where(r => r.UserId == userId)
                .Select(r => new
                {
                    r.MovieId,
                    Title = r.Movie.Translations.Where(t => t.Language == lang).Select(t => t.Title).FirstOrDefault() ?? r.Movie.Title,
                    r.Movie.PosterPath,
                    r.Rating,
                    r.Movie.TmdbVoteAverage,
                    Genres = r.Movie.MovieGenres.Select(mg => new { mg.Genre.Id, mg.Genre.Name }).ToList()
                })
                .ToListAsync();

            // Genre names in the request language (by TMDb id).
            return Ok(FilmIdentity.Build(owner.UserName ?? string.Empty,
                rows.Select(r => new RatedMovieFacts(r.MovieId, r.Title, r.PosterPath, r.Rating, r.TmdbVoteAverage,
                    r.Genres.Select(g => _t.GenreName(g.Id, g.Name)).ToList())).ToList(),
                _t));
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
                return NotFound(_t["errors.userNotFound"].Value);
            }

            // The photo can only be REMOVED here; changing it goes through the
            // upload endpoint. Any URL used to be accepted: an image on another
            // server leaked the IP of everyone viewing the profile to that server
            // (tracking pixel) and its content could not be moderated.
            // Clients sending back the current value (unchanged) are unaffected.
            var requestedImage = string.IsNullOrWhiteSpace(updateDto.ProfileImageUrl)
                ? null
                : updateDto.ProfileImageUrl.Trim();
            var removePhoto = false;
            if (requestedImage != user.ProfileImageUrl)
            {
                if (requestedImage != null)
                {
                    return BadRequest(new StatusMessageDto(_t["profile.photoUploadOnly"]));
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

            return Ok(new StatusMessageDto(_t["profile.updated"]));
        }

        // Uploads a profile photo. The file is never stored as is; it is
        // validated and re-encoded as a 512x512 JPEG (EXIF/GPS removed).
        //
        // The form is read here rather than through IFormFile binding: when
        // Kestrel's body limit was hit during binding, MVC turned it into a
        // generic validation error (400) and the client could not tell "file
        // too large" from "no file".
        [HttpPost("photo")]
        [Consumes("multipart/form-data")]
        [EnableRateLimiting("upload")]
        [RequestSizeLimit(MaxPhotoRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoRequestBytes)]
        public async Task<ActionResult<ProfilePhotoDto>> UploadPhoto()
        {
            var user = await CurrentUserAsync();
            if (user == null) return Unauthorized();

            // Reject without reading the body when Content-Length is known.
            if (Request.ContentLength > MaxPhotoRequestBytes) throw PhotoTooLarge();

            IFormFile? file;
            try
            {
                file = (await Request.ReadFormAsync()).Files.GetFile("file");
            }
            catch (Exception ex) when (ex is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge }
                                          or InvalidDataException)
            {
                // Requests without a length (chunked) hit the limit while reading.
                throw PhotoTooLarge();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new StatusMessageDto(_t["profile.photoMissing"]));
            }
            if (file.Length > MaxPhotoBytes) throw PhotoTooLarge();

            byte[] input;
            using (var buffer = new MemoryStream((int)file.Length))
            {
                await file.CopyToAsync(buffer);
                input = buffer.ToArray();
            }

            var jpeg = AvatarImageProcessor.Process(input);

            // A new, unguessable file name on every upload: clients never show
            // the old photo from cache and URLs can be cached forever.
            var relativePath = $"{AvatarDirectory(user.Id)}/{Guid.NewGuid():N}.jpg";
            await _media.SaveAsync(relativePath, jpeg);

            user.ProfileImageUrl = $"{_media.PublicPrefix}/{relativePath}";
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _media.DeleteAllExcept(AvatarDirectory(user.Id), keepRelativePath: null);
                return BadRequest(result.Errors);
            }

            // The old photo(s) and orphans left by concurrent uploads are deleted.
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
            new("profile.photoTooLarge", StatusCodes.Status413PayloadTooLarge);

        private async Task<AppUser?> CurrentUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId == null ? null : await _userManager.FindByIdAsync(userId);
        }
    }
}
