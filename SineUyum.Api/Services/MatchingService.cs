// SineUyum.Api/Services/MatchingService.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Models;
using System.Text.Json;

namespace SineUyum.Api.Services
{
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
            // Boş string de tanımsız sayılmalı; appsettings.json'daki yer tutucu
            // aksi halde TMDB_API_KEY ortam değişkenini gölgeliyordu.
            var apiKey = configuration["TMDb:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = configuration["TMDB_API_KEY"];
            }
            _tmdbApiKey = apiKey;
        }

        public async Task CreateGroupsForEventAsync(int eventId)
        {
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) throw DomainException.NotFound("Etkinlik bulunamadı.");
            if (await _context.EventGroups.AnyAsync(g => g.CinemaEventId == eventId)) throw new DomainException("Bu etkinlik için gruplar zaten oluşturulmuş.");

            var participants = await _context.EventParticipants
                .Where(p => p.CinemaEventId == eventId && !_context.EventGroupMembers.Any(gm => gm.UserId == p.UserId && gm.EventGroup.CinemaEventId == eventId))
                .Include(p => p.User)
                .ThenInclude(u => u.Ratings)
                .ToListAsync();

            if (participants.Count < cinemaEvent.GroupSize)
            {
                throw new DomainException($"Yeterli katılımcı olmadığı için gruplar oluşturulamadı. Gerekli: {cinemaEvent.GroupSize}, Mevcut: {participants.Count}");
            }
            
            var userRatingsDict = participants.ToDictionary(p => p.UserId, p => p.User.Ratings.ToDictionary(r => r.MovieId, r => r.Rating));
            var userPool = participants.Select(p => p.UserId).ToList();
            // Birbirini engelleyen (herhangi yönde) iki kişi aynı gruba düşmez.
            var blockedPairs = await _privacy.BlockedPairsAmongAsync(userPool);
            var random = new Random();

            while (userPool.Count >= cinemaEvent.GroupSize)
            {
                var starterUserId = PickStarter(userPool, blockedPairs, random);
                userPool.Remove(starterUserId);

                var compatibilities = userPool.Select(otherUserId => new {
                    UserId = otherUserId,
                    Score = CalculateCompatibility(userRatingsDict[starterUserId], userRatingsDict.ContainsKey(otherUserId) ? userRatingsDict[otherUserId] : new Dictionary<int, int>())
                }).ToList();

                // En uyumludan başlayarak, gruptaki hiç kimseyle engeli olmayanlar alınır.
                var groupMemberIds = new List<string> { starterUserId };
                foreach (var candidate in compatibilities.OrderByDescending(x => x.Score))
                {
                    if (groupMemberIds.Count == cinemaEvent.GroupSize) break;
                    if (groupMemberIds.Any(m => blockedPairs.Contains((m, candidate.UserId)))) continue;
                    groupMemberIds.Add(candidate.UserId);
                }

                // Engeller yüzünden bu kişiyle tam grup kurulamıyor; o gruba
                // alınmadan kalır (havuzdan çıktı), diğerleriyle devam edilir.
                if (groupMemberIds.Count < cinemaEvent.GroupSize) continue;

                var newGroup = new EventGroup { CinemaEventId = eventId };
                
                var suggestedMovieIds = await GetGroupMovieSuggestions(groupMemberIds, userRatingsDict);
                newGroup.SuggestedMovieIds = string.Join(",", suggestedMovieIds);
                
                _context.EventGroups.Add(newGroup);
                
                foreach (var memberId in groupMemberIds)
                {
                    newGroup.Members.Add(new EventGroupMember { UserId = memberId });
                    userPool.Remove(memberId);
                }
                
                // SaveChangesAsync'i burada çağırarak grubun ve üyelerin ID'lerinin oluşmasını sağlıyoruz.
                await _context.SaveChangesAsync();

                // Grup üyelerine bildirim: kaydedilir, sonra SignalR + push.
                await _notifications.NotifyManyAsync(
                    groupMemberIds,
                    $"'{cinemaEvent.LocationName}' etkinliği için bir gruba atandın!",
                    "/my-event");
            }

            await _context.SaveChangesAsync();
        }
        
        // ... (diğer metodlar aynı kalacak) ...
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
                if (string.IsNullOrEmpty(_tmdbApiKey)) throw new InvalidOperationException("TMDb API anahtarı yapılandırılmamış.");

                var client = _httpClientFactory.CreateClient("TMDb");
                var popularMoviesUrl = $"{TmdbApiBaseUrl}movie/popular?api_key={_tmdbApiKey}&language=tr-TR&page=1";
                
                var response = await client.GetAsync(popularMoviesUrl);
                if (!response.IsSuccessStatusCode) throw new HttpRequestException($"TMDb API'sinden popüler filmler alınamadı. Durum Kodu: {response.StatusCode}");

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
                        moviesToAdd.Add(new Movie
                        {
                            Id = movieId,
                            Title = movieElement.GetProperty("title").GetString() ?? "Başlıksız",
                            PosterPath = movieElement.GetProperty("poster_path").GetString()
                        });
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

        // Engel yoksa eskisi gibi rastgele. Varsa en çok çakışması olan önce
        // yerleştirilir (en kısıtlı önce): aksi halde birbirini engelleyenler
        // havuzda sona kalıp, geçerli bir dağılım varken gruptan dışarıda kalıyordu.
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