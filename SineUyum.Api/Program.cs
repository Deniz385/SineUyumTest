using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SineUyum.Api.Data;
using SineUyum.Api.Hubs;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- CORS POLİTİKASI ---
var corsPolicyName = "AllowReactApp";

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy.WithOrigins(
                "https://super-duper-dollop-g959prvw5q539q6-5173.app.github.dev",
                "https://super-duper-dollop-g959prvw5q539q6-5074.app.github.dev"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // SignalR için bu gerekli
    });
});

// --- DATABASE VE IDENTITY ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// --- JWT AUTHENTICATION ---
var jwtKey = builder.Configuration["JWT_KEY"] ?? builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT anahtarı yapılandırılmamış. Lütfen Codespaces secret'a 'JWT_KEY' ekleyin.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
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

// --- CONTROLLERS & SWAGGER ---
builder.Services.AddControllers(); // <-- HATA GİDERİLDİ: EKSİK OLAN SATIR BUYDU.
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- SWAGGER ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- MIDDLEWARE SIRASI ÖNEMLİ ---
app.UseRouting();
app.UseCors(corsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

// --- ROLLERİ VE ADMİN KULLANICISINI OLUŞTURMA ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roleNames = { "Admin", "User" };
    foreach (var roleName in roleNames)
    {
        var roleExist = await roleManager.RoleExistsAsync(roleName);
        if (!roleExist)
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    var adminUser = await userManager.FindByNameAsync("admin");
    if (adminUser == null)
    {
        adminUser = new AppUser
        {
            UserName = "admin",
            Email = "admin@sineuyum.com"
        };
        var result = await userManager.CreateAsync(adminUser, "Password123!");

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}

app.MapHub<NotificationHub>("/notificationHub");
app.MapControllers(); // Bu satırın çalışabilmesi için AddControllers() servisi gerekliydi.

app.Run();

public partial class Program { }