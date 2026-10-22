using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Dtos
{
    // POST /api/v1/diary. watchedOn is "yyyy-MM-dd"; an optional rating (1-10)
    // also creates or changes the user's rating of the movie.
    public class CreateDiaryEntryDto
    {
        [Required(ErrorMessage = "validation.required")]
        public int? MovieId { get; set; }

        [Required(ErrorMessage = "validation.required")]
        public DateOnly? WatchedOn { get; set; }

        public bool Rewatch { get; set; }

        [StringLength(500, ErrorMessage = "diary.noteLength")]
        public string? Note { get; set; }

        [Range(1, 10, ErrorMessage = "validation.range")]
        public int? Rating { get; set; }
    }

    // PUT /api/v1/diary/{id}: replaces the date, rewatch flag and note.
    public class UpdateDiaryEntryDto
    {
        [Required(ErrorMessage = "validation.required")]
        public DateOnly? WatchedOn { get; set; }

        public bool Rewatch { get; set; }

        [StringLength(500, ErrorMessage = "diary.noteLength")]
        public string? Note { get; set; }
    }

    public class DiaryEntryDto
    {
        public int Id { get; set; }
        public MovieSummaryDto Movie { get; set; } = new();
        public DateOnly WatchedOn { get; set; }
        public bool Rewatch { get; set; }
        public string? Note { get; set; }
        // The diary owner's current rating of this movie (not tied to the entry).
        public int? Rating { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // GET /api/v1/diary/user/{userId}: newest first.
    public class DiaryPageDto
    {
        public List<DiaryEntryDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }

    // GET /api/v1/diary/user/{userId}/summary
    public class DiarySummaryDto
    {
        public int Year { get; set; }
        public int TotalCount { get; set; }
        public int RewatchCount { get; set; }
        // Always 12 items, January to December.
        public List<DiaryMonthCountDto> Months { get; set; } = new();
        // Years that have entries, newest first.
        public List<int> AvailableYears { get; set; } = new();
    }

    public class DiaryMonthCountDto
    {
        public int Month { get; set; }
        public int Count { get; set; }
    }
}
