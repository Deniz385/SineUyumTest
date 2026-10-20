using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;

namespace SineUyum.Api.Services
{
    public class MatchingOptions
    {
        // Sinyal kaçsa (ör. başka sunucu örneği kuyruğa ekledi) bile işler bu
        // aralıkla yoklanır.
        public int PollSeconds { get; set; } = 15;

        // Bu kadar süredir "Running" kalan iş, onu alan süreç çöktü sayılıp
        // yeniden kuyruğa alınır. Bir eşleştirme normalde saniyeler sürer.
        public int StaleRunMinutes { get; set; } = 10;
    }

    // İşçiyi uyandırmak için hafif sinyal. Asıl kuyruk veritabanıdır
    // (MatchingRuns): yeniden başlatmada kaybolmaz.
    public sealed class MatchingQueue
    {
        private readonly Channel<int> _signals = Channel.CreateBounded<int>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

        public void Signal() => _signals.Writer.TryWrite(0);

        public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            try
            {
                await _signals.Reader.ReadAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Zaman aşımı: yoklama zamanı.
            }
        }
    }

    // Eşleştirme işlerini (M7) istek yolunun dışında, sırayla işler.
    public sealed class MatchingWorker : BackgroundService
    {
        public const string UnexpectedError = "Eşleştirme sırasında beklenmeyen bir hata oluştu. Tekrar deneyebilirsiniz.";

        private readonly MatchingQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<MatchingOptions> _options;
        private readonly ILogger<MatchingWorker> _logger;

        public MatchingWorker(MatchingQueue queue, IServiceScopeFactory scopeFactory, IOptions<MatchingOptions> options, ILogger<MatchingWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RequeueStaleRunsAsync(stoppingToken);
                    while (await TryProcessNextAsync(stoppingToken))
                    {
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Veritabanı geçici olarak yoksa servis durmasın (.NET 8'de
                    // BackgroundService'ten kaçan istisna uygulamayı kapatır).
                    _logger.LogError(ex, "Eşleştirme kuyruğu işlenemedi; yeniden denenecek.");
                }

                await _queue.WaitAsync(TimeSpan.FromSeconds(Math.Max(1, _options.Value.PollSeconds)), stoppingToken);
            }
        }

        private async Task RequeueStaleRunsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cutoff = DateTime.UtcNow.AddMinutes(-_options.Value.StaleRunMinutes);

            var requeued = await db.MatchingRuns
                .Where(r => r.Status == MatchingRunStatus.Running && r.StartedAt < cutoff)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(r => r.Status, MatchingRunStatus.Queued)
                    .SetProperty(r => r.StartedAt, (DateTime?)null), ct);
            if (requeued > 0)
            {
                _logger.LogWarning("{Count} yarım kalmış eşleştirme işi yeniden kuyruğa alındı.", requeued);
            }
        }

        // Sıradaki işi alır ve işler; iş yoksa false.
        private async Task<bool> TryProcessNextAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var candidate = await db.MatchingRuns
                .Where(r => r.Status == MatchingRunStatus.Queued)
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .Select(r => new { r.Id, r.CinemaEventId })
                .FirstOrDefaultAsync(ct);
            if (candidate == null) return false;

            // Atomik sahiplenme: birden çok sunucu örneği aynı işi almaz.
            var claimed = await db.MatchingRuns
                .Where(r => r.Id == candidate.Id && r.Status == MatchingRunStatus.Queued)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(r => r.Status, MatchingRunStatus.Running)
                    .SetProperty(r => r.StartedAt, DateTime.UtcNow), ct);
            if (claimed == 0) return true; // başkası aldı; sıradakine bak

            MatchingRunStatus status;
            int? groupCount = null, ungroupedCount = null;
            string? error = null;
            try
            {
                var result = await scope.ServiceProvider.GetRequiredService<MatchingService>()
                    .CreateGroupsForEventAsync(candidate.CinemaEventId, ct);
                status = MatchingRunStatus.Succeeded;
                groupCount = result.GroupCount;
                ungroupedCount = result.UngroupedCount;
                _logger.LogInformation("Etkinlik {EventId} eşleştirildi: {Groups} grup, {Ungrouped} kişi grupsuz.",
                    candidate.CinemaEventId, result.GroupCount, result.UngroupedCount);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Kapanış: iş "Running" kalır ve süre aşımında yeniden alınır;
                // yazma tek transaction olduğu için yarım grup kalmaz.
                throw;
            }
            catch (DomainException ex)
            {
                status = MatchingRunStatus.Failed;
                error = ex.Message;
            }
            catch (Exception ex)
            {
                // İç ayrıntı yalnızca log'a; kayıtta genel mesaj.
                _logger.LogError(ex, "Etkinlik {EventId} eşleştirilemedi.", candidate.CinemaEventId);
                status = MatchingRunStatus.Failed;
                error = UnexpectedError;
            }

            await db.MatchingRuns
                .Where(r => r.Id == candidate.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(r => r.Status, status)
                    .SetProperty(r => r.FinishedAt, DateTime.UtcNow)
                    .SetProperty(r => r.GroupCount, groupCount)
                    .SetProperty(r => r.UngroupedCount, ungroupedCount)
                    .SetProperty(r => r.Error, error), CancellationToken.None);
            return true;
        }
    }
}
