using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class SetNullOnWatchlistDeleteInMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Watchlists_WatchlistId",
                table: "Messages");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Watchlists_WatchlistId",
                table: "Messages",
                column: "WatchlistId",
                principalTable: "Watchlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Watchlists_WatchlistId",
                table: "Messages");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Watchlists_WatchlistId",
                table: "Messages",
                column: "WatchlistId",
                principalTable: "Watchlists",
                principalColumn: "Id");
        }
    }
}
