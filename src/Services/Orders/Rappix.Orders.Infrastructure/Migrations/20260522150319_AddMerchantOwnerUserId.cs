using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rappix.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantOwnerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_MerchantId_Status",
                schema: "orders",
                table: "orders");

            migrationBuilder.AddColumn<Guid>(
                name: "MerchantOwnerUserId",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_orders_MerchantOwnerUserId_Status",
                schema: "orders",
                table: "orders",
                columns: new[] { "MerchantOwnerUserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_MerchantOwnerUserId_Status",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "MerchantOwnerUserId",
                schema: "orders",
                table: "orders");

            migrationBuilder.CreateIndex(
                name: "IX_orders_MerchantId_Status",
                schema: "orders",
                table: "orders",
                columns: new[] { "MerchantId", "Status" });
        }
    }
}
