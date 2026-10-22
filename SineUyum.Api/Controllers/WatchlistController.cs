using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using SineUyum.Api.RateLimiting;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WatchlistController : ControllerBase
    {
        // Sahip hariç, davetler dahil.
        public const int MaxMembers = 20;

        private readonly ApplicationDbContext _context;
        private readonly MovieCatalogService _movieCatalog;
        private readonly PrivacyService _privacy;
        private readonly NotificationService _notifications;

        public WatchlistController(ApplicationDbContext context, MovieCatalogService movieCatalog, PrivacyService privacy, NotificationService notifications)
        {
            _context = context;
            _movieCatalog = movieCatalog;
            _privacy = privacy;
            _notifications = notifications;
        }

        // Kendi listelerim ve üyesi olduğum ortak listeler.
        [HttpGet]
        public async Task<ActionResult<List<WatchlistSummaryDto>>> GetUserWatchlists()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var watchlists = await _context.Watchlists
                .Where(w => w.UserId == userId || w.Members.Any(m => m.UserId == userId && m.Accepted))
                .Select(w => new WatchlistSummaryDto
                {
                    Id = w.Id,
                    Name = w.Name,
                    Description = w.Description,
                    ItemCount = w.Items.Count(),
                    IsOwner = w.UserId == userId,
                    OwnerUsername = w.User.UserName,
                    MemberCount = 1 + w.Members.Count(m => m.Accepted),
                    IsShared = w.UserId != userId || w.Members.Any(m => m.Accepted)
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

            var access = await AccessAsync(listId, userId);
            if (access == null)
            {
                return NotFound("Liste bulunamadı veya bu listeye erişim yetkiniz yok.");
            }
            var (list, isOwner) = access.Value;

            var members = await _context.WatchlistMembers
                .Where(m => m.WatchlistId == listId && (isOwner || m.Accepted))
                .OrderBy(m => m.InvitedAt)
                .Select(m => new WatchlistMemberDto
                {
                    User = new UserBriefWithImageDto { Id = m.UserId, UserName = m.User.UserName, ProfileImageUrl = m.User.ProfileImageUrl },
                    Accepted = m.Accepted
                })
                .ToListAsync();

            // "Kim izledi": liste kişilerinden (sahip + kabul etmiş üyeler)
            // filmi puanlamış olanlar. Birlikte ne izleneceğini seçmeye yarar.
            var people = _context.WatchlistMembers
                .Where(m => m.WatchlistId == listId && m.Accepted)
                .Select(m => m.UserId)
                .Concat(_context.Watchlists.Where(w => w.Id == listId).Select(w => w.UserId));

            var items = await _context.WatchlistItems
                .Where(i => i.WatchlistId == listId)
                .OrderByDescending(i => i.AddedAt)
                .Select(i => new WatchlistItemDto
                {
                    MovieId = i.MovieId,
                    Title = i.Movie.Title,
                    PosterPath = i.Movie.PosterPath,
                    AddedAt = i.AddedAt,
                    AddedByUsername = i.AddedBy == null ? null : i.AddedBy.UserName,
                    WatchedBy = _context.UserRatings
                        .Where(r => r.MovieId == i.MovieId && people.Contains(r.UserId))
                        .Select(r => r.User.UserName!)
                        .ToList()
                })
                .ToListAsync();

            var owner = await _context.Users
                .Where(u => u.Id == list.UserId)
                .Select(u => new UserBriefWithImageDto { Id = u.Id, UserName = u.UserName, ProfileImageUrl = u.ProfileImageUrl })
                .SingleAsync();

            return Ok(new WatchlistDetailDto
            {
                Id = list.Id,
                Name = list.Name,
                Description = list.Description,
                Items = items,
                IsOwner = isOwner,
                Owner = owner,
                Members = members
            });
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
            if (await AccessAsync(listId, userId) == null)
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
                MovieId = dto.MovieId,
                AddedByUserId = userId
            };
            _context.WatchlistItems.Add(watchlistItem);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Ortak listede iki kişi aynı anda aynı filmi ekledi (benzersiz indeks).
                _context.ChangeTracker.Clear();
                if (!await _context.WatchlistItems.AnyAsync(i => i.WatchlistId == listId && i.MovieId == dto.MovieId)) throw;
                return BadRequest(new StatusMessageDto("Bu film zaten bu listede mevcut."));
            }
            return Ok(new StatusMessageDto("Film listeye eklendi."));
        }

        [HttpDelete("{listId}/movies/{movieId}")]
        public async Task<ActionResult<StatusMessageDto>> RemoveMovieFromWatchlist(int listId, int movieId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();
            var watchlistItem = await _context.WatchlistItems
                .FirstOrDefaultAsync(i => i.WatchlistId == listId && i.MovieId == movieId &&
                    (i.Watchlist.UserId == userId || i.Watchlist.Members.Any(m => m.UserId == userId && m.Accepted)));
            if (watchlistItem == null)
            {
                return NotFound("Film bu listede bulunamadı veya listeye erişim yetkiniz yok.");
            }
            _context.WatchlistItems.Remove(watchlistItem);
            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto("Film listeden kaldırıldı."));
        }
    
        // --- Ortak liste: davet, kabul, ayrılma ---

        // Sahip bir kullanıcıyı davet eder; davetli kabul edene kadar listeyi göremez.
        [HttpPost("{listId}/members")]
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<WatchlistMemberDto>> Invite(int listId, [FromBody] InviteToWatchlistDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var list = await _context.Watchlists.FirstOrDefaultAsync(w => w.Id == listId && w.UserId == userId);
            if (list == null) throw DomainException.NotFound("Liste bulunamadı ya da sahibi değilsiniz.");
            if (dto.UserId == userId) throw new DomainException("Kendinizi davet edemezsiniz.");

            var invitee = await _context.Users.FindAsync(dto.UserId);
            if (invitee == null || await _privacy.IsBlockedEitherWayAsync(userId, dto.UserId))
            {
                throw DomainException.NotFound("Kullanıcı bulunamadı.");
            }
            if (await _context.WatchlistMembers.AnyAsync(m => m.WatchlistId == listId && m.UserId == dto.UserId))
            {
                throw new DomainException("Bu kişi zaten listede ya da davet edilmiş.", StatusCodes.Status409Conflict);
            }
            if (await _context.WatchlistMembers.CountAsync(m => m.WatchlistId == listId) >= MaxMembers)
            {
                throw new DomainException($"Bir listeye en fazla {MaxMembers} kişi davet edilebilir.");
            }

            _context.WatchlistMembers.Add(new WatchlistMember { WatchlistId = listId, UserId = dto.UserId });
            var me = await _context.Users.Where(u => u.Id == userId).Select(u => u.UserName).SingleAsync();
            await _notifications.NotifyAsync(dto.UserId,
                $"{me} seni '{list.Name}' ortak izleme listesine davet etti.",
                "/watchlist");

            return Ok(new WatchlistMemberDto
            {
                User = new UserBriefWithImageDto { Id = invitee.Id, UserName = invitee.UserName, ProfileImageUrl = invitee.ProfileImageUrl },
                Accepted = false
            });
        }

        [HttpGet("invitations")]
        public async Task<ActionResult<List<WatchlistInvitationDto>>> Invitations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var invitations = await _context.WatchlistMembers
                .Where(m => m.UserId == userId && !m.Accepted)
                .OrderByDescending(m => m.InvitedAt)
                .Select(m => new WatchlistInvitationDto
                {
                    ListId = m.WatchlistId,
                    Name = m.Watchlist.Name,
                    Owner = new UserBriefWithImageDto { Id = m.Watchlist.UserId, UserName = m.Watchlist.User.UserName, ProfileImageUrl = m.Watchlist.User.ProfileImageUrl },
                    ItemCount = m.Watchlist.Items.Count(),
                    InvitedAt = m.InvitedAt
                })
                .ToListAsync();
            return Ok(invitations);
        }

        [HttpPost("{listId}/accept")]
        public async Task<ActionResult<StatusMessageDto>> Accept(int listId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var membership = await _context.WatchlistMembers
                .Include(m => m.Watchlist)
                .FirstOrDefaultAsync(m => m.WatchlistId == listId && m.UserId == userId);
            if (membership == null) throw DomainException.NotFound("Davet bulunamadı.");
            if (!membership.Accepted)
            {
                membership.Accepted = true;
                membership.AcceptedAt = DateTime.UtcNow;
                var me = await _context.Users.Where(u => u.Id == userId).Select(u => u.UserName).SingleAsync();
                await _notifications.NotifyAsync(membership.Watchlist.UserId,
                    $"{me} '{membership.Watchlist.Name}' listesine katıldı.",
                    $"/watchlist/{listId}");
            }
            return Ok(new StatusMessageDto("Listeye katıldınız."));
        }

        // Sahip bir üyeyi (ya da daveti) çıkarır; üye kendisi ayrılır ya da
        // daveti reddeder.
        [HttpDelete("{listId}/members/{memberId}")]
        public async Task<ActionResult<StatusMessageDto>> RemoveMember(int listId, string memberId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var membership = await _context.WatchlistMembers
                .Include(m => m.Watchlist)
                .FirstOrDefaultAsync(m => m.WatchlistId == listId && m.UserId == memberId);
            if (membership == null) throw DomainException.NotFound("Üye bulunamadı.");
            if (memberId != userId && membership.Watchlist.UserId != userId)
            {
                throw new DomainException("Yalnızca liste sahibi üye çıkarabilir.", StatusCodes.Status403Forbidden);
            }
            _context.WatchlistMembers.Remove(membership);
            await _context.SaveChangesAsync();
            return Ok(new StatusMessageDto(memberId == userId ? "Listeden ayrıldınız." : "Üye listeden çıkarıldı."));
        }

        // Sahip ya da kabul etmiş üye; değilse null.
        private async Task<(Watchlist List, bool IsOwner)?> AccessAsync(int listId, string userId)
        {
            var list = await _context.Watchlists
                .Where(w => w.Id == listId && (w.UserId == userId || w.Members.Any(m => m.UserId == userId && m.Accepted)))
                .FirstOrDefaultAsync();
            return list == null ? null : (list, list.UserId == userId);
        }
    }
}