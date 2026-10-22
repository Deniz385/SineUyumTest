using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedWatchlists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_WatchlistId",
                table: "WatchlistItems");

            migrationBuilder.AddColumn<string>(
                name: "AddedByUserId",
                table: "WatchlistItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WatchlistMembers",
                columns: table => new
                {
                    WatchlistId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Accepted = table.Column<bool>(type: "boolean", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistMembers", x => new { x.WatchlistId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WatchlistMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchlistMembers_Watchlists_WatchlistId",
                        column: x => x.WatchlistId,
                        principalTable: "Watchlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_AddedByUserId",
                table: "WatchlistItems",
                column: "AddedByUserId");

            // Before the unique index: the "once per list" rule was only checked in
            // code; among possible duplicates the oldest row is kept.
            migrationBuilder.Sql("""
                DELETE FROM "WatchlistItems" i
                USING "WatchlistItems" older
                WHERE i."WatchlistId" = older."WatchlistId"
                  AND i."MovieId" = older."MovieId"
                  AND i."Id" > older."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_WatchlistId_MovieId",
                table: "WatchlistItems",
                columns: new[] { "WatchlistId", "MovieId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistMembers_UserId_Accepted",
                table: "WatchlistMembers",
                columns: new[] { "UserId", "Accepted" });

            migrationBuilder.AddForeignKey(
                name: "FK_WatchlistItems_AspNetUsers_AddedByUserId",
                table: "WatchlistItems",
                column: "AddedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WatchlistItems_AspNetUsers_AddedByUserId",
                table: "WatchlistItems");

            migrationBuilder.DropTable(
                name: "WatchlistMembers");

            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_AddedByUserId",
                table: "WatchlistItems");

            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_WatchlistId_MovieId",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "AddedByUserId",
                table: "WatchlistItems");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_WatchlistId",
                table: "WatchlistItems",
                column: "WatchlistId");
        }
    }
}
