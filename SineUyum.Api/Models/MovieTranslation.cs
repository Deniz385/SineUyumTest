using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    // A movie's title and poster in one language (TMDb picks a poster for the
    // language, e.g. with a localized title on it). Movie.Title/PosterPath are
    // the fallback when the request language has no row (or no poster) yet; rows are added when
    // the movie is first stored, when someone opens or rates it in another
    // language, and by MovieTranslationWorker for the rest.
    public class MovieTranslation
    {
        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        // Two-letter language code ("en", "tr").
        [MaxLength(8)]
        public string Language { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? PosterPath { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
