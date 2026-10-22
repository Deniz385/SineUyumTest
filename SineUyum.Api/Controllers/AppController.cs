using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SineUyum.Api.Controllers
{
    // The mobile app checks this on startup: is the server up, is the app's
    // version still supported, are we in maintenance mode.
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
                // Versions older than this are forced to update.
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
