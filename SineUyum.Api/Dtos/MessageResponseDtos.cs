namespace SineUyum.Api.Dtos
{
    // A single message in a conversation.
    public class MessageThreadItemDto
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string? SenderUsername { get; set; }
        public string RecipientId { get; set; } = string.Empty;
        public string? RecipientUsername { get; set; }
        public string? Content { get; set; }
        public DateTime MessageSent { get; set; }
        // When the recipient read it. Always null if either side turned read
        // receipts off.
        public DateTime? DateRead { get; set; }
        public MovieSummaryDto? Movie { get; set; }
        public SharedWatchlistDto? Watchlist { get; set; }
    }

    // SignalR "MessagesRead": ReaderId read OtherUserId's messages up to
    // LastReadMessageId at ReadAt.
    public class MessagesReadDto
    {
        public string ReaderId { get; set; } = string.Empty;
        public string OtherUserId { get; set; } = string.Empty;
        public int LastReadMessageId { get; set; }
        public DateTime ReadAt { get; set; }
    }

    // A list shared in a message.
    public class SharedWatchlistDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? OwnerUsername { get; set; }
    }

    // A row in the conversation list (the other side + the last message).
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
