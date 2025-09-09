using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistIdToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WatchlistId",
                table: "Messages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_WatchlistId",
                table: "Messages",
                column: "WatchlistId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Watchlists_WatchlistId",
                table: "Messages",
                column: "WatchlistId",
                principalTable: "Watchlists",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Watchlists_WatchlistId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_WatchlistId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "WatchlistId",
                table: "Messages");
        }
    }
}
