using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomOrderBrief : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BudgetRange",
                table: "Orders",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DesiredDate",
                table: "Orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occasion",
                table: "Orders",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceLinks",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.CreateTable(
                name: "OrderReferenceImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReferenceImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderReferenceImages_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderReferenceImages_OrderId_SortOrder",
                table: "OrderReferenceImages",
                columns: new[] { "OrderId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderReferenceImages");

            migrationBuilder.DropColumn(
                name: "BudgetRange",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DesiredDate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Occasion",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReferenceLinks",
                table: "Orders");
        }
    }
}
