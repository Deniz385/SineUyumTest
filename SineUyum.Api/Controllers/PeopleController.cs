using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using SineUyum.Api.Services;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    // People to follow (first-run welcome, People tab). Only people open to
    // discovery are suggested, the same rule as the taste twin: a public
    // profile, not hidden from search, no block, not followed yet.
    [ApiController]
    [Route("api/people")]
    [Authorize]
    public class PeopleController : ControllerBase
    {
        public const int MinCommonMovies = 3;
        public const double MinCompatibility = 50;
        private const int MaxCandidates = 300;

        private readonly ApplicationDbContext _context;
        private readonly SocialService _social;
        private readonly PrivacyService _privacy;

        public PeopleController(ApplicationDbContext context, SocialService social, PrivacyService privacy)
        {
            _context = context;
            _social = social;
            _privacy = privacy;
        }

        // By taste when possible (most compatible among people sharing at least
        // 3 rated movies), otherwise the most followed people.
        [HttpGet("suggestions")]
        public async Task<ActionResult<PeopleSuggestionsDto>> Suggestions([FromQuery] int limit = 10)
        {
            var me = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            limit = Math.Clamp(limit, 1, 30);

            var blocked = _privacy.BlockedPartnerIds(me);
            var following = _context.UserFollows.Where(f => f.FollowerId == me).Select(f => f.FollowingId);
            var open = _context.Users.Where(u =>
                u.Id != me && u.ProfileVisibility == ProfileVisibility.Public && !u.HiddenFromSearch &&
                !blocked.Contains(u.Id) && !following.Contains(u.Id));

            var myMovies = _context.UserRatings.Where(r => r.UserId == me).Select(r => r.MovieId);
            var openIds = open.Select(u => u.Id);
            var candidates = await _context.UserRatings
                .Where(r => myMovies.Contains(r.MovieId) && openIds.Contains(r.UserId))
                .GroupBy(r => r.UserId)
                .Where(g => g.Count() >= MinCommonMovies)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(MaxCandidates)
                .ToListAsync();

            var byTaste = (await _social.CompatibilityDetailsAsync(me, candidates))
                .Where(p => p.Value.CommonCount >= MinCommonMovies && p.Value.Score >= MinCompatibility)
                .OrderByDescending(p => p.Value.Score).ThenByDescending(p => p.Value.CommonCount).ThenBy(p => p.Key, StringComparer.Ordinal)
                .Take(limit)
                .ToList();

            if (byTaste.Count > 0)
            {
                var ids = byTaste.Select(p => p.Key).ToList();
                var people = await PeopleAsync(open.Where(u => ids.Contains(u.Id)));
                return Ok(new PeopleSuggestionsDto
                {
                    Basis = "taste",
                    Items = byTaste.Where(p => people.ContainsKey(p.Key)).Select(p =>
                    {
                        var person = people[p.Key];
                        person.Compatibility = p.Value.Score;
                        person.CommonMovieCount = p.Value.CommonCount;
                        return person;
                    }).ToList()
                });
            }

            var popular = await open
                .Select(u => new { u.Id, Followers = _context.UserFollows.Count(f => f.FollowingId == u.Id), Ratings = u.Ratings.Count })
                .Where(u => u.Ratings > 0)
                .OrderByDescending(u => u.Followers).ThenByDescending(u => u.Ratings).ThenBy(u => u.Id)
                .Take(limit)
                .Select(u => u.Id)
                .ToListAsync();
            var popularPeople = await PeopleAsync(open.Where(u => popular.Contains(u.Id)));
            return Ok(new PeopleSuggestionsDto
            {
                Basis = "popular",
                Items = popular.Where(popularPeople.ContainsKey).Select(id => popularPeople[id]).ToList()
            });
        }

        private Task<Dictionary<string, PersonSuggestionDto>> PeopleAsync(IQueryable<AppUser> users) =>
            users
                .Select(u => new PersonSuggestionDto
                {
                    User = new UserBriefWithImageDto { Id = u.Id, UserName = u.UserName, ProfileImageUrl = u.ProfileImageUrl },
                    Bio = u.Bio,
                    FollowerCount = _context.UserFollows.Count(f => f.FollowingId == u.Id)
                })
                .ToDictionaryAsync(p => p.User.Id);
    }
}
