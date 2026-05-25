using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rappix.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderMerchantName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MerchantName",
                schema: "orders",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MerchantName",
                schema: "orders",
                table: "orders");
        }
    }
}
