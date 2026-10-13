using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductGalleryAndItemOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedColors",
                table: "OrderItems",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.AddColumn<string>(
                name: "SelectedFlowerTypes",
                table: "OrderItems",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.AddColumn<string>(
                name: "ColorPalette",
                table: "BusinessSettings",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.AddColumn<string>(
                name: "FlowerTypeOptions",
                table: "BusinessSettings",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_Path",
                table: "ProductImages",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId_SortOrder",
                table: "ProductImages",
                columns: new[] { "ProductId", "SortOrder" });

            // La imagen actual de cada producto pasa a ser la portada de su galería
            migrationBuilder.Sql("""
                INSERT INTO "ProductImages" ("Id", "ProductId", "Path", "SortOrder", "CreatedAt")
                SELECT gen_random_uuid(), "Id", "ImageUrl", 0, now()
                FROM "Products"
                WHERE "ImageUrl" IS NOT NULL AND "ImageUrl" <> '';
                """);

            // Pedidos existentes: los colores y flores que se guardaban con comas pasan a ser listas
            migrationBuilder.Sql("""
                UPDATE "OrderItems" SET "SelectedColors" = COALESCE((
                    SELECT json_agg(CASE WHEN trim(v) = 'Surtido (Varios colores)' THEN 'Surtido multicolor' ELSE trim(v) END ORDER BY n)::text
                    FROM unnest(string_to_array(concat_ws(',', "SelectedPrimaryColor", "SelectedSecondaryColor"), ',')) WITH ORDINALITY AS t(v, n)
                    WHERE trim(v) <> ''), '[]')
                WHERE "SelectedPrimaryColor" IS NOT NULL OR "SelectedSecondaryColor" IS NOT NULL;

                UPDATE "OrderItems" SET "SelectedFlowerTypes" = COALESCE((
                    SELECT json_agg(trim(v) ORDER BY n)::text
                    FROM unnest(string_to_array(concat_ws(',', "SelectedFlowerType", "SelectedFlowerColor"), ',')) WITH ORDINALITY AS t(v, n)
                    WHERE trim(v) <> ''), '[]')
                WHERE "SelectedFlowerType" IS NOT NULL OR "SelectedFlowerColor" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropColumn(
                name: "SelectedColors",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SelectedFlowerTypes",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ColorPalette",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "FlowerTypeOptions",
                table: "BusinessSettings");
        }
    }
}
