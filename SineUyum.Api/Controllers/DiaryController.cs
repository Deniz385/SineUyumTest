using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using SineUyum.Api.RateLimiting;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    // Film diary: a dated log of watched movies, separate from ratings (the
    // rating is the opinion, the diary is when it was watched; rewatches are
    // separate entries). Reading someone's diary follows the profile details
    // rule (PrivacyService.EnsureCanViewDetailsAsync): 404 on a block, 403 on
    // a private profile. Only the owner can change their entries.
    [ApiController]
    [Route("api/diary")]
    [Authorize]
    public class DiaryController : ControllerBase
    {
        public static readonly DateOnly EarliestWatchedOn = new(1900, 1, 1);

        private readonly ApplicationDbContext _context;
        private readonly MovieCatalogService _movieCatalog;
        private readonly RatingService _ratings;
        private readonly PrivacyService _privacy;
        private readonly IStringLocalizer _t;

        public DiaryController(ApplicationDbContext context, MovieCatalogService movieCatalog, RatingService ratings, PrivacyService privacy, IStringLocalizer localizer)
        {
            _context = context;
            _movieCatalog = movieCatalog;
            _ratings = ratings;
            _privacy = privacy;
            _t = localizer;
        }

        private string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpPost]
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<DiaryEntryDto>> Create([FromBody] CreateDiaryEntryDto dto)
        {
            var movieId = dto.MovieId!.Value;
            var watchedOn = dto.WatchedOn!.Value;
            EnsureValidWatchedOn(watchedOn);

            // A movie missing locally is fetched from TMDb (same as rating it).
            await _movieCatalog.EnsureMovieAsync(movieId);

            // Checked before the rating is written, so a duplicate changes nothing.
            if (await DuplicateExistsAsync(movieId, watchedOn, exceptId: null)) throw Duplicate();

            if (dto.Rating is int rating)
            {
                await _ratings.UpsertAsync(Me, movieId, rating);
            }

            var entry = new DiaryEntry
            {
                UserId = Me,
                MovieId = movieId,
                WatchedOn = watchedOn,
                Rewatch = dto.Rewatch,
                Note = NormalizeNote(dto.Note)
            };
            _context.DiaryEntries.Add(entry);
            await SaveCheckingDuplicateAsync(movieId, watchedOn, exceptId: null);

            var created = (await ToDtosAsync(_context.DiaryEntries.Where(d => d.Id == entry.Id))).Single();
            return CreatedAtAction(nameof(ForUser), new { userId = Me }, created);
        }

        [HttpPut("{id:int}")]
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<DiaryEntryDto>> Update(int id, [FromBody] UpdateDiaryEntryDto dto)
        {
            var entry = await OwnEntryAsync(id);
            var watchedOn = dto.WatchedOn!.Value;
            EnsureValidWatchedOn(watchedOn);

            if (watchedOn != entry.WatchedOn && await DuplicateExistsAsync(entry.MovieId, watchedOn, exceptId: id))
            {
                throw Duplicate();
            }

            entry.WatchedOn = watchedOn;
            entry.Rewatch = dto.Rewatch;
            entry.Note = NormalizeNote(dto.Note);
            await SaveCheckingDuplicateAsync(entry.MovieId, watchedOn, exceptId: id);

            return Ok((await ToDtosAsync(_context.DiaryEntries.Where(d => d.Id == id))).Single());
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<StatusMessageDto>> Delete(int id)
        {
            var entry = await OwnEntryAsync(id);
            _context.DiaryEntries.Remove(entry);
            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto(_t["diary.deleted"]));
        }

        // A user's entries, newest first; optionally only one year.
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<DiaryPageDto>> ForUser(string userId, [FromQuery] int? year = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            await _privacy.EnsureCanViewDetailsAsync(Me, userId, User.IsInRole("Admin"));

            var entries = _context.DiaryEntries.Where(d => d.UserId == userId);
            if (year is int y)
            {
                var (from, to) = YearRange(y);
                entries = entries.Where(d => d.WatchedOn >= from && d.WatchedOn < to);
            }

            return Ok(new DiaryPageDto
            {
                Items = await ToDtosAsync(entries
                    .OrderByDescending(d => d.WatchedOn).ThenByDescending(d => d.Id)
                    .Skip((page - 1) * pageSize).Take(pageSize)),
                TotalCount = await entries.CountAsync()
            });
        }

        // Counts per month for one year (the current UTC year by default).
        [HttpGet("user/{userId}/summary")]
        public async Task<ActionResult<DiarySummaryDto>> Summary(string userId, [FromQuery] int? year = null)
        {
            await _privacy.EnsureCanViewDetailsAsync(Me, userId, User.IsInRole("Admin"));
            var selectedYear = year ?? DateTime.UtcNow.Year;
            var (from, to) = YearRange(selectedYear);

            var entries = _context.DiaryEntries.Where(d => d.UserId == userId);
            var months = await entries
                .Where(d => d.WatchedOn >= from && d.WatchedOn < to)
                .GroupBy(d => d.WatchedOn.Month)
                .Select(g => new { Month = g.Key, Count = g.Count(), Rewatches = g.Count(d => d.Rewatch) })
                .ToListAsync();

            return Ok(new DiarySummaryDto
            {
                Year = selectedYear,
                TotalCount = months.Sum(m => m.Count),
                RewatchCount = months.Sum(m => m.Rewatches),
                Months = Enumerable.Range(1, 12)
                    .Select(m => new DiaryMonthCountDto { Month = m, Count = months.FirstOrDefault(x => x.Month == m)?.Count ?? 0 })
                    .ToList(),
                AvailableYears = await entries
                    .Select(d => d.WatchedOn.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync()
            });
        }

        // The watch date can be at most one day ahead (the client may be in a
        // time zone ahead of UTC) and not before 1900.
        private static void EnsureValidWatchedOn(DateOnly watchedOn)
        {
            if (watchedOn > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            {
                throw new DomainException("diary.watchedOnInFuture");
            }
            if (watchedOn < EarliestWatchedOn)
            {
                throw new DomainException("diary.watchedOnTooEarly");
            }
        }

        private static (DateOnly From, DateOnly To) YearRange(int year)
        {
            if (year < EarliestWatchedOn.Year || year > DateTime.UtcNow.Year + 1)
            {
                throw new DomainException("diary.invalidYear");
            }
            return (new DateOnly(year, 1, 1), new DateOnly(year + 1, 1, 1));
        }

        private static string? NormalizeNote(string? note) =>
            string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        private async Task<DiaryEntry> OwnEntryAsync(int id) =>
            await _context.DiaryEntries.FirstOrDefaultAsync(d => d.Id == id && d.UserId == Me)
            ?? throw DomainException.NotFound("diary.notFound");

        private Task<bool> DuplicateExistsAsync(int movieId, DateOnly watchedOn, int? exceptId) =>
            _context.DiaryEntries.AnyAsync(d =>
                d.UserId == Me && d.MovieId == movieId && d.WatchedOn == watchedOn && d.Id != exceptId);

        // The unique index catches a concurrent duplicate the pre-check missed.
        private async Task SaveCheckingDuplicateAsync(int movieId, DateOnly watchedOn, int? exceptId)
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                if (await DuplicateExistsAsync(movieId, watchedOn, exceptId)) throw Duplicate();
                throw;
            }
        }

        private static DomainException Duplicate() =>
            new("diary.duplicate", StatusCodes.Status409Conflict);

        private Task<List<DiaryEntryDto>> ToDtosAsync(IQueryable<DiaryEntry> entries) =>
            entries.Select(d => new DiaryEntryDto
            {
                Id = d.Id,
                Movie = new MovieSummaryDto { Id = d.MovieId, Title = d.Movie.Title, PosterPath = d.Movie.PosterPath },
                WatchedOn = d.WatchedOn,
                Rewatch = d.Rewatch,
                Note = d.Note,
                Rating = _context.UserRatings
                    .Where(r => r.UserId == d.UserId && r.MovieId == d.MovieId)
                    .Select(r => (int?)r.Rating)
                    .FirstOrDefault(),
                CreatedAt = d.CreatedAt
            }).ToListAsync();
    }
}
