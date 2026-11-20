using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blog.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "Blogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT b."Id" AS id,
                           ROW_NUMBER() OVER (
                               ORDER BY (
                                   SELECT MAX(COALESCE(p."Published", p."Created"))
                                   FROM "Posts" p
                                   WHERE p."BlogId" = b."Id" AND p."Status" = 2
                               ) DESC NULLS LAST,
                               b."Id"
                           ) AS rn
                    FROM "Blogs" b
                )
                UPDATE "Blogs" AS b
                SET "Position" = ranked.rn
                FROM ranked
                WHERE b."Id" = ranked.id;

                WITH ranked AS (
                    SELECT "Id" AS id,
                           ROW_NUMBER() OVER (
                               PARTITION BY "BlogId"
                               ORDER BY COALESCE("Published", "Created") DESC, "Id"
                           ) AS rn
                    FROM "Posts"
                )
                UPDATE "Posts" AS p
                SET "Position" = ranked.rn
                FROM ranked
                WHERE p."Id" = ranked.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Position",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "Blogs");
        }
    }
}
