using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;

namespace SineUyum.Api.Services.Push
{
    // Pushes are queued and sent in the background so the request path does not
    // wait for FCM. The queue is in memory: pending pushes are lost on restart.
    // That is acceptable because the notification is already in the database and
    // visible in the app (REST/SignalR); a push is only a "heads-up".
    public sealed class PushQueue
    {
        private readonly Channel<PushMessage> _channel = Channel.CreateBounded<PushMessage>(
            new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
            });

        private readonly ILogger<PushQueue> _logger;

        public PushQueue(ILogger<PushQueue> logger) => _logger = logger;

        public void Enqueue(PushMessage message)
        {
            if (!_channel.Writer.TryWrite(message))
            {
                _logger.LogWarning("Push queue is full; skipped a notification for {UserId}.", message.UserId);
            }
        }

        public IAsyncEnumerable<PushMessage> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
    }

    public sealed class PushDispatchService : BackgroundService
    {
        private readonly PushQueue _queue;
        private readonly IPushSender _sender;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PushDispatchService> _logger;

        public PushDispatchService(
            PushQueue queue,
            IPushSender sender,
            IServiceScopeFactory scopeFactory,
            ILogger<PushDispatchService> logger)
        {
            _queue = queue;
            _sender = sender;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var message in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await DispatchAsync(message, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // One failed notification must not stop the service (in .NET 8 an
                    // exception escaping a BackgroundService shuts the application down).
                    _logger.LogError(ex, "Could not send a push to {UserId}.", message.UserId);
                }
            }
        }

        private async Task DispatchAsync(PushMessage message, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var tokens = await db.DeviceTokens
                .Where(d => d.UserId == message.UserId)
                .Select(d => d.Token)
                .ToListAsync(ct);

            var invalid = new List<string>();
            foreach (var token in tokens)
            {
                if (await _sender.SendAsync(token, message, ct) == PushSendResult.InvalidToken)
                {
                    invalid.Add(token);
                }
            }

            if (invalid.Count > 0)
            {
                await db.DeviceTokens.Where(d => invalid.Contains(d.Token)).ExecuteDeleteAsync(ct);
                _logger.LogInformation("Deleted {Count} invalid device tokens.", invalid.Count);
            }
        }
    }
}
