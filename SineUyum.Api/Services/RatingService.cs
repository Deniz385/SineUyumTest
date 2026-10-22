using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Models;

namespace SineUyum.Api.Services
{
    // The single place a user's rating is written (the ratings endpoint and the
    // film diary both go through here), so the rules stay the same everywhere.
    public class RatingService
    {
        private readonly ApplicationDbContext _context;
        private readonly MovieCatalogService _movieCatalog;

        public RatingService(ApplicationDbContext context, MovieCatalogService movieCatalog)
        {
            _context = context;
            _movieCatalog = movieCatalog;
        }

        // Creates or changes the user's rating of the movie. A movie missing
        // locally is fetched from TMDb and added (metadata comes from the
        // server, not the client).
        public async Task UpsertAsync(string userId, int movieId, int rating)
        {
            await _movieCatalog.EnsureMovieAsync(movieId);

            var existingRating = await _context.UserRatings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == movieId);

            if (existingRating != null)
            {
                // Changing a rating does not move it up in the feed.
                existingRating.Rating = rating;
                await _context.SaveChangesAsync();
                return;
            }

            var now = DateTime.UtcNow;
            _context.UserRatings.Add(new UserRating
            {
                MovieId = movieId,
                UserId = userId,
                Rating = rating,
                CreatedAt = now,
                ActivityAt = now
            });
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent second request created the same rating (unique index): update it.
                _context.ChangeTracker.Clear();
                var raced = await _context.UserRatings
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == movieId);
                if (raced == null) throw;
                raced.Rating = rating;
                await _context.SaveChangesAsync();
            }
        }
    }
}
