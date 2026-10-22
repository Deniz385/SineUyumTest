using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SineUyum.Api.Data;
using SineUyum.Api.Hubs;
using SineUyum.Api.Localization;
using SineUyum.Api.Middleware;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using SineUyum.Api.Services.Media;
using SineUyum.Api.Services.Push;
using System.Text;
using SineUyum.Api.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// The "Server: Kestrel" header disclosed the server software (G9).
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// --- LOGGING (Serilog) ---
// Configured from the appsettings "Serilog" section, with a sensible default
// otherwise. Request logs come from UseSerilogRequestLogging below.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    // ExceptionHandlerMiddleware logs EVERY exception as "An unhandled
    // exception" (Error + stack trace) before calling the handler, so expected
    // business errors (404, 413, 415...) looked like real failures.
    // GlobalExceptionHandler already logs unexpected ones as Error with the
    // exception and expected ones as Information, so this copy is silenced.
    .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", Serilog.Events.LogEventLevel.Fatal)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- CORS POLICY ---
// The origin list is not hard-coded but read from configuration: the
// appsettings "Cors:AllowedOrigins" array or the CORS_ALLOWED_ORIGINS
// environment variable (comma-separated). The mobile client is not subject
// to CORS; this policy is only for the web client.
var corsPolicyName = "AllowWebClients";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? builder.Configuration["CORS_ALLOWED_ORIGINS"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // required by SignalR
        }
    });
});

// --- DATABASE AND IDENTITY ---
// The provider comes from configuration: "Database:Provider" = Sqlite (default,
// development) or Postgres (production). Details: docs → M2.
var dbProvider = (builder.Configuration["Database:Provider"] ?? "Sqlite").Trim();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    // Prevent several accounts with the same email.
    options.User.RequireUniqueEmail = true;

    // RegisterDto said "at least 6 characters" but Identity's default also
    // required a special character; the rule now matches the message shown.
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    // Password rules and duplicate user/email errors in the request language.
    .AddErrorDescriber<LocalizedIdentityErrorDescriber>();

// --- JWT AUTHENTICATION ---
var jwtKey = builder.Configuration["JWT_KEY"] ?? builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException(
        "The JWT key is not configured. Set the 'JWT_KEY' environment variable or the 'Jwt:Key' setting.");
}

// HMAC-SHA512 is used, so the key must be at least 64 bytes.
if (Encoding.UTF8.GetByteCount(jwtKey) < 64)
{
    throw new InvalidOperationException(
        "The JWT key is too short for HS512. Use a key of at least 64 bytes (64 ASCII characters).");
}

// If Issuer/Audience are not configured (local development) their validation
// is off; if they are, it is enforced. The API keeps working across environments.
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer),
        ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
     // Lets SignalR pass authentication on to the hub:
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// --- AUTHORIZATION ---
builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.AddScoped<MatchingService>();
// Matching runs in the background (M7): the queue is in the database, one worker.
builder.Services.Configure<MatchingOptions>(builder.Configuration.GetSection("Matching"));
builder.Services.AddSingleton<MatchingQueue>();
builder.Services.AddHostedService<MatchingWorker>();
// Fills in missing genres of older movies from TMDb (idle without a TMDb key).
builder.Services.Configure<GenreBackfillOptions>(builder.Configuration.GetSection("GenreBackfill"));
builder.Services.AddSingleton<GenreBackfillWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GenreBackfillWorker>());
builder.Services.Configure<MovieTranslationOptions>(builder.Configuration.GetSection("MovieTranslations"));
builder.Services.AddSingleton<MovieTranslationWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MovieTranslationWorker>());
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<MovieCatalogService>();
builder.Services.AddScoped<RatingService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<PrivacyService>();
builder.Services.AddScoped<SocialService>();
builder.Services.AddScoped<TasteTwinService>();
builder.Services.AddScoped<YearInReviewService>();
// Weekly taste twin notifications.
builder.Services.Configure<TasteTwinOptions>(builder.Configuration.GetSection("TasteTwin"));
builder.Services.AddSingleton<TasteTwinWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TasteTwinWorker>());
// Payment providers (IPaymentProvider) are registered here. There is no real
// provider yet: subscriptions can only be granted by an admin and
// /api/v1/subscription/verify returns 503. See roadmap item G4.

