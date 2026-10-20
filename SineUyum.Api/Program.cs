using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SineUyum.Api.Data;
using SineUyum.Api.Hubs;
using SineUyum.Api.Middleware;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using SineUyum.Api.Services.Media;
using SineUyum.Api.Services.Push;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- LOGLAMA (Serilog) ---
// Yapılandırma appsettings "Serilog" bölümünden okunur; yoksa makul bir
// varsayılana düşülür. İstek logları aşağıda UseSerilogRequestLogging ile.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    // ExceptionHandlerMiddleware, işleyiciye vermeden önce HER istisnayı
    // "An unhandled exception" diye Error + stack trace ile logluyor; beklenen
    // iş kuralı hataları (404, 413, 415...) da gerçek hata gibi görünüyordu.
    // GlobalExceptionHandler zaten beklenmeyenleri istisnasıyla Error'da,
    // beklenenleri Information'da logladığı için bu kopya kapatılır.
    .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", Serilog.Events.LogEventLevel.Fatal)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- CORS POLİTİKASI ---
// Origin listesi artık koda gömülü değil, konfigürasyondan okunuyor:
// appsettings "Cors:AllowedOrigins" dizisi veya CORS_ALLOWED_ORIGINS ortam
// değişkeni (virgülle ayrılmış). Mobil istemci CORS'a tabi değildir, bu
// politika yalnızca web istemcisi içindir.
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
                .AllowCredentials(); // SignalR için bu gerekli
        }
    });
});

// --- DATABASE VE IDENTITY ---
// Sağlayıcı konfigürasyondan seçilir: "Database:Provider" = Sqlite (varsayılan,
// geliştirme) veya Postgres (production). Ayrıntı: docs → M2.
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
    // Aynı e-posta ile birden fazla hesap açılmasını engelle.
    options.User.RequireUniqueEmail = true;

    // RegisterDto "en az 6 karakter" diyordu ama Identity varsayılanı özel
    // karakter de istiyordu; kullanıcıya gösterilen mesajla kural artık uyumlu.
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>();

// --- JWT AUTHENTICATION ---
var jwtKey = builder.Configuration["JWT_KEY"] ?? builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException(
        "JWT anahtarı yapılandırılmamış. 'JWT_KEY' ortam değişkenini veya 'Jwt:Key' ayarını tanımlayın.");
}

// HMAC-SHA512 kullanıldığı için anahtarın en az 64 byte olması gerekir.
if (Encoding.UTF8.GetByteCount(jwtKey) < 64)
{
    throw new InvalidOperationException(
        "JWT anahtarı HS512 için çok kısa. En az 64 byte (64 ASCII karakter) uzunluğunda bir anahtar kullanın.");
}

// Issuer/Audience konfigüre edilmemişse (yerel geliştirme) doğrulama kapatılır;
// tanımlıysa zorunlu tutulur. Böylece ortam değiştiğinde API çalışmaya devam eder.
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
     // SignalR'ın kimlik doğrulamasını Hub'a iletmesi için:
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

// --- AUTHORIZATION SERVİSİ ---
builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.AddScoped<MatchingService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<MovieCatalogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<PrivacyService>();
// Ödeme sağlayıcıları (IPaymentProvider) burada kaydedilir. Henüz gerçek
// sağlayıcı yok: abonelik yalnızca yönetici tarafından tanımlanabilir,
// /api/v1/subscription/verify 503 döner. Bkz. yol haritası G4.

// --- MEDYA (profil fotoğrafları) ---
// Yerel disk; Docker'da kalıcı volume (/data/media). Storage__MediaRoot ile değişir.
var mediaRoot = builder.Configuration["Storage:MediaRoot"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "media");
var mediaStorage = new LocalMediaStorage(mediaRoot);
builder.Services.AddSingleton<IMediaStorage>(mediaStorage);

// --- PUSH BİLDİRİMİ (FCM HTTP v1) ---
// Firebase servis hesabı anahtarı verilirse açılır; verilmezse push sessizce
// kapalıdır, bildirimler REST + SignalR ile çalışmaya devam eder.
//   FCM_SERVICE_ACCOUNT_JSON : anahtarın JSON içeriği (secret olarak), ya da
//   FCM_SERVICE_ACCOUNT_FILE : anahtar dosyasının yolu.
//   FCM_PROJECT_ID           : (opsiyonel) varsayılan anahtardaki project_id.
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
            ?? throw new InvalidOperationException("project_id boş.");
    }
    catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
    {
        // Yanlış yapılandırma sessizce "push kapalı"ya dönmesin; açılışta görünsün.
        throw new InvalidOperationException(
            "FCM servis hesabı anahtarı okunamadı (geçerli JSON ve project_id gerekli).", ex);
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
// Giriş/kayıt/yenileme uçlarına brute force koruması. Dakikadaki istek sınırı
// konfigürasyondan okunur (varsayılan 10); integration testleri tek IP'den çok
// sayıda istek attığı için orada yüksek bir değere çekilir.
var authPermitLimit = builder.Configuration.GetValue("RateLimit:AuthPermitLimit", 10);
var uploadPermitLimit = builder.Configuration.GetValue("RateLimit:UploadPermitLimit", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromMinutes(1)
        }));

    // Ödeme doğrulama: sağlayıcıya dış çağrı yapar; kullanıcı başına sınırlı.
    options.AddPolicy("payment", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = uploadPermitLimit,
            Window = TimeSpan.FromMinutes(1)
        }));

    // Fotoğraf yükleme: resim çözmek CPU/bellek ister; kullanıcı başına sınırlı.
    options.AddPolicy("upload", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = uploadPermitLimit,
            Window = TimeSpan.FromMinutes(1)
        }));
});

