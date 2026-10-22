using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieTranslationPosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PosterPath",
                table: "MovieTranslations",
                type: "text",
                nullable: true);

            // The stored poster was fetched together with the stored title, so
            // it belongs to the translation in that same language (the Turkish
            // rows copied by AddMovieTranslations). Other languages fall back to
            // Movies.PosterPath until their row is refetched.
            migrationBuilder.Sql(
                @"UPDATE ""MovieTranslations"" AS t
                  SET ""PosterPath"" = m.""PosterPath""
                  FROM ""Movies"" AS m
                  WHERE t.""MovieId"" = m.""Id"" AND t.""Title"" = m.""Title"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PosterPath",
                table: "MovieTranslations");
        }
    }
}
