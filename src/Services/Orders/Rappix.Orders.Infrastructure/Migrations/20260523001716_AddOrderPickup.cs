using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // arrays constantes en metodos generados — no se reutilizan

namespace Rappix.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPickup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "pickup_latitude",
                schema: "orders",
                table: "orders",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "pickup_longitude",
                schema: "orders",
                table: "orders",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PickupLatitude",
                schema: "orders",
                table: "order_states",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PickupLongitude",
                schema: "orders",
                table: "order_states",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pickup_latitude",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "pickup_longitude",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupLatitude",
                schema: "orders",
                table: "order_states");

            migrationBuilder.DropColumn(
                name: "PickupLongitude",
                schema: "orders",
                table: "order_states");
        }
    }
}
