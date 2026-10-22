using System.Text.Json.Serialization;
using Microsoft.Extensions.Localization;
using SineUyum.Api.Models;

namespace SineUyum.Api.Dtos
{
    public class CinemaEventDto
    {
        public int Id { get; set; }
        public DateTime EventDate { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public int GroupSize { get; set; }
        public int? SelectedMovieId { get; set; }

        public static CinemaEventDto From(CinemaEvent e) => new()
        {
            Id = e.Id,
            EventDate = e.EventDate,
            LocationName = e.LocationName,
            Address = e.Address,
            GroupSize = e.GroupSize,
            SelectedMovieId = e.SelectedMovieId
        };
    }

    // GET /api/v1/event (admin)
    public class EventListItemDto
    {
        public int Id { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public int GroupSize { get; set; }
        public int ParticipantCount { get; set; }
    }

    // GET /api/v1/event/my-status. The shape depends on the status; fields
    // that mean nothing in that status are left out entirely (clients check
    // whether a field exists):
    //   NONE      -> status, message
    //   AVAILABLE -> status, event
    //   PENDING   -> status, event
    //   MATCHED   -> status, event, group, votes, suggestedMovies
    public class EventStatusDto
    {
        public string Status { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CinemaEventDto? Event { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EventGroupDto? Group { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<EventGroupVoteDto>? Votes { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<MovieSummaryDto>? SuggestedMovies { get; set; }
    }

    public class EventGroupDto
    {
        public int Id { get; set; }
        // For old clients; suggestions now live in EventStatusDto.SuggestedMovies.
        public string? SuggestedMovieIds { get; set; }
        public List<EventGroupMemberDto> Members { get; set; } = new();
    }

    public class EventGroupMemberDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? ProfileImageUrl { get; set; }
    }

    public class EventGroupVoteDto
    {
        public string UserId { get; set; } = string.Empty;
        public int MovieId { get; set; }
    }

    // POST /api/v1/event/vote request.
    public class EventVoteDto
    {
        public int GroupId { get; set; }
        public int MovieId { get; set; }
    }
}

namespace SineUyum.Api.Dtos
{
    // POST /api/v1/admin/events/{id}/create-groups (202) and
    // GET /api/v1/admin/events/{id}/matching. Status: Queued | Running |
    // Succeeded | Failed. The client polls until Succeeded/Failed.
    public class MatchingRunDto
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public int? GroupCount { get; set; }
        public int? UngroupedCount { get; set; }
        public string? Error { get; set; }

        // A displayable summary (compatible with the old response's "message" field).
        public string Message { get; set; } = string.Empty;

        // Texts in the language of the given localizer; rows from before
        // localization only have the stored Error text.
        public static MatchingRunDto From(Models.MatchingRun run, IStringLocalizer t)
        {
            var error = run.ErrorKey != null
                ? t[run.ErrorKey, Localization.MessageArgs.Deserialize(run.ErrorArgs)]
                : run.Error;
            return new()
            {
                Message = run.Status switch
                {
                    Models.MatchingRunStatus.Queued => t["matching.queued"],
                    Models.MatchingRunStatus.Running => t["matching.running"],
                    Models.MatchingRunStatus.Succeeded => run.UngroupedCount > 0
                        ? t["matching.succeededWithUngrouped", run.GroupCount ?? 0, run.UngroupedCount ?? 0]
                        : t["matching.succeeded", run.GroupCount ?? 0],
                    _ => error ?? t["matching.failed"]
                },
                Id = run.Id,
                EventId = run.CinemaEventId,
                Status = run.Status.ToString(),
                CreatedAt = run.CreatedAt,
                StartedAt = run.StartedAt,
                FinishedAt = run.FinishedAt,
                GroupCount = run.GroupCount,
                UngroupedCount = run.UngroupedCount,
                Error = error
            };
        }
    }
}
