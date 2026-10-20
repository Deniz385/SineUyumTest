using System.Text.Json.Serialization;
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

    // GET /api/v1/event/my-status. Şekil duruma göre değişir; o durumda
    // anlamı olmayan alanlar yanıta hiç yazılmaz (istemciler alanın
    // varlığına bakıyor):
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

    // POST /api/v1/event/vote isteği.
    public class EventVoteDto
    {
        public int GroupId { get; set; }
        public int MovieId { get; set; }
    }
}
