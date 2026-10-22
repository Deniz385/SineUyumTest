using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SineUyum.Api.Data;

namespace SineUyum.Api.Services
{
    public class TasteTwinOptions
    {
        public bool Enabled { get; set; } = true;
        public int IntervalMinutes { get; set; } = 60;
        // At most this many users per round (to spread the load).
        public int BatchSize { get; set; } = 100;
    }

    // Every week, finds a taste twin for everyone with enough ratings and notifies them.
    public sealed class TasteTwinWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<TasteTwinOptions> _options;
        private readonly ILogger<TasteTwinWorker> _logger;
        // People without a suitable candidate this week are not recomputed every round.
        private readonly HashSet<(DateOnly, string)> _noMatch = new();

        public TasteTwinWorker(IServiceScopeFactory scopeFactory, IOptions<TasteTwinOptions> options, ILogger<TasteTwinWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Value.Enabled) return;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(DateTime.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Taste twin round failed; will retry later.");
                }
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.Value.IntervalMinutes)), stoppingToken);
            }
        }

        // Processes users not yet notified this week; returns how many were notified.
        public async Task<int> RunOnceAsync(DateTime nowUtc, CancellationToken ct)
        {
            var week = TasteTwinService.WeekStartOf(nowUtc);
            _noMatch.RemoveWhere(x => x.Item1 != week);

            List<string> userIds;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var notified = db.TasteTwinSuggestions.Where(t => t.WeekStart == week && t.Notified).Select(t => t.UserId);
                userIds = await db.UserRatings
                    .GroupBy(r => r.UserId)
                    .Where(g => g.Count() >= TasteTwinService.MinRatings)
                    .Select(g => g.Key)
                    .Where(id => !notified.Contains(id))
                    .OrderBy(id => id)
                    .ToListAsync(ct);
            }

            var count = 0;
            foreach (var userId in userIds.Where(id => !_noMatch.Contains((week, id))).Take(Math.Max(1, _options.Value.BatchSize)))
            {
                ct.ThrowIfCancellationRequested();
                using var scope = _scopeFactory.CreateScope();
                var twins = scope.ServiceProvider.GetRequiredService<TasteTwinService>();
                var suggestion = await twins.ForThisWeekAsync(userId, nowUtc);
                if (suggestion == null)
                {
                    _noMatch.Add((week, userId));
                    continue;
                }
                await twins.NotifyAsync(suggestion);
                count++;
            }
            if (count > 0) _logger.LogInformation("Notified {Count} users of their taste twin.", count);
            return count;
        }
    }
}
