using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "PaymentTransactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "PaymentTransactions");
        }
    }
}
