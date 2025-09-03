using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Servisler
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
    options.Events = new JwtBearerEvents { OnAuthenticationFailed = context => { Console.WriteLine("--- TOKEN DOÐRULAMASI BAÞARISIZ OLDU ---"); Console.WriteLine("Exception: " + context.Exception.ToString()); return Task.CompletedTask; }, OnTokenValidated = context => { Console.WriteLine("--- Token baþarýyla doðrulandý. ---"); return Task.CompletedTask; } };
});

// Authorization'ý ekliyoruz
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => { options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"", Name = "Authorization", In = Microsoft.OpenApi.Models.ParameterLocation.Header, Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey, Scheme = "Bearer" }); options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement { { new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } }); });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// --- MÝNÝMAL API ENDPOINT'LERÝ ---

// Register Endpoint'i
app.MapPost("/register", async (UserManager<AppUser> userManager, RegisterDto registerDto) =>
{
    var user = new AppUser { UserName = registerDto.Username, Email = registerDto.Email };
    var result = await userManager.CreateAsync(user, registerDto.Password);
    return result.Succeeded ? Results.Ok("Kullanýcý baþarýyla oluþturuldu.") : Results.BadRequest(result.Errors);
});

// Login Endpoint'i
app.MapPost("/login", async (UserManager<AppUser> userManager, IConfiguration config, LoginDto loginDto) =>
{
    var user = await userManager.FindByNameAsync(loginDto.Username);
    if (user != null && await userManager.CheckPasswordAsync(user, loginDto.Password))
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!) // <--- DEÐÝÞÝKLÝK BURADA
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)); // <--- DEÐÝÞÝKLÝK BURADA
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.Now.AddDays(7),
            SigningCredentials = creds,
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"]
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Results.Ok(new { token = tokenHandler.WriteToken(token) });
    }
    return Results.Unauthorized();
});

// Korumalý Test Endpoint'i
app.MapGet("/test-auth", (ClaimsPrincipal user) =>
{
    var username = user.FindFirstValue(ClaimTypes.Name);
    return Results.Ok($"Token geçerli! Giriþ yapan kullanýcý: {username}");
}).RequireAuthorization(); // <-- Bu endpoint'in yetki gerektirdiðini belirtiyoruz.

app.Run();

public partial class Program { }