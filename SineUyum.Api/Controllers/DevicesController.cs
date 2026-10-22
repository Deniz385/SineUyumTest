using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SineUyum.Api.Data;
using SineUyum.Api.Dtos;
using SineUyum.Api.Models;
using System.Security.Claims;

namespace SineUyum.Api.Controllers
{
    // Device (FCM token) registration for push notifications.
    [ApiController]
    [Route("api/devices")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        // Registered device limit per account; beyond it the device not seen
        // for the longest time is removed. Keeps endless fake registrations
        // from bloating the table.
        public const int MaxDevicesPerUser = 20;

        private readonly ApplicationDbContext _context;

        public DevicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Called by the app after sign-in and whenever the FCM token is
        // refreshed; calling it again is safe (the same token is updated).
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterDeviceDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                await UpsertAsync(userId, dto);
            }
            catch (DbUpdateException)
            {
                // Two concurrent registrations of the same token: the unique
                // index rejects the second one. Moving the winner to this user is enough.
                _context.ChangeTracker.Clear();
                await UpsertAsync(userId, dto);
            }

            return NoContent();
        }

        // Called on logout (and when the session expires). No authentication
        // required: a device whose session is gone must still be able to stop
        // receiving the previous user's notifications. Only the device knows the token.
        [HttpPost("unregister")]
        [AllowAnonymous]
        public async Task<IActionResult> Unregister([FromBody] UnregisterDeviceDto dto)
        {
            await _context.DeviceTokens.Where(d => d.Token == dto.Token).ExecuteDeleteAsync();
            return NoContent();
        }

        private async Task UpsertAsync(string userId, RegisterDeviceDto dto)
        {
            var now = DateTime.UtcNow;
            var device = await _context.DeviceTokens.FirstOrDefaultAsync(d => d.Token == dto.Token);

            if (device != null)
            {
                // Another account may have signed in on the same device: the
                // row moves to the new user, the previous user's notifications stop going here.
                device.UserId = userId;
                device.Platform = dto.Platform;
                device.LastSeenAt = now;
            }
            else
            {
                _context.DeviceTokens.Add(new DeviceToken
                {
                    UserId = userId,
                    Token = dto.Token,
                    Platform = dto.Platform,
                    CreatedAt = now,
                    LastSeenAt = now,
                });
            }

            await _context.SaveChangesAsync();

            var stale = await _context.DeviceTokens
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.LastSeenAt)
                .Skip(MaxDevicesPerUser)
                .Select(d => d.Id)
                .ToListAsync();
            if (stale.Count > 0)
            {
                await _context.DeviceTokens.Where(d => stale.Contains(d.Id)).ExecuteDeleteAsync();
            }
        }
    }
}
