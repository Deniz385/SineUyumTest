using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SineUyum.Api.Data;

namespace SineUyum.Api.Tests;

// API'yi bellekte ayağa kaldırır; gerçek SQLite dosyası yerine açık tutulan
// bir in-memory bağlantı kullanır, böylece her test sınıfı izole ve hızlı çalışır.
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    // :memory: veritabanı yalnızca bağlantı açık kaldığı sürece yaşar; bu yüzden
    // bağlantıyı fabrika ömrü boyunca elimizde tutuyoruz.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

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
        // Testler tek IP'den çok sayıda auth isteği atıyor; limit açılsın.
        Environment.SetEnvironmentVariable("RateLimit__AuthPermitLimit", "100000");

        // İsteğe bağlı: tüm paketi gerçek PostgreSQL'e karşı (migration'larla)
        // koşmak için TEST_POSTGRES_CONNECTION verin, ör.
        //   TEST_POSTGRES_CONNECTION="Host=localhost;Username=postgres;Password=..." dotnet test
        // Her fabrika kendi veritabanını oluşturur. Verilmezse in-memory SQLite.
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

            _connection.Open();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
