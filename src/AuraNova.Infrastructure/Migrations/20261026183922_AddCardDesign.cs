using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCardDesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardBackgroundPath",
                table: "OrderItems",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhraseFrom",
                table: "OrderItems",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhraseTheme",
                table: "OrderItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhraseTo",
                table: "OrderItems",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardBackgroundPath",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PhraseFrom",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PhraseTheme",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PhraseTo",
                table: "OrderItems");
        }
    }
}
