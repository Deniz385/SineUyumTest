// SineUyum.Api/Services/MatchingService.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Localization;
using SineUyum.Api.Models;
using System.Text.Json;

namespace SineUyum.Api.Services
{
    public record MatchingResult(int GroupCount, int UngroupedCount);

    public class MatchingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string? _tmdbApiKey;
        private const string TmdbApiBaseUrl = "https://api.themoviedb.org/3/";
        private readonly NotificationService _notifications;
        private readonly UserManager<AppUser> _userManager;
        private readonly PrivacyService _privacy;

        public MatchingService(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, NotificationService notifications, UserManager<AppUser> userManager, PrivacyService privacy)
        {
            _context = context;
            _privacy = privacy;
            _httpClientFactory = httpClientFactory;
            _notifications = notifications;
            _userManager = userManager;
            // An empty string must count as unset too; otherwise the placeholder
            // in appsettings.json shadowed the TMDB_API_KEY environment variable.
            var apiKey = configuration["TMDb:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = configuration["TMDB_API_KEY"];
            }
            _tmdbApiKey = apiKey;
        }

        // Fast pre-check in the request path: the admin sees errors (no event,
        // groups already exist, too few participants) before the job is
        // queued. The job starts with the same check.
        public async Task<CinemaEvent> ValidateAsync(int eventId)
        {
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) throw DomainException.NotFound("errors.eventNotFound");
            if (await _context.EventGroups.AnyAsync(g => g.CinemaEventId == eventId)) throw new DomainException("matching.groupsAlreadyCreated");

