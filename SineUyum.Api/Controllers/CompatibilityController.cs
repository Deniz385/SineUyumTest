// SineUyum.Api/Controllers/CompatibilityController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompatibilityController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet("{targetUserId}")]
    public async Task<IActionResult> GetCompatibility(string targetUserId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (currentUserId == null) return Unauthorized();
        if (currentUserId == targetUserId) return BadRequest("Kullanıcı kendisiyle karşılaştırılamaz.");

        var currentUserRatings = await context.UserRatings
            .Where(r => r.UserId == currentUserId)
            .ToListAsync();

        var targetUserRatings = await context.UserRatings
            .Where(r => r.UserId == targetUserId)
            .ToListAsync();
            
        // --- DEĞİŞİKLİK BURADA BAŞLIYOR ---

        // LINQ sorgusu ile ortak filmleri, puanları ve film detaylarını alıyoruz
        var commonRatings = from r1 in currentUserRatings
                            join r2 in targetUserRatings on r1.MovieId equals r2.MovieId
                            join movie in context.Movies on r1.MovieId equals movie.Id
                            select new
                            {
                                MovieId = r1.MovieId,
                                Title = movie.Title,
                                PosterPath = movie.PosterPath,
                                CurrentUserRating = r1.Rating,
                                TargetUserRating = r2.Rating
                            };

        if (!commonRatings.Any())
        {
            return Ok(new { compatibilityScore = 0.0, commonMovieCount = 0, commonMovies = new List<object>() });
        }

        double totalDifference = commonRatings.Sum(r => Math.Abs(r.CurrentUserRating - r.TargetUserRating));
        double averageDifference = totalDifference / commonRatings.Count();
        double compatibilityScore = 100.0 - (averageDifference / 9.0 * 100.0);

        var result = new
        {
            compatibilityScore = Math.Round(compatibilityScore, 2),
            commonMovieCount = commonRatings.Count(),
            commonMovies = commonRatings.ToList() // Ortak film listesini de sonuca ekliyoruz
        };

        return Ok(result);
    }
}