// --- MEDIA (profile photos) ---
// Local disk; a persistent volume in Docker (/data/media). Override with Storage__MediaRoot.
var mediaRoot = builder.Configuration["Storage:MediaRoot"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "media");
var mediaStorage = new LocalMediaStorage(mediaRoot);
builder.Services.AddSingleton<IMediaStorage>(mediaStorage);

// --- PUSH NOTIFICATIONS (FCM HTTP v1) ---
// Enabled when a Firebase service account key is given; otherwise push is
// silently off and notifications keep working over REST + SignalR.
//   FCM_SERVICE_ACCOUNT_JSON : the key's JSON content (as a secret), or
//   FCM_SERVICE_ACCOUNT_FILE : path to the key file.
//   FCM_PROJECT_ID           : (optional) defaults to the key's project_id.
var fcmJson = builder.Configuration["FCM_SERVICE_ACCOUNT_JSON"];
var fcmFile = builder.Configuration["FCM_SERVICE_ACCOUNT_FILE"];
if (string.IsNullOrWhiteSpace(fcmJson) && !string.IsNullOrWhiteSpace(fcmFile))
{
    fcmJson = File.ReadAllText(fcmFile);
}

builder.Services.AddSingleton<PushQueue>();
builder.Services.AddHostedService<PushDispatchService>();
if (!string.IsNullOrWhiteSpace(fcmJson))
{
    string projectId;
    try
    {
        using var keyDoc = System.Text.Json.JsonDocument.Parse(fcmJson);
        projectId = builder.Configuration["FCM_PROJECT_ID"]
            ?? keyDoc.RootElement.GetProperty("project_id").GetString()
            ?? throw new InvalidOperationException("project_id is empty.");
    }
    catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
    {
        // A wrong configuration must not silently become "push off"; fail at startup.
        throw new InvalidOperationException(
            "The FCM service account key could not be read (valid JSON with a project_id is required).", ex);
    }

    builder.Services.AddSingleton(new FcmOptions { ProjectId = projectId });
    builder.Services.AddSingleton<IFcmAccessTokenProvider>(
        new GoogleFcmAccessTokenProvider(fcmJson));
    builder.Services.AddHttpClient(FcmPushSender.HttpClientName);
    builder.Services.AddSingleton<IPushSender, FcmPushSender>();
}
else
{
    builder.Services.AddSingleton<IPushSender, NullPushSender>();
}

// --- RATE LIMITING ---
// A global limit on every request (per user or IP) plus strict policies for
// expensive or abusable endpoints. Details: RateLimitingSetup.
builder.Services.AddSineUyumRateLimiting(builder.Configuration);
// The real client IP behind a reverse proxy (only from TRUSTED_PROXIES).
builder.Services.AddTrustedProxies();

// --- LOCALIZATION ---
// Request culture from the "lang" query string or Accept-Language (default
// en); strings come from Resources/Locales/*.json. Details: LocalizationSetup.
builder.Services.AddSineUyumLocalization(builder.Configuration);

// --- GLOBAL ERROR HANDLING ---
// Uncaught exceptions are turned into ProblemDetails in one place; stack
// traces never reach the client, details only go to the log.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- CACHE ---
// Keeps TMDb responses (popular, now playing, search, movie details) in a
// TTL cache. Saves quota and speeds up responses. SizeLimit keeps distinct
// search queries from growing memory without bound.
builder.Services.AddMemoryCache(options => options.SizeLimit = 2048);

// --- CONTROLLERS & SWAGGER ---
// All API routes live under "api/v1/..." (see ApiV1RouteConvention).
// DataAnnotations error messages are locale keys (see JsonStringLocalizerFactory).
// Framework messages (implicit [Required], binding errors, unreadable JSON)
// and field names are localized as well (ValidationLocalization); JSON parser
// messages are not passed to the client.
builder.Services.AddControllers(options =>
    {
        options.Conventions.Add(new SineUyum.Api.Conventions.ApiV1RouteConvention());
        ValidationLocalization.Configure(options);
    })
    .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false)
    .AddDataAnnotationsLocalization();
