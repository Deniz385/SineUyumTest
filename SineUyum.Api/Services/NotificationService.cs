using Microsoft.AspNetCore.SignalR;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Hubs;
using SineUyum.Api.Models;
using SineUyum.Api.Services.Push;

namespace SineUyum.Api.Services
{
    // Bildirim oluşturmanın tek yolu: kaydet → SignalR (uygulama açıkken) →
    // push (uygulama kapalıyken). Önceden bu üç adım üç ayrı yerde
    // kopyalanmıştı ve biri varlığı (kullanıcı bilgileriyle) itiyordu.
    public class NotificationService
    {
        public const string PushTitle = "SineUyum";

        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IPushSender _pushSender;
        private readonly PushQueue _pushQueue;

        public NotificationService(
            ApplicationDbContext context,
            IHubContext<NotificationHub> hubContext,
            IPushSender pushSender,
            PushQueue pushQueue)
        {
            _context = context;
            _hubContext = hubContext;
            _pushSender = pushSender;
            _pushQueue = pushQueue;
        }

        public Task NotifyAsync(string userId, string message, string? relatedUrl) =>
            NotifyManyAsync(new[] { userId }, message, relatedUrl);

        // DİKKAT: Bağlamdaki bekleyen değişiklikleri de (ör. yeni mesaj, takip)
        // bildirimlerle birlikte TEK SaveChanges'te kaydeder; böylece "mesaj
        // kaydedildi ama bildirimi yok" gibi yarım durumlar oluşmaz.
        public async Task NotifyManyAsync(IEnumerable<string> userIds, string message, string? relatedUrl)
        {
            var notifications = Stage(userIds, message, relatedUrl);
            await _context.SaveChangesAsync();
            await PublishAsync(notifications);
        }

        // Bildirimleri yalnızca bağlama ekler; çağıran kendi işiyle (ör. bir
        // transaction içinde) kaydeder ve kayıttan SONRA PublishAsync çağırır.
        // Böylece geri alınan bir iş için kimseye bildirim gitmez.
        public IReadOnlyList<Notification> Stage(IEnumerable<string> userIds, string message, string? relatedUrl)
        {
            var notifications = userIds.Distinct().Select(userId => new Notification
            {
                UserId = userId,
                Message = message,
                RelatedUrl = relatedUrl,
            }).ToList();

            _context.Notifications.AddRange(notifications);
            return notifications;
        }

        public async Task PublishAsync(IEnumerable<Notification> notifications)
        {
            foreach (var notification in notifications)
            {
                // Kayıttan SONRA itilir ki istemci gerçek id'yi alsın.
                await _hubContext.Clients.User(notification.UserId)
                    .SendAsync("ReceiveNotification", NotificationDto.From(notification));

                if (_pushSender.IsEnabled)
                {
                    var data = new Dictionary<string, string>
                    {
                        ["notificationId"] = notification.Id.ToString(),
                    };
                    if (notification.RelatedUrl != null) data["relatedUrl"] = notification.RelatedUrl;

                    _pushQueue.Enqueue(new PushMessage(notification.UserId, PushTitle, notification.Message, data));
                }
            }
        }
    }
}
