using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SineUyum.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeGroupSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventGroupSuggestions",
                columns: table => new
                {
                    EventGroupId = table.Column<int>(type: "integer", nullable: false),
                    MovieId = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventGroupSuggestions", x => new { x.EventGroupId, x.MovieId });
                    table.ForeignKey(
                        name: "FK_EventGroupSuggestions_EventGroups_EventGroupId",
                        column: x => x.EventGroupId,
                        principalTable: "EventGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventGroupSuggestions_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventGroupSuggestions_MovieId",
                table: "EventGroupSuggestions",
                column: "MovieId");

            // Mevcut "1,2,3" değerlerini sırasıyla taşı. Sayı olmayan parçalar
            // (eskiden ekranı 500'e düşüren bozuk veri), Movies'te olmayan
            // filmler ve tekrarlar atlanır. Sütun ancak taşımadan sonra silinir.
            migrationBuilder.Sql("""
                INSERT INTO "EventGroupSuggestions" ("EventGroupId", "MovieId", "Rank")
                SELECT g."Id", trim(t.part)::int, MIN(t.ord) - 1
                FROM "EventGroups" g
                CROSS JOIN LATERAL unnest(string_to_array(g."SuggestedMovieIds", ',')) WITH ORDINALITY AS t(part, ord)
                WHERE trim(t.part) ~ '^[0-9]{1,9}$'
                  AND EXISTS (SELECT 1 FROM "Movies" m WHERE m."Id" = trim(t.part)::int)
                GROUP BY g."Id", trim(t.part)::int;
                """);

            migrationBuilder.DropColumn(
                name: "SuggestedMovieIds",
                table: "EventGroups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuggestedMovieIds",
                table: "EventGroups",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "EventGroups" g
                SET "SuggestedMovieIds" = (
                    SELECT string_agg(s."MovieId"::text, ',' ORDER BY s."Rank")
                    FROM "EventGroupSuggestions" s WHERE s."EventGroupId" = g."Id");
                """);

            migrationBuilder.DropTable(
                name: "EventGroupSuggestions");
        }
    }
}
