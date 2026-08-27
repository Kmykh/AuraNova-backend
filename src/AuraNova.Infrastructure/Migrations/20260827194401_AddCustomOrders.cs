using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CustomizationCost",
                table: "Quotes",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomizationCost",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CustomizationNotes",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomOrder",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceImageUrl",
                table: "Orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomizationCost",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CustomizationCost",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomizationNotes",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsCustomOrder",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReferenceImageUrl",
                table: "Orders");
        }
    }
}
