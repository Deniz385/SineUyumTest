namespace SineUyum.Api.Dtos
{
    // Sohbet içindeki tek mesaj.
    public class MessageThreadItemDto
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string? SenderUsername { get; set; }
        public string RecipientId { get; set; } = string.Empty;
        public string? RecipientUsername { get; set; }
        public string? Content { get; set; }
        public DateTime MessageSent { get; set; }
        // Alıcının okuduğu an. İki taraftan biri okundu bilgisini kapattıysa
        // her zaman null.
        public DateTime? DateRead { get; set; }
        public MovieSummaryDto? Movie { get; set; }
        public SharedWatchlistDto? Watchlist { get; set; }
    }

    // SignalR "MessagesRead": ReaderId, OtherUserId'nin LastReadMessageId'ye
    // kadarki mesajlarını ReadAt'te okudu.
    public class MessagesReadDto
    {
        public string ReaderId { get; set; } = string.Empty;
        public string OtherUserId { get; set; } = string.Empty;
        public int LastReadMessageId { get; set; }
        public DateTime ReadAt { get; set; }
    }

    // Mesajla paylaşılan liste.
    public class SharedWatchlistDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? OwnerUsername { get; set; }
    }

    // Sohbet listesindeki satır (karşı taraf + son mesaj).
    public class ConversationDto
    {
        public string OtherUserId { get; set; } = string.Empty;
        public string? OtherUserUsername { get; set; }
        public string? OtherUserProfileImageUrl { get; set; }
        public string LastMessageContent { get; set; } = string.Empty;
        public DateTime LastMessageSent { get; set; }
        public bool IsRead { get; set; }
    }

    public class MarkedAsReadDto
    {
        public int MarkedAsRead { get; set; }
    }
}
