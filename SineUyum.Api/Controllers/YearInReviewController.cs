using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Dtos;
using SineUyum.Api.Exceptions;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    // A year in movies (the shareable yearly recap). Built from the diary and
    // ratings, so the same visibility rule as the profile details.
    [ApiController]
    [Route("api/year-in-review")]
    [Authorize]
    public class YearInReviewController : ControllerBase
    {
        private readonly YearInReviewService _service;
        private readonly PrivacyService _privacy;
        private readonly IStringLocalizer _t;

        public YearInReviewController(YearInReviewService service, PrivacyService privacy, IStringLocalizer localizer)
        {
            _service = service;
            _privacy = privacy;
            _t = localizer;
        }

        // ?year= defaults to the current (UTC) year: the year so far.
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<YearInReviewDto>> ForUser(string userId, [FromQuery] int? year = null)
        {
            var viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (viewerId == null) return Unauthorized();

            var selectedYear = year ?? DateTime.UtcNow.Year;
            if (selectedYear < DiaryController.EarliestWatchedOn.Year || selectedYear > DateTime.UtcNow.Year)
            {
                throw new DomainException("diary.invalidYear");
            }

            var owner = await _privacy.EnsureCanViewDetailsAsync(viewerId, userId, User.IsInRole("Admin"));
            return Ok(await _service.BuildAsync(viewerId, userId, owner.UserName ?? string.Empty, selectedYear, _t));
        }
    }
}
