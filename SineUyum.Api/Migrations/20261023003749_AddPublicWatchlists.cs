using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicWatchlists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Watchlists",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            // Existing lists: the date of their first movie is the best guess.
            migrationBuilder.Sql(
                @"UPDATE ""Watchlists"" AS w
                  SET ""CreatedAt"" = i.""FirstAddedAt""
                  FROM (SELECT ""WatchlistId"", MIN(""AddedAt"") AS ""FirstAddedAt"" FROM ""WatchlistItems"" GROUP BY ""WatchlistId"") AS i
                  WHERE i.""WatchlistId"" = w.""Id"";");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Watchlists",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "Watchlists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WatchlistLikes",
                columns: table => new
                {
                    WatchlistId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistLikes", x => new { x.WatchlistId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WatchlistLikes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchlistLikes_Watchlists_WatchlistId",
                        column: x => x.WatchlistId,
                        principalTable: "Watchlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchlistSaves",
                columns: table => new
                {
                    WatchlistId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistSaves", x => new { x.WatchlistId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WatchlistSaves_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchlistSaves_Watchlists_WatchlistId",
                        column: x => x.WatchlistId,
                        principalTable: "Watchlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_Visibility_PublishedAt",
                table: "Watchlists",
                columns: new[] { "Visibility", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistLikes_CreatedAt",
                table: "WatchlistLikes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistLikes_UserId",
                table: "WatchlistLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistSaves_CreatedAt",
                table: "WatchlistSaves",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistSaves_UserId_CreatedAt",
                table: "WatchlistSaves",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchlistLikes");

            migrationBuilder.DropTable(
                name: "WatchlistSaves");

            migrationBuilder.DropIndex(
                name: "IX_Watchlists_Visibility_PublishedAt",
                table: "Watchlists");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Watchlists");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Watchlists");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Watchlists");
        }
    }
}
