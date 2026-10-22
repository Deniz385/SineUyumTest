using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTmdbVoteAverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TmdbVoteAverage",
                table: "Movies",
                type: "double precision",
                nullable: true);

            // The average was not stored before: process every movie once more
            // (GenreBackfillWorker; existing genres are not added again).
            migrationBuilder.Sql("UPDATE \"Movies\" SET \"GenresSyncedAt\" = NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TmdbVoteAverage",
                table: "Movies");
        }
    }
}