builder.Services.AddSingleton<Microsoft.AspNetCore.Mvc.DataAnnotations.IValidationAttributeAdapterProvider, LocalizedAttributeAdapterProvider>();
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(ValidationLocalization.Configure);
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// First: logging, HTTPS redirection and per-IP limits must see the real client.
app.UseForwardedHeaders();

// Security headers on every response (errors and 429 included), hence outermost.
app.UseSecurityHeaders();

// One summary log line per request (method, path, status code, duration). It
// must be outside the exception handler so it logs the status code actually
// returned AFTER the error was mapped to 404/400/500 (otherwise every error
// would show up as "500").
app.UseSerilogRequestLogging();

// Before the exception handler, so error titles use the request culture too.
app.UseRequestLocalization();

// The exception handler must come early to catch exceptions from every later
// layer; right inside request logging and localization.
app.UseExceptionHandler();

// --- SWAGGER ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// --- MEDIA FILES ---
// Only the .jpg files the server produces are served; unknown extensions
// (including a file that ended up in the folder by mistake) return 404. File
// names are unique, so they can be cached for a long time.
var mediaContentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
mediaContentTypes.Mappings.Clear();
mediaContentTypes.Mappings[".jpg"] = "image/jpeg";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(mediaStorage.Root),
    RequestPath = mediaStorage.PublicPrefix,
    ContentTypeProvider = mediaContentTypes,
    ServeUnknownFileTypes = false,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
    },
});

// --- BACKWARD COMPATIBILITY (UNVERSIONED /api/*) ---
// Old clients (deployed web/mobile) call the unversioned "/api/...". Routes
// now live under "/api/v1/...", so unversioned requests are rewritten to v1
// before routing. Versioning thus does not break old clients; new clients
// call "/api/v1/..." directly (transition/deprecation).
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path != null &&
        path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
    {
        context.Request.Path = "/api/v1" + path.Substring("/api".Length);
    }
    await next();
});

// --- MIDDLEWARE ORDER MATTERS ---
app.UseRouting();
app.UseCors(corsPolicyName);
app.UseAuthentication();
// AFTER authentication: limits are partitioned per user (before it,
// context.User would be empty and everyone would fall back to their IP). The
// "auth" policy is IP-based and unaffected.
app.UseRateLimiter();
app.UseAuthorization();

// --- MIGRATIONS, ROLES AND THE ADMIN USER ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    // Schema setup depends on the provider:
    //  - Postgres (production): versioned EF migrations are applied.
    //  - SQLite (development/test): no migrations; the schema is created from
    //    the model (EnsureCreated). When the model changes, delete the dev
    //    database file and it is recreated.
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    if (dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roleNames = { "Admin", "User" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // The admin account is no longer created with a fixed "admin" /
    // "Password123!". User name and password come from configuration (or the
    // ADMIN_USERNAME / ADMIN_PASSWORD environment variables); without a
    // password no account is created.
    // The appsettings.json placeholders are empty strings, so `??` is not
    // enough: empty values must count as unset too.
    static string? FirstNonEmpty(IConfiguration configuration, params string[] keys)
        => keys.Select(key => configuration[key])
               .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    var adminUserName = FirstNonEmpty(app.Configuration, "ADMIN_USERNAME", "Admin:UserName");
    var adminEmail = FirstNonEmpty(app.Configuration, "ADMIN_EMAIL", "Admin:Email");
    var adminPassword = FirstNonEmpty(app.Configuration, "ADMIN_PASSWORD", "Admin:Password");

    if (!string.IsNullOrWhiteSpace(adminUserName) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var adminUser = await userManager.FindByNameAsync(adminUserName);
        if (adminUser == null)
        {
            adminUser = new AppUser
            {
                UserName = adminUserName,
                Email = adminEmail ?? $"{adminUserName}@sineuyum.local"
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Admin user '{UserName}' created.", adminUserName);
            }
            else
            {
                logger.LogError("Could not create the admin user: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
    else
    {
        logger.LogInformation(
            "Admin account not seeded. Set ADMIN_USERNAME and ADMIN_PASSWORD to create it.");
    }
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapHub<NotificationHub>("/notificationHub");
app.MapControllers();

app.Run();

public partial class Program { }
