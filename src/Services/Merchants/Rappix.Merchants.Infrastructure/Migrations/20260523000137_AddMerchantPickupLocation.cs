using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable
#pragma warning disable CA1861 // arrays constantes en metodos generados — no se reutilizan

namespace Rappix.Merchants.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantPickupLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Point>(
                name: "PickupLocation",
                schema: "merchants",
                table: "merchants",
                type: "geography(Point,4326)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchants_PickupLocation",
                schema: "merchants",
                table: "merchants",
                column: "PickupLocation")
                .Annotation("Npgsql:IndexMethod", "gist");

            // Backfill para merchants pre-existentes: usa la primera ServiceArea por orden de creacion.
            // Circle: Center (ya es geography). Polygon: ST_Centroid::geography. Solo aplica a merchants
            // sin PickupLocation. Es un valor por defecto — el owner puede sobrescribirlo via
            // PUT /api/v1/merchants/me/pickup-location. Los merchants sin ninguna ServiceArea quedan en NULL
            // y no podran enviar a aprobacion hasta fijarlo (correctness, no scope creep).
            migrationBuilder.Sql(
                """
                UPDATE merchants."merchants" m
                SET "PickupLocation" = sub.pickup
                FROM (
                    SELECT DISTINCT ON ("MerchantId") "MerchantId",
                        CASE
                            WHEN "Type" = 'Circle' THEN "Center"
                            WHEN "Type" = 'Polygon' THEN ST_Centroid("Polygon")::geography
                        END AS pickup
                    FROM merchants.service_areas
                    ORDER BY "MerchantId", "CreatedAtUtc" ASC
                ) sub
                WHERE m."Id" = sub."MerchantId"
                  AND m."PickupLocation" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_merchants_PickupLocation",
                schema: "merchants",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "PickupLocation",
                schema: "merchants",
                table: "merchants");
        }
    }
}
