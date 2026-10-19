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

        builder.ConfigureServices(services =>
        {
            // Gerçek SQLite dosyası kaydını kaldırıp in-memory bağlantıyı koy.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

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
