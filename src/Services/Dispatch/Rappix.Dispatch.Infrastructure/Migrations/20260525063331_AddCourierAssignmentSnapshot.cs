using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rappix.Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourierAssignmentSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "snapshot_customer_user_id",
                schema: "dispatch",
                table: "courier_assignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<double>(
                name: "snapshot_delivery_lat",
                schema: "dispatch",
                table: "courier_assignments",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "snapshot_delivery_lng",
                schema: "dispatch",
                table: "courier_assignments",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "snapshot_delivery_reference",
                schema: "dispatch",
                table: "courier_assignments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "snapshot_delivery_street",
                schema: "dispatch",
                table: "courier_assignments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            // Default '[]' (no '') porque la columna es jsonb: PostgreSQL valida que el literal sea JSON
            // valido. '' es valido para varchar pero no para jsonb. Filas pre-13.6 reciben lista vacia.
            migrationBuilder.AddColumn<string>(
                name: "snapshot_lines",
                schema: "dispatch",
                table: "courier_assignments",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "snapshot_merchant_name",
                schema: "dispatch",
                table: "courier_assignments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "snapshot_order_currency",
                schema: "dispatch",
                table: "courier_assignments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "snapshot_order_total",
                schema: "dispatch",
                table: "courier_assignments",
                type: "numeric(19,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "snapshot_pickup_lat",
                schema: "dispatch",
                table: "courier_assignments",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "snapshot_pickup_lng",
                schema: "dispatch",
                table: "courier_assignments",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "snapshot_customer_user_id",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropColumn(
                name: "snapshot_delivery_lat",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropColumn(
                name: "snapshot_delivery_lng",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropColumn(
                name: "snapshot_delivery_reference",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropColumn(
                name: "snapshot_delivery_street",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropColumn(
                name: "snapshot_lines",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropColumn(
                name: "snapshot_merchant_name",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropColumn(
                name: "snapshot_order_currency",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropColumn(
                name: "snapshot_order_total",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropColumn(
                name: "snapshot_pickup_lat",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropColumn(
                name: "snapshot_pickup_lng",
                schema: "dispatch",
                table: "courier_assignments");
        }
    }
}
