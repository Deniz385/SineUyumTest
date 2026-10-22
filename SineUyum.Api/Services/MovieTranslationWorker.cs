using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SineUyum.Api.Data;
using SineUyum.Api.Localization;

namespace SineUyum.Api.Services
{
    public class MovieTranslationOptions
    {
        public bool Enabled { get; set; } = true;
        // Wait at startup so TMDb is not hammered while the server comes up.
        public int InitialDelaySeconds { get; set; } = 90;
        public int IntervalMinutes { get; set; } = 10;
    }

    // Fills in movie titles in the supported languages that have none yet
    // (older movies were stored in Turkish only; a new language starts with
    // none). Small batches keep the TMDb quota safe; most titles are added
    // earlier, when someone opens or rates the movie in that language. It does
    // nothing without TMDb configured.
    public sealed class MovieTranslationWorker : BackgroundService
    {
        private const int BatchSize = 40;
        private static readonly TimeSpan BetweenCalls = TimeSpan.FromMilliseconds(250);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly IOptions<MovieTranslationOptions> _options;
        private readonly SupportedLanguages _languages;
        private readonly ILogger<MovieTranslationWorker> _logger;

        public MovieTranslationWorker(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            IOptions<MovieTranslationOptions> options,
            SupportedLanguages languages,
            ILogger<MovieTranslationWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _options = options;
            _languages = languages;
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
                    _logger.LogWarning(ex, "Movie title batch failed; will retry later.");
                }

                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.IntervalMinutes)), stoppingToken);
            }
        }

        // At most BatchSize titles per round, across all languages.
        public async Task<int> RunBatchAsync(CancellationToken ct)
        {
            var missing = new List<(int MovieId, string Language)>();
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                foreach (var language in _languages.All)
                {
                    if (missing.Count >= BatchSize) break;
                    var ids = await db.Movies
                        .Where(m => !m.Translations.Any(t => t.Language == language))
                        .OrderBy(m => m.Id)
                        .Select(m => m.Id)
                        .Take(BatchSize - missing.Count)
                        .ToListAsync(ct);
                    missing.AddRange(ids.Select(id => (id, language)));
                }
            }

            var filled = 0;
            foreach (var (movieId, language) in missing)
            {
                ct.ThrowIfCancellationRequested();
                using var scope = _scopeFactory.CreateScope();
                try
                {
                    await scope.ServiceProvider.GetRequiredService<MovieCatalogService>().FillTranslationAsync(movieId, language);
                    filled++;
                }
                catch (Exceptions.DomainException ex)
                {
                    // TMDb unreachable or failing: stop this round, try later.
                    _logger.LogInformation("Movie title round stopped: {Error}", ex.MessageKey);
                    break;
                }
                await Task.Delay(BetweenCalls, ct);
            }

            if (filled > 0) _logger.LogInformation("Filled in {Count} movie titles.", filled);
            return filled;
        }
    }
}
