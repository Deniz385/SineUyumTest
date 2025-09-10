// SineUyum.Api/Controllers/AdminController.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<AppUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpPost("seed-database")]
        public async Task<IActionResult> SeedDatabase()
        {
            var botUsers = new List<(string username, string email, Dictionary<int, int> ratings)>
            {
                // TMDB ID'leri ve film adları
                ("AksiyonCanavari", "aksiyon@bot.com", new Dictionary<int, int> { {299536, 9}, {577922, 10}, {24428, 8}, {1726, 9} }), // Avengers: Endgame, The Avengers, Iron Man 2, Gladiator
                ("RomantikRuh", "romantik@bot.com", new Dictionary<int, int> { {13, 10}, {597, 9}, {19404, 8}, {49026, 9} }), // Forrest Gump, Titanic, Amelie, The Artist
                ("BilimKurguAsigi", "scifi@bot.com", new Dictionary<int, int> { {157336, 10}, {680, 10}, {12, 8}, {155, 9} }), // Interstellar, Pulp Fiction, Finding Nemo, The Dark Knight
                ("KomediKralicesi", "komedi@bot.com", new Dictionary<int, int> { {38757, 9}, {343611, 8}, {274, 10}, {9331, 8} }), // The Hangover, Zombieland, The Big Lebowski, Superbad
                ("KorkuGecesi", "korku@bot.com", new Dictionary<int, int> { {530385, 9}, {49018, 10}, {269149, 8}, {296096, 7} }), // The Conjuring 2, The Conjuring, The Hallow, The Nun
                ("SanatFilmiGurusu", "sanat@bot.com", new Dictionary<int, int> { {497, 10}, {832, 9}, {550, 9}, {637, 8} }), // The Green Mile, Memento, Fight Club, The Godfather
                ("FantastikDiyar", "fantastik@bot.com", new Dictionary<int, int> { {122, 10}, {120, 10}, {121, 9}, {671, 9} }), // Lord of the Rings serisi
                ("DramaSever", "drama@bot.com", new Dictionary<int, int> { {278, 10}, {238, 9}, {101, 8}, {1359, 7} }), // Shawshank Redemption, The Godfather, The Pianist, The Departed
                ("GerilimUstasi", "gerilim@bot.com", new Dictionary<int, int> { {769, 9}, {100402, 8}, {414906, 10}, {424, 7} }), // The Silence of the Lambs, Prisoners, Joker, Schindler's List
                ("AnimasyonPerisi", "animasyon@bot.com", new Dictionary<int, int> { {129, 9}, {35, 10}, {10681, 8}, {14160, 9} }) // Spirited Away, Spirited Away, Howl's Moving Castle, Ponyo
            };

            // --- DEĞİŞİKLİK BURADA BAŞLIYOR ---

            // 1. Adım: Botların oyladığı tüm filmlerin ID'lerini topla
            var allMovieIds = botUsers.SelectMany(b => b.ratings.Keys).Distinct().ToList();

            // 2. Adım: Bu ID'lerden hangilerinin veritabanında zaten olduğunu kontrol et
            var existingMovieIds = await _context.Movies
                .Where(m => allMovieIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync();

            // 3. Adım: Veritabanında olmayan filmleri ekle
            var moviesToAdd = new List<Movie>();
            foreach (var movieId in allMovieIds)
            {
                if (!existingMovieIds.Contains(movieId))
                {
                    // Not: Gerçek bir uygulamada bu filmlerin adlarını TMDB API'den çekerdik.
                    // Test için şimdilik sadece "Film [ID]" olarak ekliyoruz.
                    moviesToAdd.Add(new Movie { Id = movieId, Title = $"Film {movieId}" });
                }
            }

            if (moviesToAdd.Any())
            {
                await _context.Movies.AddRangeAsync(moviesToAdd);
                await _context.SaveChangesAsync();
            }

            // 4. Adım: Artık tüm filmler mevcut olduğuna göre, bot kullanıcıları ve oylarını oluştur
            int usersCreated = 0;
            foreach (var bot in botUsers)
            {
                if (await _userManager.FindByNameAsync(bot.username) == null)
                {
                    var newUser = new AppUser
                    {
                        UserName = bot.username,
                        Email = bot.email,
                        IsSubscribed = true,
                        SubscriptionExpires = DateTime.UtcNow.AddYears(1)
                    };
                    var result = await _userManager.CreateAsync(newUser, "Password123!");
                    if (result.Succeeded)
                    {
                        usersCreated++;
                        foreach (var rating in bot.ratings)
                        {
                            _context.UserRatings.Add(new UserRating { UserId = newUser.Id, MovieId = rating.Key, Rating = rating.Value });
                        }
                    }
                }
            }

            if (usersCreated > 0)
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = $"{usersCreated} adet bot kullanıcı ve oyları başarıyla oluşturuldu." });
            }

            return Ok(new { message = "Tüm bot kullanıcılar zaten mevcut." });
        }
    }
}