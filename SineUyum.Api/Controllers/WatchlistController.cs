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
    public class WatchlistController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly MovieCatalogService _movieCatalog;

        public WatchlistController(ApplicationDbContext context, MovieCatalogService movieCatalog)
        {
            _context = context;
            _movieCatalog = movieCatalog;
        }

        [HttpGet]
        public async Task<ActionResult<List<WatchlistSummaryDto>>> GetUserWatchlists()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var watchlists = await _context.Watchlists
                .Where(w => w.UserId == userId)
                .Select(w => new WatchlistSummaryDto
                {
                    Id = w.Id,
                    Name = w.Name,
                    Description = w.Description,
                    ItemCount = w.Items.Count()
                })
                .OrderBy(w => w.Name)
                .ToListAsync();

            return Ok(watchlists);
        }

        [HttpGet("public/{listId}")]
        public async Task<ActionResult<PublicWatchlistDto>> GetPublicWatchlistDetails(int listId)
        {
            var watchlist = await _context.Watchlists
                .Where(w => w.Id == listId)
                .Select(w => new PublicWatchlistDto
                {
                    Id = w.Id,
                    Name = w.Name,
                    Description = w.Description,
                    OwnerUsername = w.User.UserName,
                    Items = w.Items
                        .OrderByDescending(i => i.AddedAt)
                        .Select(i => new WatchlistItemDto
                        {
                            MovieId = i.MovieId,
                            Title = i.Movie.Title,
                            PosterPath = i.Movie.PosterPath,
                            AddedAt = i.AddedAt
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (watchlist == null)
            {
                return NotFound("İstenen liste bulunamadı.");
            }

            return Ok(watchlist);
        }
        
        [HttpGet("{listId}")]
        public async Task<ActionResult<WatchlistDetailDto>> GetWatchlistDetails(int listId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var watchlist = await _context.Watchlists
                .Where(w => w.Id == listId && w.UserId == userId)
                .Select(w => new WatchlistDetailDto
                {
                    Id = w.Id,
                    Name = w.Name,
                    Description = w.Description,
                    Items = w.Items
                        .OrderByDescending(i => i.AddedAt)
                        .Select(i => new WatchlistItemDto
                        {
                            MovieId = i.MovieId,
                            Title = i.Movie.Title,
                            PosterPath = i.Movie.PosterPath,
                            AddedAt = i.AddedAt
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (watchlist == null)
            {
                return NotFound("Liste bulunamadı veya bu listeye erişim yetkiniz yok.");
            }

            return Ok(watchlist);
        }

        [HttpPost]
        public async Task<ActionResult<WatchlistDetailDto>> CreateWatchlist([FromBody] CreateWatchlistDto dto)
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

            // Varlık doğrudan dönülmez (UserId ve boş User navigasyonu sızıyordu).
            return CreatedAtAction(nameof(GetWatchlistDetails), new { listId = watchlist.Id }, new WatchlistDetailDto
            {
                Id = watchlist.Id,
                Name = watchlist.Name,
                Description = watchlist.Description
            });
        }
        
        [HttpPut("{listId}")]
        public async Task<ActionResult<StatusMessageDto>> UpdateWatchlist(int listId, [FromBody] CreateWatchlistDto dto)
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

            return Ok(new StatusMessageDto("Liste başarıyla güncellendi."));
        }

        [HttpDelete("{listId}")]
        public async Task<ActionResult<StatusMessageDto>> DeleteWatchlist(int listId)
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
            
            return Ok(new StatusMessageDto("Liste başarıyla silindi."));
        }

        [HttpPost("{listId}/movies")]
        public async Task<ActionResult<StatusMessageDto>> AddMovieToWatchlist(int listId, [FromBody] AddWatchlistItemDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();
            var watchlist = await _context.Watchlists.FirstOrDefaultAsync(w => w.Id == listId && w.UserId == userId);
            if (watchlist == null)
            {
                return NotFound("Liste bulunamadı veya bu listeye erişim yetkiniz yok.");
            }
            var alreadyInList = await _context.WatchlistItems.AnyAsync(i => i.WatchlistId == listId && i.MovieId == dto.MovieId);
            if (alreadyInList)
            {
                return BadRequest(new StatusMessageDto("Bu film zaten bu listede mevcut."));
            }
            // Film yerelde yoksa başlık/poster TMDb'den çekilir; istemcinin
            // gönderdiği Title/PosterPath paylaşılan Movies tablosuna yazılmaz
            // (G7'deki over-posting açığının izleme listesi tarafı).
            await _movieCatalog.EnsureMovieAsync(dto.MovieId);
            var watchlistItem = new WatchlistItem
            {
                WatchlistId = listId,
                MovieId = dto.MovieId
            };
            _context.WatchlistItems.Add(watchlistItem);
            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto("Film listeye eklendi."));
        }

        [HttpDelete("{listId}/movies/{movieId}")]
        public async Task<ActionResult<StatusMessageDto>> RemoveMovieFromWatchlist(int listId, int movieId)
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
            return Ok(new StatusMessageDto("Film listeden kaldırıldı."));
        }
    }
}