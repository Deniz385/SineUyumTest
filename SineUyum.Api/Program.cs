using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SineUyum.Api.Data;
using SineUyum.Api.Hubs;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// --- RATE LIMITING ---
// Giriş/kayıt/yenileme uçlarına brute force koruması. Dakikadaki istek sınırı
// konfigürasyondan okunur (varsayılan 10); integration testleri tek IP'den çok
// sayıda istek attığı için orada yüksek bir değere çekilir.
var authPermitLimit = builder.Configuration.GetValue("RateLimit:AuthPermitLimit", 10);
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
});

// --- CONTROLLERS & SWAGGER ---
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

// --- MIDDLEWARE SIRASI ÖNEMLİ ---
app.UseRouting();
app.UseCors(corsPolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// --- MIGRATION, ROLLER VE ADMİN KULLANICISI ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    // Bekleyen migration'ları uygula (deploy'da elle çalıştırma ihtiyacını kaldırır).
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();

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
