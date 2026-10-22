using Microsoft.Extensions.Localization;
using SineUyum.Api.Localization;
using SineUyum.Api.Models;

namespace SineUyum.Api.Dtos
{
    // REST (/api/v1/notification) and SignalR ("ReceiveNotification") share
    // this contract. The entity used to be pushed directly; once EF relationship
    // fix-up filled Notification.User, the recipient's PasswordHash,
    // SecurityStamp and email went to the client.
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? RelatedUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        // With the stored text (rendered in the recipient's language).
        public static NotificationDto From(Notification n) => From(n, n.Message);

        // Rendered with the given localizer; rows from before localization
        // have no key and keep their stored text.
        public static NotificationDto From(Notification n, IStringLocalizer t) =>
            From(n, t, MessageArgs.Deserialize(n.MessageArgs));

        // With arguments already prepared (e.g. movie titles in the request language).
        public static NotificationDto From(Notification n, IStringLocalizer t, object?[] args) =>
            From(n, n.MessageKey == null ? n.Message : t[n.MessageKey, args.Select(a => a ?? string.Empty).ToArray()]);

        private static NotificationDto From(Notification n, string message) => new()
        {
            Id = n.Id,
            Message = message,
            RelatedUrl = n.RelatedUrl,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        };
    }
}
