// SineUyum.Api/Services/MatchingService.cs
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

namespace SineUyum.Api.Services
{
    public class MatchingService
    {
        private readonly ApplicationDbContext _context;

        public MatchingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateGroupsForEventAsync(int eventId)
        {
            var cinemaEvent = await _context.CinemaEvents.FindAsync(eventId);
            if (cinemaEvent == null) throw new Exception("Etkinlik bulunamadı.");
            if (await _context.EventGroups.AnyAsync(g => g.CinemaEventId == eventId)) throw new Exception("Bu etkinlik için gruplar zaten oluşturulmuş.");

            // --- DEĞİŞİKLİK BURADA: Artık tüm aboneler yerine, etkinliğe katılanları alıyoruz ---
            var participants = await _context.EventParticipants
                .Where(p => p.CinemaEventId == eventId && !_context.EventGroupMembers.Any(gm => gm.UserId == p.UserId && gm.EventGroup.CinemaEventId == eventId))
                .Include(p => p.User)
                .ThenInclude(u => u.Ratings)
                .ToListAsync();

            if (participants.Count < cinemaEvent.GroupSize)
            {
                throw new Exception("Yeterli katılımcı olmadığı için gruplar oluşturulamadı.");
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
                    Score = CalculateCompatibility(userRatingsDict[starterUserId], userRatingsDict[otherUserId])
                }).ToList();

                var groupMemberIds = compatibilities.OrderByDescending(x => x.Score)
                                                    .Take(cinemaEvent.GroupSize - 1)
                                                    .Select(x => x.UserId)
                                                    .ToList();
                groupMemberIds.Add(starterUserId);

                var newGroup = new EventGroup { CinemaEventId = eventId };
                
                var suggestedMovieIds = GetGroupMovieSuggestions(groupMemberIds, userRatingsDict);
                newGroup.SuggestedMovieIds = string.Join(",", suggestedMovieIds);
                
                _context.EventGroups.Add(newGroup);
                
                foreach (var memberId in groupMemberIds)
                {
                    newGroup.Members.Add(new EventGroupMember { UserId = memberId });
                    userPool.Remove(memberId);
                }
            }

            await _context.SaveChangesAsync();
        }

        private List<int> GetGroupMovieSuggestions(List<string> groupMemberIds, Dictionary<string, Dictionary<int, int>> allRatings)
        {
            var allSeenMovies = groupMemberIds.SelectMany(id => allRatings[id].Keys).ToHashSet();
            var collectiveFavorites = new Dictionary<int, int>();
            foreach (var memberId in groupMemberIds)
            {
                foreach (var rating in allRatings[memberId].Where(r => r.Value >= 8))
                {
                    collectiveFavorites[rating.Key] = collectiveFavorites.GetValueOrDefault(rating.Key, 0) + 1;
                }
            }
            var keyMovies = collectiveFavorites.Where(kv => kv.Value >= groupMemberIds.Count / 2).Select(kv => kv.Key).ToList();
            if (!keyMovies.Any())
            {
                var mostRated = collectiveFavorites.OrderByDescending(kv => kv.Value).FirstOrDefault();
                if(mostRated.Key != 0) keyMovies.Add(mostRated.Key);
            }
            if (!keyMovies.Any()) return new List<int>();
            var similarUsers = _context.UserRatings
                .Where(r => keyMovies.Contains(r.MovieId) && r.Rating >= 8 && !groupMemberIds.Contains(r.UserId))
                .Select(r => r.UserId).Distinct().Take(50).ToList();
            if (!similarUsers.Any()) return new List<int>();
            var suggestions = _context.UserRatings
                .Where(r => similarUsers.Contains(r.UserId) && r.Rating >= 9 && !allSeenMovies.Contains(r.MovieId))
                .GroupBy(r => r.MovieId)
                .Select(g => new { MovieId = g.Key, Score = g.Count() })
                .OrderByDescending(x => x.Score).Take(3).Select(x => x.MovieId).ToList();
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