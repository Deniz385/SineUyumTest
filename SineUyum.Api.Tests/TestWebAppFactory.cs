using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SineUyum.Api.Data;

namespace SineUyum.Api.Tests;

// API'yi bellekte ayağa kaldırır; her fabrika kendi geçici SQLite dosyasını
// kullanır, böylece her test sınıfı izole çalışır.
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    // Tek bir paylaşılan :memory: bağlantısı yerine dosya: arka plan servisleri
    // (ör. PushDispatchService) istekle eşzamanlı kendi DbContext'ini açtığında
    // her context kendi bağlantısını alır ve SQLite'ın normal kilit/bekleme
    // davranışı geçerli olur — üretimdeki gibi.
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "sineuyum-test-" + Guid.NewGuid().ToString("N") + ".db");

    // Her fabrika kendi medya klasörünü kullanır (yüklenen fotoğraflar).
    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "sineuyum-media-" + Guid.NewGuid().ToString("N"));

    public const string AdminUserName = "testadmin";
    public const string AdminPassword = "Admin123!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Program.cs üst seviye kodu JWT_KEY'i builder aşamasında okuyor; bu,
        // fabrikanın ConfigureAppConfiguration geri çağrısından önce çalışıyor.
        // Bu yüzden değerleri ortam değişkeni olarak veriyoruz — WebApplicationBuilder
        // bunları varsayılan olarak ve erkenden okur. (Hiyerarşi için "__".)
        Environment.SetEnvironmentVariable("JWT_KEY",
            "test-ortami-icin-sabit-bir-anahtar-en-az-64-byte-olmali-xxxxxxxxxxxx");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "60");
        Environment.SetEnvironmentVariable("ADMIN_USERNAME", AdminUserName);
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", AdminPassword);
        Environment.SetEnvironmentVariable("ADMIN_EMAIL", "testadmin@sineuyum.local");
        // Testler tek IP'den/kullanıcıdan çok sayıda istek atıyor; sınırlar
        // açılsın (sınırların kendisi RateLimitTests'te düşük değerlerle).
        foreach (var limit in new[] { "Global", "Auth", "Upload", "Tmdb", "SocialWrite" })
        {
            Environment.SetEnvironmentVariable($"RateLimit__{limit}PermitLimit", "100000");
        }
        // Arka plan tür tamamlaması testlerde kendiliğinden çalışmasın (TMDb
        // sahtelerindeki çağrı sayılarını bozar); gereken test doğrudan çağırır.
        Environment.SetEnvironmentVariable("GenreBackfill__Enabled", "false");
        Environment.SetEnvironmentVariable("TasteTwin__Enabled", "false");
        builder.UseSetting("Storage:MediaRoot", MediaRoot);

        // İsteğe bağlı: tüm paketi gerçek PostgreSQL'e karşı (migration'larla)
        // koşmak için TEST_POSTGRES_CONNECTION verin, ör.
        //   TEST_POSTGRES_CONNECTION="Host=localhost;Username=postgres;Password=..." dotnet test
        // Her fabrika kendi veritabanını oluşturur. Verilmezse geçici SQLite dosyası.
        var postgres = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(postgres))
        {
            Environment.SetEnvironmentVariable("Database__Provider", "Postgres");
        }

        builder.ConfigureServices(services =>
        {
            // Gerçek veritabanı kaydını kaldırıp test veritabanını koy.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

            if (!string.IsNullOrWhiteSpace(postgres))
            {
                var database = "sineuyum_test_" + Guid.NewGuid().ToString("N")[..12];
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql($"{postgres};Database={database}"));
                return;
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
            {
                try { File.Delete(_dbPath + suffix); } catch (IOException) { }
            }
            try { Directory.Delete(MediaRoot, recursive: true); } catch (IOException) { }
        }
    }
}
