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

            // Ortalama önceden saklanmıyordu: tüm filmler bir kez yeniden
            // işlensin (GenreBackfillWorker; mevcut türler tekrar eklenmez).
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
