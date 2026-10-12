using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCodeSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "OrderCodeSequence");

            // Continue after the highest existing code (PED-000123 -> next is 124)
            migrationBuilder.Sql("""
                SELECT setval('"OrderCodeSequence"',
                    COALESCE((SELECT MAX(CAST(SUBSTRING("OrderCode" FROM 5) AS bigint))
                              FROM "Orders"
                              WHERE "OrderCode" ~ '^PED-[0-9]+$'), 0) + 1,
                    false);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "OrderCodeSequence");
        }
    }
}
