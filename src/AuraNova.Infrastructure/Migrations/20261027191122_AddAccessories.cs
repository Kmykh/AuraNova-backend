using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailableAccessories",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.AddColumn<string>(
                name: "Accessories",
                table: "OrderItems",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.AddColumn<string>(
                name: "AccessoryOptions",
                table: "BusinessSettings",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'");

            // Los productos que ofrecían luces o mariposas (sí/no) pasan a la lista de accesorios
            migrationBuilder.Sql("""
                UPDATE "Products" SET "AvailableAccessories" = CASE
                    WHEN "AllowsLights" AND "AllowsButterfly" THEN '["luces","mariposas"]'
                    WHEN "AllowsLights" THEN '["luces"]'
                    WHEN "AllowsButterfly" THEN '["mariposas"]'
                    ELSE '[]'
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableAccessories",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Accessories",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "AccessoryOptions",
                table: "BusinessSettings");
        }
    }
}
