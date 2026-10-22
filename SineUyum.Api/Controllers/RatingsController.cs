using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Dtos;
using SineUyum.Api.Localization;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RatingsController(MovieCatalogService movieCatalog, RatingService ratings, IStringLocalizer localizer) : ControllerBase
    {
        private readonly MovieCatalogService _movieCatalog = movieCatalog;
        private readonly RatingService _ratings = ratings;
        private readonly IStringLocalizer _t = localizer;

        [HttpPost]
        public async Task<ActionResult<StatusMessageDto>> AddRating(CreateRatingDto createRatingDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            // A movie missing locally is fetched from TMDb and added by the
            // service. No separate "add first" call needed.
            await _ratings.UpsertAsync(userId, createRatingDto.MovieId, createRatingDto.Rating);

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
            var (title, posterPath) = await _movieCatalog.LocalizedAsync(movie, SupportedLanguages.Current);
            return Ok(new MovieSummaryDto { Id = movie.Id, Title = title, PosterPath = posterPath });
        }
    }
}
