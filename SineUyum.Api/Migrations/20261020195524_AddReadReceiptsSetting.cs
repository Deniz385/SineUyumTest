using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReadReceiptsSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowReadReceipts",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                // Existing accounts start with the current behaviour (read receipts on).
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowReadReceipts",
                table: "AspNetUsers");
        }
    }
}
