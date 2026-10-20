using SineUyum.Api.Models;

namespace SineUyum.Api.Dtos
{
    // REST (/api/v1/notification) ve SignalR ("ReceiveNotification") aynı
    // sözleşmeyi kullanır. Önceden varlık doğrudan itiliyordu; EF ilişki
    // düzeltmesi Notification.User'ı doldurduğunda alıcının PasswordHash,
    // SecurityStamp ve e-postası istemciye gidiyordu.
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? RelatedUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public static NotificationDto From(Notification n) => new()
        {
            Id = n.Id,
            Message = n.Message,
            RelatedUrl = n.RelatedUrl,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        };
    }
}
