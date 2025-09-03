// Controllers/CompatibilityController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Uyum oranını görmek için giriş yapmış olmak zorunlu.
public class CompatibilityController(ApplicationDbContext context) : ControllerBase
{
    // API endpoint'imiz: GET /api/compatibility/{targetUserId}
    [HttpGet("{targetUserId}")]
    public async Task<IActionResult> GetCompatibility(string targetUserId)
    {
        // 1. ADIM: KULLANICI ID'LERİNİ ALMA
        // ===================================
        // Giriş yapmış olan (token'ı gönderen) kullanıcının ID'sini token'ın içinden okuyoruz.
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Bir güvenlik kontrolü. Normalde [Authorize] sayesinde bu her zaman dolu gelir.
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        // Bir kullanıcının kendisiyle uyum oranını hesaplaması anlamsız.
        if (currentUserId == targetUserId)
        {
            return BadRequest("Kullanıcı kendisiyle karşılaştırılamaz.");
        }


        // 2. ADIM: KULLANICILARIN PUANLARINI VERİTABANINDAN ÇEKME
        // =========================================================
        var currentUserRatings = await context.UserRatings
            .Where(r => r.UserId == currentUserId)
            .ToListAsync();

        var targetUserRatings = await context.UserRatings
            .Where(r => r.UserId == targetUserId)
            .ToListAsync();


        // 3. ADIM: ORTAK FİLMLERİ BULMA VE HESAPLAMA
        // ============================================
        // LINQ sorgusu ile iki kullanıcının da puanladığı ortak filmleri ve puanlarını buluyoruz.
        var commonRatings = from r1 in currentUserRatings
                            join r2 in targetUserRatings on r1.MovieId equals r2.MovieId
                            select new
                            {
                                CurrentUserRating = r1.Rating,
                                TargetUserRating = r2.Rating
                            };

        // Eğer hiç ortak film puanlamamışlarsa, uyumları %0'dır.
        if (!commonRatings.Any())
        {
            return Ok(new { compatibilityScore = 0.0, commonMovieCount = 0 });
        }

        // Puanlar arasındaki farkların toplamını hesaplıyoruz.
        double totalDifference = commonRatings.Sum(r => Math.Abs(r.CurrentUserRating - r.TargetUserRating));
        
        // Bu toplam farkın ortalamasını alıyoruz.
        double averageDifference = totalDifference / commonRatings.Count();

        // Ortalama farkı %100'lük bir skora çeviriyoruz.
        // Puanlar 1-10 arası olduğu için iki puan arasındaki maksimum fark 9'dur.
        // Ortalama fark 0 ise uyum %100, 9 ise uyum %0 olur.
        double compatibilityScore = 100.0 - (averageDifference / 9.0 * 100.0);


        // 4. ADIM: SONUCU DÖNDÜRME
        // =========================
        var result = new
        {
            compatibilityScore = Math.Round(compatibilityScore, 2), // Sonucu iki ondalık basamağa yuvarla
            commonMovieCount = commonRatings.Count()
        };

        return Ok(result);
    }
}