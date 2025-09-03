// Controllers/RatingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RatingsController(ApplicationDbContext context) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;

        // POST /api/ratings
        [HttpPost]  // <-- ARTIK DOĞRU YERDE!
        public async Task<IActionResult> AddRating(CreateRatingDto createRatingDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            var movie = await _context.Movies.FindAsync(createRatingDto.MovieId);

            if (movie == null)
            {
                return NotFound("Bu film sistemde kayıtlı değil. Lütfen önce filmi ekleyin.");
            }

            var existingRating = await _context.UserRatings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == createRatingDto.MovieId);

            if (existingRating != null)
            {
                existingRating.Rating = createRatingDto.Rating;
            }
            else
            {
                var newRating = new UserRating
                {
                    MovieId = createRatingDto.MovieId,
                    UserId = userId,
                    Rating = createRatingDto.Rating
                };
                await _context.UserRatings.AddAsync(newRating);
            }

            await _context.SaveChangesAsync();
            return Ok("Puan başarıyla kaydedildi.");
        }

        // POST /api/ratings/addmovie
        [HttpPost("addmovie")] // <-- SADECE KENDİNE AİT ATTRIBUTE KALDI!
        public async Task<IActionResult> AddMovie(Movie movie)
        {
            var movieExists = await _context.Movies.AnyAsync(m => m.Id == movie.Id);
            if (movieExists)
            {
                return BadRequest("Bu film zaten mevcut.");
            }

            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();
            return Ok(movie);
        }
        // Controllers/RatingsController.cs ... en alta

        [HttpGet("test")]
        public IActionResult TestAuth()
        {
            // Eğer bu metoda erişebiliyorsak, token'ımız geçerli demektir.
            // Token'ın içindeki kullanıcı adını okuyup geri döndürelim.
            var username = User.FindFirstValue(ClaimTypes.Name);
            return Ok($"Token geçerli! Giriş yapan kullanıcı: {username}");
        }
    }
}