            var participantCount = await _context.EventParticipants.CountAsync(p => p.CinemaEventId == eventId);
            if (participantCount < cinemaEvent.GroupSize)
            {
                throw new DomainException("matching.notEnoughParticipants", [cinemaEvent.GroupSize, participantCount]);
            }
            return cinemaEvent;
        }

        // Called from the background job (MatchingWorker). Three stages:
        //  1) groups are built in memory (without writing to the database),
        //  2) suggestions are computed — this may call TMDb, outside the
        //     transaction so no lock is held during the external call,
        //  3) all groups, suggestions and notifications are written in ONE transaction.
        // Each group used to be saved separately: an error midway (e.g. TMDb)
        // left half the groups behind, and a retry was then rejected with
        // "groups already created". Live notifications/pushes go out after the
        // commit; nobody is notified about rolled-back work.
        public async Task<MatchingResult> CreateGroupsForEventAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var cinemaEvent = await ValidateAsync(eventId);

            var participants = await _context.EventParticipants
                .Where(p => p.CinemaEventId == eventId)
                .Include(p => p.User)
                .ThenInclude(u => u.Ratings)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var userRatingsDict = participants.ToDictionary(p => p.UserId, p => p.User.Ratings.ToDictionary(r => r.MovieId, r => r.Rating));
            var userPool = participants.Select(p => p.UserId).ToList();
            // Two people who blocked each other (either way) never land in the same group.
            var blockedPairs = await _privacy.BlockedPairsAmongAsync(userPool);
            var random = new Random();

            var plannedGroups = new List<List<string>>();
            while (userPool.Count >= cinemaEvent.GroupSize)
            {
                var starterUserId = PickStarter(userPool, blockedPairs, random);
                userPool.Remove(starterUserId);

                var compatibilities = userPool.Select(otherUserId => new {
                    UserId = otherUserId,
                    Score = CalculateCompatibility(userRatingsDict[starterUserId], userRatingsDict.ContainsKey(otherUserId) ? userRatingsDict[otherUserId] : new Dictionary<int, int>())
                }).ToList();

                // Starting from the most compatible, take people with no block against anyone in the group.
                var groupMemberIds = new List<string> { starterUserId };
                foreach (var candidate in compatibilities.OrderByDescending(x => x.Score))
                {
                    if (groupMemberIds.Count == cinemaEvent.GroupSize) break;
                    if (groupMemberIds.Any(m => blockedPairs.Contains((m, candidate.UserId)))) continue;
                    groupMemberIds.Add(candidate.UserId);
                }

                // Blocks make a full group with this person impossible; they
                // stay ungrouped (already out of the pool) and we go on with the rest.
                if (groupMemberIds.Count < cinemaEvent.GroupSize) continue;

                foreach (var memberId in groupMemberIds) userPool.Remove(memberId);
                plannedGroups.Add(groupMemberIds);
            }

            var suggestionsByGroup = new List<List<int>>(plannedGroups.Count);
            foreach (var members in plannedGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                suggestionsByGroup.Add(await GetGroupMovieSuggestions(members, userRatingsDict));
            }

            var staged = new List<Notification>();
            await using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
            {
                // Another job may have written the groups since the pre-check.
                if (await _context.EventGroups.AnyAsync(g => g.CinemaEventId == eventId, cancellationToken))
                {
                    throw new DomainException("matching.groupsAlreadyCreated");
                }

                for (var i = 0; i < plannedGroups.Count; i++)
                {
                    var group = new EventGroup { CinemaEventId = eventId };
                    foreach (var memberId in plannedGroups[i])
                    {
                        group.Members.Add(new EventGroupMember { UserId = memberId });
                    }
                    foreach (var (movieId, rank) in suggestionsByGroup[i].Distinct().Select((id, r) => (id, r)))
                    {
                        group.Suggestions.Add(new EventGroupSuggestion { MovieId = movieId, Rank = rank });
                    }
                    _context.EventGroups.Add(group);

                    staged.AddRange(await _notifications.StageAsync(
                        plannedGroups[i],
                        "/my-event",
                        "notifications.groupAssigned", cinemaEvent.LocationName));
                }

                // Deliberately no cancellation token: once writing has started it
                // must not be cut off halfway (the commit either happens or not).
                await _context.SaveChangesAsync(CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
            }

            await _notifications.PublishAsync(staged);

            var groupedCount = plannedGroups.Sum(g => g.Count);
            return new MatchingResult(plannedGroups.Count, participants.Count - groupedCount);
        }

        private async Task<List<int>> GetGroupMovieSuggestions(List<string> groupMemberIds, Dictionary<string, Dictionary<int, int>> allRatings)
        {
            var allSeenMovies = groupMemberIds.SelectMany(id => allRatings.ContainsKey(id) ? allRatings[id].Keys : Enumerable.Empty<int>()).ToHashSet();
            var collectiveFavorites = new Dictionary<int, int>();
            foreach (var memberId in groupMemberIds)
            {
                if (allRatings.ContainsKey(memberId))
                {
                    foreach (var rating in allRatings[memberId].Where(r => r.Value >= 8))
                    {
                        collectiveFavorites[rating.Key] = collectiveFavorites.GetValueOrDefault(rating.Key, 0) + 1;
                    }
                }
            }
            var keyMovies = collectiveFavorites.Where(kv => kv.Value >= groupMemberIds.Count / 2).Select(kv => kv.Key).ToList();
            if (!keyMovies.Any())
            {
                var mostRated = collectiveFavorites.OrderByDescending(kv => kv.Value).FirstOrDefault();
                if(mostRated.Key != 0) keyMovies.Add(mostRated.Key);
            }

            List<int> suggestions = new List<int>();
            if (keyMovies.Any())
            {
                 var similarUsers = await _context.UserRatings
                    .Where(r => keyMovies.Contains(r.MovieId) && r.Rating >= 8 && !groupMemberIds.Contains(r.UserId))
                    .Select(r => r.UserId).Distinct().Take(50).ToListAsync();

                if (similarUsers.Any())
                {
                    suggestions = await _context.UserRatings
                        .Where(r => similarUsers.Contains(r.UserId) && r.Rating >= 9 && !allSeenMovies.Contains(r.MovieId))
                        .GroupBy(r => r.MovieId)
                        .Select(g => new { MovieId = g.Key, Score = g.Count() })
                        .OrderByDescending(x => x.Score).Take(3).Select(x => x.MovieId).ToListAsync();
                }
            }

            if (!suggestions.Any())
            {
                if (string.IsNullOrEmpty(_tmdbApiKey)) throw new InvalidOperationException("The TMDb API key is not configured.");

                var client = _httpClientFactory.CreateClient("TMDb");
                // A background job has no request language: titles are stored in
                // the default language; MovieTranslationWorker adds the others.
                var language = SupportedLanguages.Default;
                var popularMoviesUrl = $"{TmdbApiBaseUrl}movie/popular?api_key={_tmdbApiKey}&language={SupportedLanguages.TmdbLanguage(language)}&page=1";

                var response = await client.GetAsync(popularMoviesUrl);
                if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Could not fetch popular movies from the TMDb API. Status code: {response.StatusCode}");

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var popularMoviesDoc = JsonDocument.Parse(jsonResponse);
                var popularMovies = popularMoviesDoc.RootElement.GetProperty("results").EnumerateArray().ToList();

                var fallbackSuggestions = popularMovies
                    .Where(movie => !allSeenMovies.Contains(movie.GetProperty("id").GetInt32()))
                    .Take(3)
                    .Select(movie => movie.GetProperty("id").GetInt32())
                    .ToList();

                if (!fallbackSuggestions.Any() && popularMovies.Any())
                {
                    fallbackSuggestions = popularMovies.Take(3).Select(movie => movie.GetProperty("id").GetInt32()).ToList();
                }

                var moviesToEnsureInDb = popularMovies.Where(m => fallbackSuggestions.Contains(m.GetProperty("id").GetInt32()));
                var moviesToAdd = new List<Movie>();
                foreach (var movieElement in moviesToEnsureInDb)
                {
                    var movieId = movieElement.GetProperty("id").GetInt32();
                    if (!await _context.Movies.AnyAsync(m => m.Id == movieId))
                    {
                        var title = movieElement.GetProperty("title").GetString() ?? MovieCatalogService.UntitledTitle(language);
                        var movie = new Movie
                        {
                            Id = movieId,
                            Title = title,
                            PosterPath = movieElement.GetProperty("poster_path").GetString()
                        };
                        movie.Translations.Add(new MovieTranslation { Language = language, Title = title });
                        moviesToAdd.Add(movie);
                    }
                }

                if(moviesToAdd.Any())
                {
                     _context.Movies.AddRange(moviesToAdd);
                     await _context.SaveChangesAsync();
                }

                return fallbackSuggestions;
            }

            return suggestions;
        }

        // Without blocks, random as before. With blocks, the person with the
        // most conflicts is placed first (most constrained first): otherwise
        // people who blocked each other were left for last in the pool and ended
        // up ungrouped even though a valid split existed.
        private static string PickStarter(List<string> pool, HashSet<(string, string)> blockedPairs, Random random)
        {
            if (blockedPairs.Count == 0) return pool[random.Next(pool.Count)];

            var conflicts = pool
                .Select(u => (UserId: u, Count: pool.Count(o => blockedPairs.Contains((u, o)))))
                .ToList();
            var max = conflicts.Max(x => x.Count);
            var mostConstrained = conflicts.Where(x => x.Count == max).Select(x => x.UserId).ToList();
            return mostConstrained[random.Next(mostConstrained.Count)];
        }

        private static double CalculateCompatibility(Dictionary<int, int> ratings1, Dictionary<int, int> ratings2)
            => CompatibilityCalculator.Score(ratings1, ratings2);
    }
}
