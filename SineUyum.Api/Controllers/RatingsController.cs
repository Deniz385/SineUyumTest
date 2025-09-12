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

        [HttpPost]
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
                // Normalde bu durumun yaşanmaması gerekir çünkü oylamadan hemen önce film ekleniyor.
                // Yine de bir güvenlik kontrolü olarak kalabilir.
                return NotFound("Bu film sistemde kayıtlı değil. Lütfen önce filmi ekleyin.");
            }

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
            return Ok(new { message = "Puan başarıyla kaydedildi." });
        }
        
        // --- DÜZELTİLMİŞ METOT ---
        [HttpPost("addmovie")]
        public async Task<IActionResult> AddMovie(Movie movie)
        {
            // Film veritabanında zaten var mı diye kontrol et
            var movieExists = await _context.Movies.AnyAsync(m => m.Id == movie.Id);
            
            // Eğer film mevcut değilse, ekle. Mevcutsa hiçbir şey yapma ve başarılı dön.
            if (!movieExists)
            {
                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();
            }
            
            // Her iki durumda da frontend'in devam edebilmesi için OK (200) dön.
            return Ok(movie);
        }

        [HttpGet("test")]
        public IActionResult TestAuth()
        {
            var username = User.FindFirstValue(ClaimTypes.Name);
            return Ok($"Token geçerli! Giriş yapan kullanıcı: {username}");
        }
    }
}