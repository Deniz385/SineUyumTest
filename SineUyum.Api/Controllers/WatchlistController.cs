// SineUyum.Api/Controllers/WatchlistController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos; // DTO'yu kullanmak için ekle
using SineUyum.Api.Models;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WatchlistController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public WatchlistController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET metodu aynı kalıyor...
    [HttpGet]
    public async Task<IActionResult> GetWatchlist()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var watchlist = await _context.WatchlistItems
            .Where(wi => wi.UserId == userId)
            .Include(wi => wi.Movie)
            .Select(wi => new { wi.MovieId, wi.Movie.Title, wi.Movie.PosterPath, wi.AddedAt })
            .OrderByDescending(wi => wi.AddedAt)
            .ToListAsync();

        return Ok(watchlist);
    }

    // --- DEĞİŞİKLİK BURADA BAŞLIYOR ---
    // POST: api/watchlist
    [HttpPost] // URL'den movieId parametresini kaldırdık
    public async Task<IActionResult> AddToWatchlist([FromBody] AddWatchlistItemDto dto) // Parametreyi DTO olarak değiştirdik
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        // Film veritabanında var mı kontrol et
        var movie = await _context.Movies.FindAsync(dto.MovieId);
        
        // Eğer film yerel veritabanında yoksa, oluştur ve ekle
        if (movie == null)
        {
            movie = new Movie
            {
                Id = dto.MovieId,
                Title = dto.Title,
                PosterPath = dto.PosterPath
            };
            _context.Movies.Add(movie);
        }

        var alreadyInWatchlist = await _context.WatchlistItems
            .AnyAsync(wi => wi.UserId == userId && wi.MovieId == dto.MovieId);

        if (alreadyInWatchlist)
        {
            return BadRequest("Bu film zaten izleme listenizde.");
        }

        var watchlistItem = new WatchlistItem
        {
            UserId = userId,
            MovieId = dto.MovieId
        };

        _context.WatchlistItems.Add(watchlistItem);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Film izleme listesine eklendi." });
    }
    // --- DEĞİŞİKLİK BURADA BİTİYOR ---


    // DELETE metodu aynı kalıyor...
    [HttpDelete("{movieId}")]
    public async Task<IActionResult> RemoveFromWatchlist(int movieId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var watchlistItem = await _context.WatchlistItems
            .FirstOrDefaultAsync(wi => wi.UserId == userId && wi.MovieId == movieId);

        if (watchlistItem == null)
        {
            return NotFound("Bu film izleme listenizde bulunamadı.");
        }

        _context.WatchlistItems.Remove(watchlistItem);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Film izleme listesinden kaldırıldı." });
    }
}