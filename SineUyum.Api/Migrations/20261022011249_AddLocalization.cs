using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MessageArgs",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageKey",
                table: "Notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorArgs",
                table: "MatchingRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorKey",
                table: "MatchingRuns",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "AspNetUsers",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                // Existing accounts used the Turkish-only app, so they start in Turkish.
                defaultValue: "tr");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageArgs",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "MessageKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ErrorArgs",
                table: "MatchingRuns");

            migrationBuilder.DropColumn(
                name: "ErrorKey",
                table: "MatchingRuns");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "AspNetUsers");
        }
    }
}
