using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovieTranslations",
                columns: table => new
                {
                    MovieId = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieTranslations", x => new { x.MovieId, x.Language });
                    table.ForeignKey(
                        name: "FK_MovieTranslations_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Every stored title so far was fetched in Turkish (the catalog's
            // only language until now): keep them as the Turkish titles. The
            // other languages are filled in by MovieTranslationWorker.
            migrationBuilder.Sql(
                @"INSERT INTO ""MovieTranslations"" (""MovieId"", ""Language"", ""Title"", ""UpdatedAt"")
                  SELECT ""Id"", 'tr', ""Title"", now() FROM ""Movies"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovieTranslations");
        }
    }
}
