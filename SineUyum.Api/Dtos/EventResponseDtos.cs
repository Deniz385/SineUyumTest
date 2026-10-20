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
        // Eski istemciler için; öneriler artık EventStatusDto.SuggestedMovies'te.
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

namespace SineUyum.Api.Dtos
{
    // POST /api/v1/admin/events/{id}/create-groups (202) ve
    // GET /api/v1/admin/events/{id}/matching. Durum: Queued | Running |
    // Succeeded | Failed. İstemci Succeeded/Failed olana dek yoklar.
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

        // Gösterilebilir özet (eski yanıttaki "message" alanıyla uyumlu).
        public string Message { get; set; } = string.Empty;

        public static MatchingRunDto From(Models.MatchingRun run) => new()
        {
            Message = run.Status switch
            {
                Models.MatchingRunStatus.Queued => "Eşleştirme sıraya alındı.",
                Models.MatchingRunStatus.Running => "Eşleştirme sürüyor.",
                Models.MatchingRunStatus.Succeeded => run.UngroupedCount > 0
                    ? $"{run.GroupCount} grup oluşturuldu; {run.UngroupedCount} kişi gruba alınamadı."
                    : $"{run.GroupCount} grup oluşturuldu.",
                _ => run.Error ?? "Eşleştirme başarısız oldu."
            },
            Id = run.Id,
            EventId = run.CinemaEventId,
            Status = run.Status.ToString(),
            CreatedAt = run.CreatedAt,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            GroupCount = run.GroupCount,
            UngroupedCount = run.UngroupedCount,
            Error = run.Error
        };
    }
}
