using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RatingsController(ApplicationDbContext context, MovieCatalogService movieCatalog, IStringLocalizer localizer) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;
        private readonly MovieCatalogService _movieCatalog = movieCatalog;
        private readonly IStringLocalizer _t = localizer;

        [HttpPost]
        public async Task<ActionResult<StatusMessageDto>> AddRating(CreateRatingDto createRatingDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            // A movie missing locally is fetched from TMDb and added (metadata
            // comes from the server, not the client). No separate "add first" call needed.
            await _movieCatalog.EnsureMovieAsync(createRatingDto.MovieId);

            var existingRating = await _context.UserRatings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == createRatingDto.MovieId);

            if (existingRating != null)
            {
                // Changing a rating does not move it up in the feed.
                existingRating.Rating = createRatingDto.Rating;
                await _context.SaveChangesAsync();
            }
            else
            {
                var now = DateTime.UtcNow;
                _context.UserRatings.Add(new UserRating
                {
                    MovieId = createRatingDto.MovieId,
                    UserId = userId,
                    Rating = createRatingDto.Rating,
                    CreatedAt = now,
                    ActivityAt = now
                });
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // A concurrent second request created the same rating (unique index): update it.
                    _context.ChangeTracker.Clear();
                    var raced = await _context.UserRatings
                        .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == createRatingDto.MovieId);
                    if (raced == null) throw;
                    raced.Rating = createRatingDto.Rating;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new StatusMessageDto(_t["ratings.saved"]));
        }

        // Backward compatibility: clients call this before rating. The title/
        // poster sent by the client are now IGNORED; only the id is read and the
        // movie is fetched from TMDb (over-posting closed). AddRating does the
        // same, so this call is optional.
        [HttpPost("addmovie")]
        public async Task<ActionResult<MovieSummaryDto>> AddMovie([FromBody] AddMovieDto dto)
        {
            var movie = await _movieCatalog.EnsureMovieAsync(dto.MovieId);
            return Ok(new MovieSummaryDto { Id = movie.Id, Title = movie.Title, PosterPath = movie.PosterPath });
        }
    }
}
