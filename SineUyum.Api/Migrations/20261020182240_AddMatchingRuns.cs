using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchingRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchingRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CinemaEventId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GroupCount = table.Column<int>(type: "integer", nullable: true),
                    UngroupedCount = table.Column<int>(type: "integer", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchingRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchingRuns_CinemaEvents_CinemaEventId",
                        column: x => x.CinemaEventId,
                        principalTable: "CinemaEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchingRuns_CinemaEventId_CreatedAt",
                table: "MatchingRuns",
                columns: new[] { "CinemaEventId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchingRuns_Status_CreatedAt",
                table: "MatchingRuns",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchingRuns");
        }
    }
}
