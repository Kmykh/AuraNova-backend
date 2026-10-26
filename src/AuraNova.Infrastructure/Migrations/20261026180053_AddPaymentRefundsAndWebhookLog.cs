using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRefundsAndWebhookLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "PaymentTransactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RefundedAt",
                table: "PaymentTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Topic = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ResourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Detail = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Status_CreatedAt",
                table: "PaymentTransactions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_ReceivedAt",
                table: "PaymentWebhookEvents",
                column: "ReceivedAt");

            // Seguridad en Supabase: con RLS activo y sin políticas, la API pública de Supabase (anon/authenticated)
            // no puede leer ni escribir las tablas. El backend se conecta como dueño de las tablas y no se ve afectado.
            // Cada tabla va en su propio bloque: si una no se puede alterar, la migración sigue.
            migrationBuilder.Sql("""
                DO $$
                DECLARE r record;
                BEGIN
                    FOR r IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' LOOP
                        BEGIN
                            EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', r.tablename);
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'RLS no aplicado en %: %', r.tablename, SQLERRM;
                        END;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_Status_CreatedAt",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "PaymentTransactions");
        }
    }
}
