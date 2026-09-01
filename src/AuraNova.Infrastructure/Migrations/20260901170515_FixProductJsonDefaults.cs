using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixProductJsonDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Products\" SET \"AvailableColors\" = '[]' WHERE \"AvailableColors\" = '';");
            migrationBuilder.Sql("UPDATE \"Products\" SET \"AvailableFlowerTypes\" = '[]' WHERE \"AvailableFlowerTypes\" = '';");

            migrationBuilder.AlterColumn<string>(
                name: "AvailableFlowerTypes",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AvailableColors",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AvailableFlowerTypes",
                table: "Products",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValueSql: "'[]'");

            migrationBuilder.AlterColumn<string>(
                name: "AvailableColors",
                table: "Products",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValueSql: "'[]'");
        }
    }
}
