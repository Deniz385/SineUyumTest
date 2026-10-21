using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTasteTwins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasteTwinSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SuggestedUserId = table.Column<string>(type: "text", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Compatibility = table.Column<double>(type: "double precision", nullable: false),
                    CommonMovieCount = table.Column<int>(type: "integer", nullable: false),
                    Notified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasteTwinSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasteTwinSuggestions_AspNetUsers_SuggestedUserId",
                        column: x => x.SuggestedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TasteTwinSuggestions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasteTwinSuggestions_SuggestedUserId",
                table: "TasteTwinSuggestions",
                column: "SuggestedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TasteTwinSuggestions_UserId_SuggestedUserId",
                table: "TasteTwinSuggestions",
                columns: new[] { "UserId", "SuggestedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TasteTwinSuggestions_UserId_WeekStart",
                table: "TasteTwinSuggestions",
                columns: new[] { "UserId", "WeekStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasteTwinSuggestions");
        }
    }
}
