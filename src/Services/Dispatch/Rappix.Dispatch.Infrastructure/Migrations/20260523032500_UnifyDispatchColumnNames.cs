using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // arrays constantes en metodos generados — no se reutilizan

namespace Rappix.Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifyDispatchColumnNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_courier_assignments_active_courier",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropIndex(
                name: "ux_courier_assignments_active_order",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "dispatch",
                table: "courier_profiles",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "released_at_utc",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "ReleasedAtUtc");

            migrationBuilder.RenameColumn(
                name: "release_reason",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "ReleaseReason");

            migrationBuilder.RenameColumn(
                name: "order_id",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "OrderId");

            migrationBuilder.RenameColumn(
                name: "courier_id",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "CourierId");

            migrationBuilder.RenameColumn(
                name: "assigned_at_utc",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "AssignedAtUtc");

            migrationBuilder.CreateIndex(
                name: "ux_courier_assignments_active_courier",
                schema: "dispatch",
                table: "courier_assignments",
                column: "CourierId",
                unique: true,
                filter: "\"ReleasedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_courier_assignments_active_order",
                schema: "dispatch",
                table: "courier_assignments",
                column: "OrderId",
                unique: true,
                filter: "\"ReleasedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_courier_assignments_active_courier",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.DropIndex(
                name: "ux_courier_assignments_active_order",
                schema: "dispatch",
                table: "courier_assignments");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "dispatch",
                table: "courier_profiles",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ReleasedAtUtc",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "released_at_utc");

            migrationBuilder.RenameColumn(
                name: "ReleaseReason",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "release_reason");

            migrationBuilder.RenameColumn(
                name: "OrderId",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "order_id");

            migrationBuilder.RenameColumn(
                name: "CourierId",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "courier_id");

            migrationBuilder.RenameColumn(
                name: "AssignedAtUtc",
                schema: "dispatch",
                table: "courier_assignments",
                newName: "assigned_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_courier_assignments_active_courier",
                schema: "dispatch",
                table: "courier_assignments",
                column: "courier_id",
                unique: true,
                filter: "\"released_at_utc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_courier_assignments_active_order",
                schema: "dispatch",
                table: "courier_assignments",
                column: "order_id",
                unique: true,
                filter: "\"released_at_utc\" IS NULL");
        }
    }
}
