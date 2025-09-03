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
    options.Events = new JwtBearerEvents { OnAuthenticationFailed = context => { Console.WriteLine("--- TOKEN DO�RULAMASI BA�ARISIZ OLDU ---"); Console.WriteLine("Exception: " + context.Exception.ToString()); return Task.CompletedTask; }, OnTokenValidated = context => { Console.WriteLine("--- Token ba�ar�yla do�ruland�. ---"); return Task.CompletedTask; } };
});

// Authorization'� ekliyoruz
builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => { options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"", Name = "Authorization", In = Microsoft.OpenApi.Models.ParameterLocation.Header, Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey, Scheme = "Bearer" }); options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement { { new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } }); });
var corsPolicyName = "AllowReactApp";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: corsPolicyName,
        policy =>
        {
            policy.WithOrigins("http://localhost:5173") // React uygulamasının adresi
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}
//app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }