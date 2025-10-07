// SineUyum.Api/Services/MatchingService.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Hubs;
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
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserManager<AppUser> _userManager;

        public MatchingService(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, IHubContext<NotificationHub> hubContext, UserManager<AppUser> userManager)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext;
            _userManager = userManager;
            var apiKey = configuration["TMDb:ApiKey"] ?? configuration["TMDB_API_KEY"];
            _tmdbApiKey = apiKey;
        }

        public async Task CreateGroupsForEventAsync(int eventId)
        {
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) throw new Exception("Etkinlik bulunamadı.");
            if (await _context.EventGroups.AnyAsync(g => g.CinemaEventId == eventId)) throw new Exception("Bu etkinlik için gruplar zaten oluşturulmuş.");

            var participants = await _context.EventParticipants
                .Where(p => p.CinemaEventId == eventId && !_context.EventGroupMembers.Any(gm => gm.UserId == p.UserId && gm.EventGroup.CinemaEventId == eventId))
                .Include(p => p.User)
                .ThenInclude(u => u.Ratings)
                .ToListAsync();

            if (participants.Count < cinemaEvent.GroupSize)
            {
                throw new Exception($"Yeterli katılımcı olmadığı için gruplar oluşturulamadı. Gerekli: {cinemaEvent.GroupSize}, Mevcut: {participants.Count}");
            }
            
            var userRatingsDict = participants.ToDictionary(p => p.UserId, p => p.User.Ratings.ToDictionary(r => r.MovieId, r => r.Rating));
            var userPool = participants.Select(p => p.UserId).ToList();
            var random = new Random();

            while (userPool.Count >= cinemaEvent.GroupSize)
            {
                var starterUserId = userPool[random.Next(userPool.Count)];
                userPool.Remove(starterUserId);

                var compatibilities = userPool.Select(otherUserId => new {
                    UserId = otherUserId,
                    Score = CalculateCompatibility(userRatingsDict[starterUserId], userRatingsDict.ContainsKey(otherUserId) ? userRatingsDict[otherUserId] : new Dictionary<int, int>())
                }).ToList();

                var groupMemberIds = compatibilities.OrderByDescending(x => x.Score)
                                                    .Take(cinemaEvent.GroupSize - 1)
                                                    .Select(x => x.UserId)
                                                    .ToList();
                groupMemberIds.Add(starterUserId);

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

                // BİLDİRİM GÖNDERME KISMI
                var notificationMessage = $"'{cinemaEvent.LocationName}' etkinliği için bir gruba atandın!";
                foreach (var memberId in groupMemberIds)
                {
                    var notification = new Notification
                    {
                        UserId = memberId,
                        Message = notificationMessage,
                        RelatedUrl = "/my-event"
                    };
                    await _context.Notifications.AddAsync(notification);
                    await _hubContext.Clients.User(memberId).SendAsync("ReceiveNotification", notification);
                }
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

        private double CalculateCompatibility(Dictionary<int, int> ratings1, Dictionary<int, int> ratings2)
        {
            var commonMovieIds = ratings1.Keys.Intersect(ratings2.Keys).ToList();
            if (!commonMovieIds.Any()) return 0;
            double totalWeightedScore = 0;
            foreach (var movieId in commonMovieIds)
            {
                int rating1 = ratings1[movieId];
                int rating2 = ratings2[movieId];
                double difference = Math.Abs(rating1 - rating2);
                double weight = 1.0;
                if ((rating1 <= 3 && rating2 <= 3) || (rating1 >= 8 && rating2 >= 8)) weight = 1.5;
                else if ((rating1 <= 3 && rating2 >= 8) || (rating1 >= 8 && rating2 <= 3)) weight = 2.0;
                totalWeightedScore += (10 - difference) * weight;
            }
            double maxPossibleScore = commonMovieIds.Count * 10 * 1.5;
            return maxPossibleScore > 0 ? (totalWeightedScore / maxPossibleScore) * 100 : 0;
        }
    }
}