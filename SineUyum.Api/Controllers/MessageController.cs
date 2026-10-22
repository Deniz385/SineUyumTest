using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Hubs;
using SineUyum.Api.Localization;
using SineUyum.Api.Models;
using SineUyum.Api.RateLimiting;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    [ApiController]
    [Route("api/messages")]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly PrivacyService _privacy;
        private readonly IStringLocalizer _t;

        public MessageController(
            ApplicationDbContext context,
            NotificationService notifications,
            IHubContext<NotificationHub> hubContext,
            PrivacyService privacy,
            IStringLocalizer localizer)
        {
            _context = context;
            _notifications = notifications;
            _hubContext = hubContext;
            _privacy = privacy;
            _t = localizer;
        }

        // Name of the SignalR "ReceiveMessage" event clients listen to.
        public const string ReceiveMessageEvent = "ReceiveMessage";
        // Read receipts (M10): "Seen" on the sender's screen.
        public const string MessagesReadEvent = "MessagesRead";

        // The single shape of a chat message: the thread list, the send
        // response and the live event share this projection (the client reads
        // all three with the same model).
        private static System.Linq.Expressions.Expression<Func<Message, MessageThreadItemDto>> ToThreadItem(string lang) =>
            m => new MessageThreadItemDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderUsername = m.Sender.UserName,
                RecipientId = m.RecipientId,
                RecipientUsername = m.Recipient.UserName,
                Content = m.Content,
                MessageSent = m.MessageSent,
                DateRead = m.DateRead,
                Movie = m.Movie == null ? null : new MovieSummaryDto
                {
                    Id = m.Movie.Id,
                    Title = m.Movie.Translations.Where(t => t.Language == lang).Select(t => t.Title).FirstOrDefault() ?? m.Movie.Title,
                    PosterPath = m.Movie.Translations.Where(t => t.Language == lang).Select(t => t.PosterPath).FirstOrDefault() ?? m.Movie.PosterPath
                },
                Watchlist = m.Watchlist == null ? null : new SharedWatchlistDto
                {
                    Id = m.Watchlist.Id,
                    Name = m.Watchlist.Name,
                    OwnerUsername = m.Watchlist.User == null ? null : m.Watchlist.User.UserName
                }
            };

        [HttpPost]
        // Every message creates a notification + push for the recipient; a strict limit against spam.
        [EnableRateLimiting(RateLimitPolicies.SocialWrite)]
        public async Task<ActionResult<MessageThreadItemDto>> CreateMessage(CreateMessageDto createMessageDto)
        {
            var senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (senderId == null) return Unauthorized();

            var sender = await _context.Users.FindAsync(senderId);
            if (sender == null) return Unauthorized();

            if (senderId == createMessageDto.RecipientId)
                return BadRequest(_t["messages.cannotMessageSelf"].Value);

            if (string.IsNullOrWhiteSpace(createMessageDto.Content) && !createMessageDto.MovieId.HasValue && !createMessageDto.WatchlistId.HasValue)
                return BadRequest(_t["messages.contentRequired"].Value);

            if (createMessageDto.WatchlistId.HasValue)
            {
                var listExists = await _context.Watchlists.AnyAsync(w => w.Id == createMessageDto.WatchlistId.Value && w.UserId == senderId);
                if (!listExists)
                {
                    // Forbid("...") took the text for an authentication scheme and returned 500.
                    throw new DomainException("messages.cannotShareOthersList", StatusCodes.Status403Forbidden);
                }
            }

            var recipient = await _context.Users.FindAsync(createMessageDto.RecipientId);
            if (recipient == null) return NotFound(_t["messages.recipientNotFound"].Value);

            // Blocks and the recipient's message permission (G12); 403 on violation.
            await _privacy.EnsureCanMessageAsync(senderId, recipient);

            var message = new Message
            {
                SenderId = senderId,
                RecipientId = createMessageDto.RecipientId,
                Content = createMessageDto.Content,
                MovieId = createMessageDto.MovieId,
                WatchlistId = createMessageDto.WatchlistId
            };
            await _context.Messages.AddAsync(message);

            // The message and the recipient's notification are saved together;
            // then SignalR + push. Tapping it opens the chat with the sender.
            await _notifications.NotifyAsync(
                createMessageDto.RecipientId,
                $"/messages/{senderId}",
                "notifications.newMessage", sender.UserName);

            // Live chat (M11): the message goes to both sides. The sender gets
            // it too so the same account's other open tabs/devices update; the
            // requesting client de-duplicates it by id against the response.
            var created = await _context.Messages
                .Where(m => m.Id == message.Id)
                .Select(ToThreadItem(SupportedLanguages.Current))
                .SingleAsync();

            await _hubContext.Clients.Users(senderId, createMessageDto.RecipientId)
                .SendAsync(ReceiveMessageEvent, created);

            return Ok(created);
        }

        // Paginated so the mobile client does not download without limit.
        // Without page/pageSize the latest page is returned (100 messages by default).
        [HttpGet("thread/{otherUserId}")]
        public async Task<ActionResult<List<MessageThreadItemDto>>> GetMessageThread(
            string otherUserId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var messages = await _context.Messages
                .Where(m => (m.RecipientId == currentUserId && m.SenderId == otherUserId) ||
                            (m.RecipientId == otherUserId && m.SenderId == currentUserId))
                .OrderByDescending(m => m.MessageSent)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToThreadItem(SupportedLanguages.Current))
                .ToListAsync();

            // Queried newest first; the client expects oldest first.
            messages.Reverse();

            if (!await ReadReceiptsVisibleAsync(currentUserId, otherUserId))
            {
                foreach (var message in messages) message.DateRead = null;
            }

            return Ok(messages);
        }

        // Marks the conversation as read. Message.DateRead existed but was never
        // set anywhere, so the "read" indicator was always wrong.
        [HttpPost("thread/{otherUserId}/mark-as-read")]
        public async Task<ActionResult<MarkedAsReadDto>> MarkThreadAsRead(string otherUserId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            var unread = await _context.Messages
                .Where(m => m.RecipientId == currentUserId && m.SenderId == otherUserId && m.DateRead == null)
                .ToListAsync();

            if (unread.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var message in unread)
                {
                    message.DateRead = now;
                }
                await _context.SaveChangesAsync();

                // The reader's other tabs/devices are always told; the sender
                // only if both sides keep read receipts on.
                var receivers = await ReadReceiptsVisibleAsync(currentUserId, otherUserId)
                    ? new[] { currentUserId, otherUserId }
                    : new[] { currentUserId };
                await _hubContext.Clients.Users(receivers).SendAsync(MessagesReadEvent, new MessagesReadDto
                {
                    ReaderId = currentUserId,
                    OtherUserId = otherUserId,
                    LastReadMessageId = unread.Max(m => m.Id),
                    ReadAt = now
                });
            }

            return Ok(new MarkedAsReadDto { MarkedAsRead = unread.Count });
        }

        private Task<bool> ReadReceiptsVisibleAsync(string userId, string otherUserId) =>
            _context.Users
                .Where(u => u.Id == userId || u.Id == otherUserId)
                .AllAsync(u => u.ShowReadReceipts);

        [HttpGet]
        public async Task<ActionResult<List<ConversationDto>>> GetConversations(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var mine = _context.Messages
                .Where(m => m.SenderId == currentUserId || m.RecipientId == currentUserId);

            // The last message time per conversation is computed in the
            // database; the user's messages are not all loaded into memory.
            // Pagination happens in the database too.
            var pagedPartners = await mine
                .GroupBy(m => m.SenderId == currentUserId ? m.RecipientId : m.SenderId)
                .Select(g => new { PartnerId = g.Key, LastSent = g.Max(m => m.MessageSent) })
                .OrderByDescending(x => x.LastSent)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (pagedPartners.Count == 0)
            {
                return Ok(new List<ConversationDto>());
            }

            // Load only the last messages of this page's conversations (with their relations).
            var partnerIds = pagedPartners.Select(p => p.PartnerId).ToList();
            var lastSents = pagedPartners.Select(p => p.LastSent).ToList();

            var candidates = await mine
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Include(m => m.Watchlist)
                .Where(m =>
                    lastSents.Contains(m.MessageSent) &&
                    (partnerIds.Contains(m.SenderId) || partnerIds.Contains(m.RecipientId)))
                .ToListAsync();

            var conversations = new List<ConversationDto>(pagedPartners.Count);
            foreach (var partner in pagedPartners)
            {
                // The message with this partner that matches the last send time.
                var lastMessage = candidates.FirstOrDefault(m =>
                    m.MessageSent == partner.LastSent &&
                    (m.SenderId == partner.PartnerId || m.RecipientId == partner.PartnerId));
                if (lastMessage == null) continue;

                var otherUser = lastMessage.SenderId == currentUserId
                    ? lastMessage.Recipient
                    : lastMessage.Sender;

                string lastMessageContent = _t["messages.previewMovie"];
                if (lastMessage.WatchlistId.HasValue)
                {
                    lastMessageContent = _t["messages.previewWatchlist", lastMessage.Watchlist?.Name ?? string.Empty];
                }
                else if (!string.IsNullOrEmpty(lastMessage.Content))
                {
                    lastMessageContent = lastMessage.Content;
                }

                conversations.Add(new ConversationDto
                {
                    OtherUserId = otherUser.Id,
                    OtherUserUsername = otherUser.UserName,
                    OtherUserProfileImageUrl = otherUser.ProfileImageUrl,
                    LastMessageContent = lastMessageContent,
                    LastMessageSent = lastMessage.MessageSent,
                    IsRead = lastMessage.DateRead != null || lastMessage.SenderId == currentUserId
                });
            }

            return Ok(conversations);
        }
    }
}

