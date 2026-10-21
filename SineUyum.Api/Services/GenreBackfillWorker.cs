using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SineUyum.Api.Data;

namespace SineUyum.Api.Services
{
    public class GenreBackfillOptions
    {
        public bool Enabled { get; set; } = true;
        // Açılışta beklenir: sunucu ayağa kalkarken TMDb'ye yüklenilmesin.
        public int InitialDelaySeconds { get; set; } = 60;
        public int IntervalMinutes { get; set; } = 30;
    }

    // TMDb ortalaması da aynı istekten gelir ve saklanır.
    // Tür bilgisi önceden hiç yazılmadığı için mevcut filmlerin türleri boştu;
    // uyum açıklaması, film kimliği ve tür bazlı öneriler buna dayanır. Bu iş
    // türü işlenmemiş filmleri küçük partilerle TMDb'den tamamlar (kota
    // dostu). TMDb yapılandırılmamışsa hiçbir şey yapmaz.
    public sealed class GenreBackfillWorker : BackgroundService
    {
        private const int BatchSize = 40;
        private static readonly TimeSpan BetweenCalls = TimeSpan.FromMilliseconds(250);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly IOptions<GenreBackfillOptions> _options;
        private readonly ILogger<GenreBackfillWorker> _logger;

        public GenreBackfillWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, IOptions<GenreBackfillOptions> options, ILogger<GenreBackfillWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var hasKey = !string.IsNullOrWhiteSpace(_configuration["TMDb:ApiKey"]) ||
                         !string.IsNullOrWhiteSpace(_configuration["TMDB_API_KEY"]);
            var options = _options.Value;
            if (!hasKey || !options.Enabled) return;

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, options.InitialDelaySeconds)), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Tür tamamlama partisi başarısız; sonra yeniden denenecek.");
                }

                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.IntervalMinutes)), stoppingToken);
            }
        }

        public async Task<int> RunBatchAsync(CancellationToken ct)
        {
            List<int> ids;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                ids = await db.Movies
                    .Where(m => m.GenresSyncedAt == null)
                    .OrderBy(m => m.Id)
                    .Select(m => m.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);
            }

            var synced = 0;
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                using var scope = _scopeFactory.CreateScope();
                try
                {
                    await scope.ServiceProvider.GetRequiredService<MovieCatalogService>().SyncGenresAsync(id);
                    synced++;
                }
                catch (Exceptions.DomainException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
                {
                    // TMDb'de yok: işlenmiş say, bir daha sorma.
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await db.Movies.Where(m => m.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(m => m.GenresSyncedAt, DateTime.UtcNow), ct);
                }
                await Task.Delay(BetweenCalls, ct);
            }

            if (synced > 0) _logger.LogInformation("{Count} filmin türleri tamamlandı.", synced);
            return synced;
        }
    }
}
