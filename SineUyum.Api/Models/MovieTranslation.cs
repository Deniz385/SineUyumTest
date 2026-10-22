using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    // A movie's title in one language (TMDb's translation). Movie.Title is the
    // fallback when the request language has no row yet; rows are added when
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

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
