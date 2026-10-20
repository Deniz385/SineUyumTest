using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [Authorize]
    public class RatingsController(ApplicationDbContext context, MovieCatalogService movieCatalog) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;
        private readonly MovieCatalogService _movieCatalog = movieCatalog;

        [HttpPost]
        public async Task<ActionResult<StatusMessageDto>> AddRating(CreateRatingDto createRatingDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            // Film yerelde yoksa TMDb'den çekilip eklenir (meta veri sunucudan,
            // istemciden değil). Ayrı bir "önce ekle" çağrısı gerekmez.
            await _movieCatalog.EnsureMovieAsync(createRatingDto.MovieId);

            var existingRating = await _context.UserRatings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == createRatingDto.MovieId);

            if (existingRating != null)
            {
                // Kullanıcı bu filme daha önce oy verdiyse, sadece puanını güncelle
                existingRating.Rating = createRatingDto.Rating;
            }
            else
            {
                // Kullanıcı bu filme ilk defa oy veriyorsa, yeni bir kayıt oluştur
                var newRating = new UserRating
                {
                    MovieId = createRatingDto.MovieId,
                    UserId = userId,
                    Rating = createRatingDto.Rating
                };
                await _context.UserRatings.AddAsync(newRating);
            }

            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto("Puan başarıyla kaydedildi."));
        }
        
        // Geriye dönük uyumluluk: istemciler puanlamadan önce bu ucu çağırıyor.
        // Artık istemciden gelen başlık/poster YOK SAYILIR; yalnızca id okunur ve
        // film TMDb'den çekilip eklenir (over-posting kapatıldı). AddRating zaten
        // aynı işi yaptığı için bu çağrı opsiyoneldir.
        [HttpPost("addmovie")]
        public async Task<ActionResult<MovieSummaryDto>> AddMovie([FromBody] AddMovieDto dto)
        {
            var movie = await _movieCatalog.EnsureMovieAsync(dto.MovieId);
            return Ok(new MovieSummaryDto { Id = movie.Id, Title = movie.Title, PosterPath = movie.PosterPath });
        }
    }
}