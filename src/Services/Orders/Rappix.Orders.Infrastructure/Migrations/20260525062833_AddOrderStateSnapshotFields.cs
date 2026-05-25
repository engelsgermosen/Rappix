using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rappix.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderStateSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryReference",
                schema: "orders",
                table: "order_states",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStreet",
                schema: "orders",
                table: "order_states",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinesJson",
                schema: "orders",
                table: "order_states",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchantName",
                schema: "orders",
                table: "order_states",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryReference",
                schema: "orders",
                table: "order_states");

            migrationBuilder.DropColumn(
                name: "DeliveryStreet",
                schema: "orders",
                table: "order_states");

            migrationBuilder.DropColumn(
                name: "LinesJson",
                schema: "orders",
                table: "order_states");

            migrationBuilder.DropColumn(
                name: "MerchantName",
                schema: "orders",
                table: "order_states");
        }
    }
}
