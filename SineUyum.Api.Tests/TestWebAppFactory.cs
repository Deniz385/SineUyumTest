using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SineUyum.Api.Data;

namespace SineUyum.Api.Tests;

// Runs the API in memory; every factory uses its own temporary SQLite file,
// so every test class runs isolated.
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    // A file instead of a single shared :memory: connection: when background
    // services (e.g. PushDispatchService) open their own DbContext concurrently
    // with a request, every context gets its own connection and SQLite's normal
    // locking/waiting applies — as in production.
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "sineuyum-test-" + Guid.NewGuid().ToString("N") + ".db");

    // Every factory uses its own media folder (uploaded photos).
    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "sineuyum-media-" + Guid.NewGuid().ToString("N"));

    public const string AdminUserName = "testadmin";
    public const string AdminPassword = "Admin123!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Program.cs top-level code reads JWT_KEY while building, which runs
        // before the factory's ConfigureAppConfiguration callback. So the values
        // are given as environment variables — WebApplicationBuilder reads those
        // by default and early. ("__" for hierarchy.)
        Environment.SetEnvironmentVariable("JWT_KEY",
            "a-fixed-key-for-the-test-environment-at-least-64-bytes-long-xxxxxxxx");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "60");
        Environment.SetEnvironmentVariable("ADMIN_USERNAME", AdminUserName);
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", AdminPassword);
        Environment.SetEnvironmentVariable("ADMIN_EMAIL", "testadmin@sineuyum.local");
        // Tests send many requests from a single IP/user; lift the limits (the
        // limits themselves are tested with low values in RateLimitTests).
        foreach (var limit in new[] { "Global", "Auth", "Upload", "Tmdb", "SocialWrite" })
        {
            Environment.SetEnvironmentVariable($"RateLimit__{limit}PermitLimit", "100000");
        }
        // The background genre backfill must not run on its own in tests (it
        // would disturb the call counts of the TMDb fakes); tests that need it call it directly.
        Environment.SetEnvironmentVariable("GenreBackfill__Enabled", "false");
        Environment.SetEnvironmentVariable("TasteTwin__Enabled", "false");
        Environment.SetEnvironmentVariable("MovieTranslations__Enabled", "false");
        builder.UseSetting("Storage:MediaRoot", MediaRoot);

        // Optional: to run the whole suite against a real PostgreSQL (with the
        // migrations), set TEST_POSTGRES_CONNECTION, e.g.
        //   TEST_POSTGRES_CONNECTION="Host=localhost;Username=postgres;Password=..." dotnet test
        // Every factory creates its own database. Without it, a temporary SQLite file.
        var postgres = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(postgres))
        {
            Environment.SetEnvironmentVariable("Database__Provider", "Postgres");
        }

        builder.ConfigureServices(services =>
        {
            // Replace the real database registration with the test database.
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
