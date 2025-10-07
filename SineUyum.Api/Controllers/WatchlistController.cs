using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos; // <-- HATA GİDERİLDİ: EKSİK OLAN SATIR BUYDU
using SineUyum.Api.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Controllers
{
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

        [HttpGet]
        public async Task<IActionResult> GetUserWatchlists()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var watchlists = await _context.Watchlists
                .Where(w => w.UserId == userId)
                .Select(w => new { w.Id, w.Name, w.Description, ItemCount = w.Items.Count() })
                .OrderBy(w => w.Name)
                .ToListAsync();

            return Ok(watchlists);
        }

        [HttpGet("public/{listId}")]
        public async Task<IActionResult> GetPublicWatchlistDetails(int listId)
        {
            var watchlist = await _context.Watchlists
                .Include(w => w.User)
                .Where(w => w.Id == listId)
                .Select(w => new
                {
                    w.Id,
                    w.Name,
                    w.Description,
                    OwnerUsername = w.User.UserName,
                    Items = w.Items.Select(i => new
                    {
                        i.MovieId,
                        i.Movie.Title,
                        i.Movie.PosterPath,
                        i.AddedAt
                    }).OrderByDescending(i => i.AddedAt).ToList()
                })
                .FirstOrDefaultAsync();

            if (watchlist == null)
            {
                return NotFound("İstenen liste bulunamadı.");
            }

            return Ok(watchlist);
        }
        
        [HttpGet("{listId}")]
        public async Task<IActionResult> GetWatchlistDetails(int listId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var watchlist = await _context.Watchlists
                .Where(w => w.Id == listId && w.UserId == userId)
                .Select(w => new
                {
                    w.Id,
                    w.Name,
                    w.Description,
                    Items = w.Items.Select(i => new
                    {
                        i.MovieId,
                        i.Movie.Title,
                        i.Movie.PosterPath,
                        i.AddedAt
                    }).OrderByDescending(i => i.AddedAt).ToList()
                })
                .FirstOrDefaultAsync();

            if (watchlist == null)
            {
                return NotFound("Liste bulunamadı veya bu listeye erişim yetkiniz yok.");
            }

            return Ok(watchlist);
        }

        [HttpPost]
        public async Task<IActionResult> CreateWatchlist([FromBody] CreateWatchlistDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var watchlist = new Watchlist
            {
                Name = dto.Name,
                Description = dto.Description,
                UserId = userId
            };

            _context.Watchlists.Add(watchlist);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetWatchlistDetails), new { listId = watchlist.Id }, watchlist);
        }
        
        [HttpPut("{listId}")]
        public async Task<IActionResult> UpdateWatchlist(int listId, [FromBody] CreateWatchlistDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var watchlist = await _context.Watchlists.FirstOrDefaultAsync(w => w.Id == listId && w.UserId == userId);

            if (watchlist == null)
            {
                return NotFound("Liste bulunamadı veya bu listeye erişim yetkiniz yok.");
            }

            watchlist.Name = dto.Name;
            watchlist.Description = dto.Description;

            _context.Watchlists.Update(watchlist);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Liste başarıyla güncellendi." });
        }

        [HttpDelete("{listId}")]
        public async Task<IActionResult> DeleteWatchlist(int listId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var watchlist = await _context.Watchlists.FirstOrDefaultAsync(w => w.Id == listId && w.UserId == userId);

            if (watchlist == null)
            {
                return NotFound("Liste bulunamadı veya bu listeye erişim yetkiniz yok.");
            }

            _context.Watchlists.Remove(watchlist);
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Liste başarıyla silindi." });
        }

        [HttpPost("{listId}/movies")]
        public async Task<IActionResult> AddMovieToWatchlist(int listId, [FromBody] AddWatchlistItemDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();
            var watchlist = await _context.Watchlists.FirstOrDefaultAsync(w => w.Id == listId && w.UserId == userId);
            if (watchlist == null)
            {
                return NotFound("Liste bulunamadı veya bu listeye erişim yetkiniz yok.");
            }
            var movie = await _context.Movies.FindAsync(dto.MovieId);
            if (movie == null)
            {
                movie = new Movie { Id = dto.MovieId, Title = dto.Title, PosterPath = dto.PosterPath };
                _context.Movies.Add(movie);
            }
            var alreadyInList = await _context.WatchlistItems.AnyAsync(i => i.WatchlistId == listId && i.MovieId == dto.MovieId);
            if (alreadyInList)
            {
                return BadRequest(new { message = "Bu film zaten bu listede mevcut." });
            }
            var watchlistItem = new WatchlistItem
            {
                WatchlistId = listId,
                MovieId = dto.MovieId
            };
            _context.WatchlistItems.Add(watchlistItem);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Film listeye eklendi." });
        }

        [HttpDelete("{listId}/movies/{movieId}")]
        public async Task<IActionResult> RemoveMovieFromWatchlist(int listId, int movieId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();
            var watchlistItem = await _context.WatchlistItems
                .Include(i => i.Watchlist)
                .FirstOrDefaultAsync(i => i.WatchlistId == listId && i.MovieId == movieId && i.Watchlist.UserId == userId);
            if (watchlistItem == null)
            {
                return NotFound("Film bu listede bulunamadı veya listeye erişim yetkiniz yok.");
            }
            _context.WatchlistItems.Remove(watchlistItem);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Film listeden kaldırıldı." });
        }
    }
}