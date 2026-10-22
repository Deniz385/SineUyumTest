using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Hubs;
using SineUyum.Api.Localization;
using SineUyum.Api.Models;
using SineUyum.Api.Services.Push;

namespace SineUyum.Api.Services
{
    // The only way to create a notification: save → SignalR (app open) → push
    // (app closed). These three steps used to be copied in three places, and
    // one of them pushed the entity (with the user's details).
    //
    // A notification stores a locale key + arguments. The in-app list renders
    // it in the request language; the SignalR event and the push are rendered
    // in the recipient's stored language (AppUser.Language), which is also
    // written to Message for backward compatibility.
    public class NotificationService
    {
        public const string PushTitle = "SineUyum";

        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IPushSender _pushSender;
        private readonly PushQueue _pushQueue;
        private readonly LocaleCatalog _catalog;
        private readonly SupportedLanguages _languages;

        public NotificationService(
            ApplicationDbContext context,
            IHubContext<NotificationHub> hubContext,
            IPushSender pushSender,
            PushQueue pushQueue,
            LocaleCatalog catalog,
            SupportedLanguages languages)
        {
            _context = context;
            _hubContext = hubContext;
            _pushSender = pushSender;
            _pushQueue = pushQueue;
            _catalog = catalog;
            _languages = languages;
        }

        public Task NotifyAsync(string userId, string? relatedUrl, string messageKey, params object?[] messageArgs) =>
            NotifyManyAsync(new[] { userId }, relatedUrl, messageKey, messageArgs);

        // CAUTION: also saves the pending changes in the context (e.g. a new
        // message or follow) in the SAME SaveChanges as the notifications, so a
        // half state like "message saved but no notification" cannot happen.
        public async Task NotifyManyAsync(IEnumerable<string> userIds, string? relatedUrl, string messageKey, params object?[] messageArgs)
        {
            var notifications = await StageAsync(userIds, relatedUrl, messageKey, messageArgs);
            await _context.SaveChangesAsync();
            await PublishAsync(notifications);
        }

        // Only adds the notifications to the context; the caller saves them with
        // its own work (e.g. inside a transaction) and calls PublishAsync AFTER
        // saving. That way nobody is notified about work that was rolled back.
        public async Task<IReadOnlyList<Notification>> StageAsync(IEnumerable<string> userIds, string? relatedUrl, string messageKey, params object?[] messageArgs)
        {
            var ids = userIds.Distinct().ToList();
            var languages = await _context.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.Language })
                .ToDictionaryAsync(u => u.Id, u => u.Language);
            var args = MessageArgs.Serialize(messageArgs);

            var notifications = ids.Select(userId => new Notification
            {
                UserId = userId,
                Message = _catalog.Translate(_languages.Resolve(languages.GetValueOrDefault(userId)), messageKey, messageArgs),
                MessageKey = messageKey,
                MessageArgs = args,
                RelatedUrl = relatedUrl,
            }).ToList();

            _context.Notifications.AddRange(notifications);
            return notifications;
        }

        public async Task PublishAsync(IEnumerable<Notification> notifications)
        {
            foreach (var notification in notifications)
            {
                // Pushed AFTER saving so the client gets the real id. Message is
                // already in the recipient's language.
                await _hubContext.Clients.User(notification.UserId)
                    .SendAsync("ReceiveNotification", NotificationDto.From(notification));

                if (_pushSender.IsEnabled)
                {
                    var data = new Dictionary<string, string>
                    {
                        ["notificationId"] = notification.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    };
                    if (notification.RelatedUrl != null) data["relatedUrl"] = notification.RelatedUrl;

                    _pushQueue.Enqueue(new PushMessage(notification.UserId, PushTitle, notification.Message, data));
                }
            }
        }
    }
}
