using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rappix.Pricing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteRevertAndRedemptionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConsumedByOrderId",
                schema: "pricing",
                table: "quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevertedAtUtc",
                schema: "pricing",
                table: "quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevertReason",
                schema: "pricing",
                table: "coupon_redemptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevertedAtUtc",
                schema: "pricing",
                table: "coupon_redemptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_ConsumedByOrderId",
                schema: "pricing",
                table: "quotes",
                column: "ConsumedByOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_coupon_redemptions_QuoteId",
                schema: "pricing",
                table: "coupon_redemptions",
                column: "QuoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_quotes_ConsumedByOrderId",
                schema: "pricing",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "IX_coupon_redemptions_QuoteId",
                schema: "pricing",
                table: "coupon_redemptions");

            migrationBuilder.DropColumn(
                name: "ConsumedByOrderId",
                schema: "pricing",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "RevertedAtUtc",
                schema: "pricing",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "RevertReason",
                schema: "pricing",
                table: "coupon_redemptions");

            migrationBuilder.DropColumn(
                name: "RevertedAtUtc",
                schema: "pricing",
                table: "coupon_redemptions");
        }
    }
}
