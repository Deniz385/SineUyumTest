// SineUyum.Api/Controllers/MessageController.cs
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
    public class MessageController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MessageController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateMessage(CreateMessageDto createMessageDto)
        {
            var senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (senderId == null) return Unauthorized();

            if (senderId == createMessageDto.RecipientId)
                return BadRequest("Kendinize mesaj gönderemezsiniz.");

            var recipient = await _context.Users.FindAsync(createMessageDto.RecipientId);
            if (recipient == null) return NotFound("Alıcı kullanıcı bulunamadı.");

            var message = new Message
            {
                SenderId = senderId,
                RecipientId = createMessageDto.RecipientId,
                Content = createMessageDto.Content,
            };

            await _context.Messages.AddAsync(message);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Mesaj başarıyla gönderildi." });
        }

        [HttpGet("thread/{otherUserId}")]
        public async Task<IActionResult> GetMessageThread(string otherUserId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null) return Unauthorized();

            var messages = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Where(m => (m.RecipientId == currentUserId && m.SenderId == otherUserId) ||
                            (m.RecipientId == otherUserId && m.SenderId == currentUserId))
                .OrderBy(m => m.MessageSent)
                .Select(m => new 
                {
                    m.Id,
                    m.SenderId,
                    SenderUsername = m.Sender.UserName,
                    SenderProfileImageUrl = m.Sender.ProfileImageUrl,
                    m.RecipientId,
                    RecipientUsername = m.Recipient.UserName,
                    m.Content,
                    m.MessageSent
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
                .Where(m => m.SenderId == currentUserId || m.RecipientId == currentUserId)
                .OrderByDescending(m => m.MessageSent)
                .ToListAsync();

            var conversations = messages
                .GroupBy(m => m.SenderId == currentUserId ? m.RecipientId : m.SenderId)
                .Select(g => 
                {
                    var lastMessage = g.First();
                    var otherUser = lastMessage.SenderId == currentUserId ? lastMessage.Recipient : lastMessage.Sender;
                    return new 
                    {
                        OtherUserId = otherUser.Id,
                        OtherUserUsername = otherUser.UserName,
                        OtherUserProfileImageUrl = otherUser.ProfileImageUrl,
                        LastMessageContent = lastMessage.Content,
                        LastMessageSent = lastMessage.MessageSent,
                        IsRead = lastMessage.DateRead != null || lastMessage.SenderId == currentUserId
                    };
                })
                .ToList();

            return Ok(conversations);
        }
    }
}