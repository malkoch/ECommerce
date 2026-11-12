using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stock.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StockUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_units", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_units_available",
                table: "stock_units",
                column: "product_id",
                filter: "reservation_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stock_units_reservation_id",
                table: "stock_units",
                column: "reservation_id");

            // Mevcut miktar kadar müsait birim satırı üretilir.
            migrationBuilder.Sql(
                """
                INSERT INTO stock_units (id, product_id, reservation_id)
                SELECT gen_random_uuid(), s.product_id, NULL
                FROM stock_items s, generate_series(1, s.available_quantity);
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_items_available_non_negative",
                table: "stock_items");

            migrationBuilder.DropColumn(
                name: "available_quantity",
                table: "stock_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "available_quantity",
                table: "stock_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE stock_items s
                SET available_quantity = (SELECT count(*) FROM stock_units u WHERE u.product_id = s.product_id AND u.reservation_id IS NULL);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_items_available_non_negative",
                table: "stock_items",
                sql: "available_quantity >= 0");

            migrationBuilder.DropTable(
                name: "stock_units");
        }
    }
}
