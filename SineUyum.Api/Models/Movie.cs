namespace SineUyum.Api.Models
{
    // Models/Movie.cs
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Movie
    {
        [Key] // Bu özelliğin Primary Key (birincil anahtar) olduğunu belirtir.
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // Bu alanın veritabanı tarafından otomatik artırılmayacağını, bizim tarafımızdan atanacağını belirtir.
        public int Id { get; set; } // Bu ID, TMDb'deki film ID'si ile aynı olacak.

        public string Title { get; set; } = string.Empty;

        public string? PosterPath { get; set; } // Afiş resminin yolu (nullable olabilir)
    }

}
