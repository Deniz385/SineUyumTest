namespace SineUyum.Api.Models
{
    public enum MatchingRunStatus
    {
        Queued,
        Running,
        Succeeded,
        Failed
    }

    // An event's matching job (M7). It does not run in the request path: the
    // admin's request creates the row and the background worker processes it.
    // The row survives a server restart and the job is picked up again.
    public class MatchingRun
    {
        public int Id { get; set; }

        public int CinemaEventId { get; set; }
        public CinemaEvent CinemaEvent { get; set; } = null!;

        public MatchingRunStatus Status { get; set; } = MatchingRunStatus.Queued;

        public string? RequestedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        // On success: number of groups created and participants left without a group.
        public int? GroupCount { get; set; }
        public int? UngroupedCount { get; set; }

        // On failure: a reason that can be shown to users (no internal details).
        // ErrorKey + ErrorArgs (JSON array) are rendered in the reader's
        // language; Error keeps the English text (and older rows' Turkish text).
        public string? Error { get; set; }
        public string? ErrorKey { get; set; }
        public string? ErrorArgs { get; set; }
    }
}
