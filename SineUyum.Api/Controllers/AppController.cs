using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SineUyum.Api.Controllers
{
    // Mobil uygulama açılışta buraya bakar: sunucu ayakta mı, uygulamanın
    // sürümü hâlâ destekliyor mu, bakım modunda mıyız.
    [ApiController]
    [Route("api/app")]
    [AllowAnonymous]
    public class AppController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AppController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("config")]
        public ActionResult<AppConfigDto> GetConfig()
        {
            return Ok(new AppConfigDto
            {
                // Bu sürümden eskisi zorunlu güncellemeye düşer.
                MinimumSupportedVersion = _configuration["App:MinimumSupportedVersion"] ?? "1.0.0",
                LatestVersion = _configuration["App:LatestVersion"] ?? "1.0.0",
                MaintenanceMode = _configuration.GetValue("App:MaintenanceMode", false),
                MaintenanceMessage = _configuration["App:MaintenanceMessage"],
                TmdbImageBaseUrl = _configuration["TMDb:ImageBaseUrl"] ?? "https://image.tmdb.org/t/p/"
            });
        }
    }

    public class AppConfigDto
    {
        public string MinimumSupportedVersion { get; set; } = "1.0.0";
        public string LatestVersion { get; set; } = "1.0.0";
        public bool MaintenanceMode { get; set; }
        public string? MaintenanceMessage { get; set; }
        public string TmdbImageBaseUrl { get; set; } = string.Empty;
    }
}
