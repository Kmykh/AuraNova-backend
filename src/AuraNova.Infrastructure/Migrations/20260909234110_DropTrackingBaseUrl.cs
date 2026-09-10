using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropTrackingBaseUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrackingBaseUrl",
                table: "BusinessSettings");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 9, 23, 41, 10, 340, DateTimeKind.Unspecified).AddTicks(6790), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 9, 23, 41, 10, 340, DateTimeKind.Unspecified).AddTicks(6800), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 9, 23, 41, 10, 340, DateTimeKind.Unspecified).AddTicks(6800), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingBaseUrl",
                table: "BusinessSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 9, 21, 35, 3, 212, DateTimeKind.Unspecified).AddTicks(6340), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 9, 21, 35, 3, 212, DateTimeKind.Unspecified).AddTicks(6350), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 9, 21, 35, 3, 212, DateTimeKind.Unspecified).AddTicks(6350), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
