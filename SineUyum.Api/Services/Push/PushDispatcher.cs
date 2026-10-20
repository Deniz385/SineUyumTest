using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;

namespace SineUyum.Api.Services.Push
{
    // İstek yolunda FCM'i beklememek için push'lar kuyruğa atılır ve arka
    // planda gönderilir. Kuyruk bellek içidir: sunucu yeniden başlarsa
    // bekleyenler düşer. Bu kabul edilebilir çünkü bildirim zaten veritabanında
    // ve uygulama içinde (REST/SignalR) görünür; push yalnızca "haber verme"dir.
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
                _logger.LogWarning("Push kuyruğu dolu; {UserId} için bildirim atlandı.", message.UserId);
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
                    // Tek bir bildirimin hatası servisi durdurmamalı (.NET 8'de
                    // BackgroundService'ten kaçan istisna uygulamayı kapatır).
                    _logger.LogError(ex, "{UserId} için push gönderilemedi.", message.UserId);
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
                _logger.LogInformation("{Count} geçersiz cihaz token'ı silindi.", invalid.Count);
            }
        }
    }
}
