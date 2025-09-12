using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Hubs;
using SineUyum.Api.Models;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/messages")]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;


        public MessageController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpPost]
        public async Task<IActionResult> CreateMessage(CreateMessageDto createMessageDto)
        {
            var senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (senderId == null) return Unauthorized();
            
            var sender = await _context.Users.FindAsync(senderId);
            if (sender == null) return Unauthorized();

            if (senderId == createMessageDto.RecipientId)
                return BadRequest("Kendinize mesaj gönderemezsiniz.");

            if (string.IsNullOrWhiteSpace(createMessageDto.Content) && !createMessageDto.MovieId.HasValue && !createMessageDto.WatchlistId.HasValue)
                return BadRequest("Mesaj içeriği boş olamaz.");

            if (createMessageDto.WatchlistId.HasValue)
            {
                var listExists = await _context.Watchlists.AnyAsync(w => w.Id == createMessageDto.WatchlistId.Value && w.UserId == senderId);
                if (!listExists)
                {
                    return Forbid("Kendinize ait olmayan bir listeyi paylaşamazsınız.");
                }
            }

            var recipient = await _context.Users.FindAsync(createMessageDto.RecipientId);
            if (recipient == null) return NotFound("Alıcı kullanıcı bulunamadı.");

            var message = new Message
            {
                SenderId = senderId,
                RecipientId = createMessageDto.RecipientId,
                Content = createMessageDto.Content,
                MovieId = createMessageDto.MovieId,
                WatchlistId = createMessageDto.WatchlistId
            };
            await _context.Messages.AddAsync(message);
            
            // --- BİLDİRİM OLUŞTURMA VE GÖNDERME ---
            var notification = new Notification
            {
                UserId = createMessageDto.RecipientId, // Bildirim mesajın alıcısına gidecek
                Message = $"{sender.UserName} size bir mesaj gönderdi.",
                RelatedUrl = $"/messages/{senderId}" // Tıklayınca gönderenle olan sohbete gitsin
            };
            await _context.Notifications.AddAsync(notification);

            await _context.SaveChangesAsync();
            
            // SignalR ile anlık bildirimi gönder
            await _hubContext.Clients.User(createMessageDto.RecipientId).SendAsync("ReceiveNotification", notification);

            return Ok(new { message = "Mesaj başarıyla gönderildi." });
        }

        // ... GetMessageThread ve GetConversations metodları aynı kalacak ...
         [HttpGet("thread/{otherUserId}")]
        public async Task<IActionResult> GetMessageThread(string otherUserId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            var messages = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Include(m => m.Movie)
                .Include(m => m.Watchlist)
                    .ThenInclude(w => w!.User)
                .Where(m => (m.RecipientId == currentUserId && m.SenderId == otherUserId) ||
                            (m.RecipientId == otherUserId && m.SenderId == currentUserId))
                .OrderBy(m => m.MessageSent)
                .Select(m => new 
                {
                    m.Id,
                    m.SenderId,
                    SenderUsername = m.Sender.UserName,
                    m.RecipientId,
                    RecipientUsername = m.Recipient.UserName,
                    m.Content,
                    m.MessageSent,
                    Movie = m.Movie == null ? null : new {
                        Id = m.Movie.Id,
                        Title = m.Movie.Title,
                        PosterPath = m.Movie.PosterPath
                    },
                    Watchlist = m.Watchlist == null ? null : new {
                        Id = m.Watchlist.Id,
                        Name = m.Watchlist.Name,
                        OwnerUsername = m.Watchlist.User == null ? null : m.Watchlist.User.UserName
                    }
                })
                .ToListAsync();

            return Ok(messages);
        }

        [HttpGet]
        public async Task<IActionResult> GetConversations()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            var messages = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Include(m => m.Watchlist)
                .Where(m => m.SenderId == currentUserId || m.RecipientId == currentUserId)
                .OrderByDescending(m => m.MessageSent)
                .ToListAsync();

            var conversations = messages
                .GroupBy(m => m.SenderId == currentUserId ? m.RecipientId : m.SenderId)
                .Select(g => 
                {
                    var lastMessage = g.First();
                    var otherUser = lastMessage.SenderId == currentUserId ? lastMessage.Recipient : lastMessage.Sender;
                    
                    string lastMessageContent = "Bir film önerdi.";
                    if (lastMessage.WatchlistId.HasValue) {
                         lastMessageContent = $"'{lastMessage.Watchlist?.Name}' listesini paylaştı.";
                    } else if (!string.IsNullOrEmpty(lastMessage.Content)) {
                        lastMessageContent = lastMessage.Content;
                    }

                    return new 
                    {
                        OtherUserId = otherUser.Id,
                        OtherUserUsername = otherUser.UserName,
                        OtherUserProfileImageUrl = otherUser.ProfileImageUrl,
                        LastMessageContent = lastMessageContent,
                        LastMessageSent = lastMessage.MessageSent,
                        IsRead = lastMessage.DateRead != null || lastMessage.SenderId == currentUserId
                    };
                })
                .ToList();

            return Ok(conversations);
        }
    }
}

