using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMaxChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxChoices",
                table: "Products",
                type: "integer",
                nullable: true);

            // Lo que ya está en "Flores por unidad" es una sola flor: un solo color y un tipo de flor
            migrationBuilder.Sql("""
                UPDATE "Products" SET "MaxChoices" = 1
                WHERE "CategoryId" IN (SELECT "Id" FROM "Categories" WHERE lower("Name") LIKE '%por unidad%');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxChoices",
                table: "Products");
        }
    }
}
