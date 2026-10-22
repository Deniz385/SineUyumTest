using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Localization;
using SineUyum.Api.Models;

namespace SineUyum.Api.Services
{
    public class MatchingOptions
    {
        // Jobs are polled at this interval even if a signal is missed (e.g.
        // another server instance queued the job).
        public int PollSeconds { get; set; } = 15;

        // A job "Running" for this long is considered abandoned by a crashed
        // process and is queued again. A matching normally takes seconds.
        public int StaleRunMinutes { get; set; } = 10;
    }

    // A lightweight signal to wake the worker. The real queue is the database
    // (MatchingRuns): it is not lost on restart.
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
                // Timeout: time to poll.
            }
        }
    }

    // Processes matching jobs (M7) one by one, outside the request path.
    public sealed class MatchingWorker : BackgroundService
    {
        // Locale key stored for unexpected failures (details only go to the log).
        public const string UnexpectedErrorKey = "matching.unexpectedError";

        private readonly MatchingQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<MatchingOptions> _options;
        private readonly ILogger<MatchingWorker> _logger;
        private readonly LocaleCatalog _catalog;

        public MatchingWorker(MatchingQueue queue, IServiceScopeFactory scopeFactory, IOptions<MatchingOptions> options, ILogger<MatchingWorker> logger, LocaleCatalog catalog)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
            _catalog = catalog;
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
                    // If the database is temporarily unavailable the service must
                    // not stop (in .NET 8 an exception escaping a BackgroundService
                    // shuts the application down).
                    _logger.LogError(ex, "Could not process the matching queue; will retry.");
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
                _logger.LogWarning("Requeued {Count} abandoned matching jobs.", requeued);
            }
        }

        // Takes the next job and processes it; false when there is none.
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

            // Atomic claim: several server instances never take the same job.
            var claimed = await db.MatchingRuns
                .Where(r => r.Id == candidate.Id && r.Status == MatchingRunStatus.Queued)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(r => r.Status, MatchingRunStatus.Running)
                    .SetProperty(r => r.StartedAt, DateTime.UtcNow), ct);
            if (claimed == 0) return true; // someone else took it; look at the next one

            MatchingRunStatus status;
            int? groupCount = null, ungroupedCount = null;
            string? errorKey = null;
            object?[] errorArgs = Array.Empty<object?>();
            try
            {
                var result = await scope.ServiceProvider.GetRequiredService<MatchingService>()
                    .CreateGroupsForEventAsync(candidate.CinemaEventId, ct);
                status = MatchingRunStatus.Succeeded;
                groupCount = result.GroupCount;
                ungroupedCount = result.UngroupedCount;
                _logger.LogInformation("Event {EventId} matched: {Groups} groups, {Ungrouped} people without a group.",
                    candidate.CinemaEventId, result.GroupCount, result.UngroupedCount);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shutdown: the job stays "Running" and is picked up again after
                // the timeout; writing is a single transaction, so no half groups remain.
                throw;
            }
            catch (DomainException ex)
            {
                status = MatchingRunStatus.Failed;
                errorKey = ex.MessageKey;
                errorArgs = ex.MessageArgs;
            }
            catch (Exception ex)
            {
                // Internal details only go to the log; the row gets a generic message.
                _logger.LogError(ex, "Could not match event {EventId}.", candidate.CinemaEventId);
                status = MatchingRunStatus.Failed;
                errorKey = UnexpectedErrorKey;
            }

            // The key is rendered in each reader's language; Error keeps the English text.
            var error = errorKey == null ? null : _catalog.Translate(LocaleCatalog.DefaultLanguage, errorKey, errorArgs);
            if (error is { Length: > 500 }) error = error[..500];
            var errorArgsJson = errorKey == null ? null : MessageArgs.Serialize(errorArgs);

            await db.MatchingRuns
                .Where(r => r.Id == candidate.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(r => r.Status, status)
                    .SetProperty(r => r.FinishedAt, DateTime.UtcNow)
                    .SetProperty(r => r.GroupCount, groupCount)
                    .SetProperty(r => r.UngroupedCount, ungroupedCount)
                    .SetProperty(r => r.Error, error)
                    .SetProperty(r => r.ErrorKey, errorKey)
                    .SetProperty(r => r.ErrorArgs, errorArgsJson), CancellationToken.None);
            return true;
        }
    }
}
