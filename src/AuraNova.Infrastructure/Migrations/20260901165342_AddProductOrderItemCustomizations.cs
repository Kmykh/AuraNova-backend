using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductOrderItemCustomizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowsButterfly",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsLights",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsPhraseCard",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AvailableColors",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AvailableFlowerTypes",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasButterfly",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasLights",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasPhraseCard",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PhraseFont",
                table: "OrderItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhraseText",
                table: "OrderItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedFlowerColor",
                table: "OrderItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedFlowerType",
                table: "OrderItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedPrimaryColor",
                table: "OrderItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedSecondaryColor",
                table: "OrderItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowsButterfly",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AllowsLights",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AllowsPhraseCard",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AvailableColors",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AvailableFlowerTypes",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "HasButterfly",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "HasLights",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "HasPhraseCard",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PhraseFont",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PhraseText",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SelectedFlowerColor",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SelectedFlowerType",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SelectedPrimaryColor",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SelectedSecondaryColor",
                table: "OrderItems");
        }
    }
}