// --- GLOBAL HATA YÖNETİMI ---
// Yakalanmayan istisnalar tek yerde ProblemDetails'e çevrilir; stack trace
// istemciye sızmaz, ayrıntı yalnızca log'a yazılır.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- CACHE ---
// TMDb yanıtlarını (popüler, vizyondakiler, arama, film detayı) TTL'li
// önbellekte tutmak için. Kota tüketimini azaltır, yanıtı hızlandırır.
// SizeLimit, farklı arama sorgularının belleği sınırsız şişirmesini önler.
builder.Services.AddMemoryCache(options => options.SizeLimit = 2048);

// --- CONTROLLERS & SWAGGER ---
// Tüm API rotaları "api/v1/..." altına alınır (bkz. ApiV1RouteConvention).
builder.Services.AddControllers(options =>
    options.Conventions.Add(new SineUyum.Api.Conventions.ApiV1RouteConvention()));
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// İstek başına özet log (yöntem, yol, durum kodu, süre). En dışta olmalı ki
// exception handler hatayı 404/400/500'e çevirdikten SONRA gerçek dönen durum
// kodunu loglasın (aksi halde her hata "500" görünür).
app.UseSerilogRequestLogging();

// Hata yöneticisi erken olmalı ki sonraki tüm katmanlardaki istisnaları
// yakalayabilsin; request logging'in hemen içinde.
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

// --- MEDYA DOSYALARI ---
// Yalnızca sunucunun ürettiği .jpg dosyaları sunulur; bilinmeyen uzantılar
// (yanlışlıkla klasöre düşmüş bir dosya dahil) 404 döner. Dosya adları
// benzersiz olduğu için uzun süre önbelleklenebilir.
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

// --- GERİYE DÖNÜK UYUMLULUK (SÜRÜMSÜZ /api/*) ---
// Eski istemciler (dağıtılmış web/mobil) sürümsüz "/api/..." çağırır.
// Rotalar artık "/api/v1/..." altında olduğundan, sürümsüz istekleri
// routing'den önce v1'e yönlendiririz. Böylece sürümleme eski istemcileri
// kırmaz; yeni istemciler doğrudan "/api/v1/..." kullanır (geçiş/deprecation).
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

// --- MIDDLEWARE SIRASI ÖNEMLİ ---
app.UseRouting();
app.UseCors(corsPolicyName);
app.UseAuthentication();
// Kimlik doğrulamadan SONRA: "upload" politikası kullanıcıya göre bölümlenir
// (öncesinde context.User boş olur ve IP'ye düşerdi). "auth" politikası IP
// tabanlı olduğu için etkilenmez.
app.UseRateLimiter();
app.UseAuthorization();

// --- MIGRATION, ROLLER VE ADMİN KULLANICISI ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    // Şema hazırlığı sağlayıcıya göre:
    //  - Postgres (production): versiyonlu EF migration'ları uygulanır.
    //  - SQLite (geliştirme/test): migration tutulmuyor; şema doğrudan modelden
    //    üretilir (EnsureCreated). Model değişince dev veritabanı dosyası
    //    silinip yeniden oluşturulur.
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

    // Admin hesabı artık sabit "admin" / "Password123!" ile oluşturulmuyor.
    // Kullanıcı adı ve parola konfigürasyondan (ya da ADMIN_USERNAME /
    // ADMIN_PASSWORD ortam değişkenlerinden) okunur; parola verilmezse
    // hesap hiç oluşturulmaz.
    // appsettings.json'daki yer tutucular boş string olduğu için `??` yeterli
    // değil: boş değerler de tanımsız sayılmalı.
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
                logger.LogInformation("Admin kullanıcısı '{UserName}' oluşturuldu.", adminUserName);
            }
            else
            {
                logger.LogError("Admin kullanıcısı oluşturulamadı: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
    else
    {
        logger.LogInformation(
            "Admin hesabı tohumlanmadı. Oluşturmak için ADMIN_USERNAME ve ADMIN_PASSWORD tanımlayın.");
    }
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapHub<NotificationHub>("/notificationHub");
app.MapControllers();

app.Run();

public partial class Program { }
