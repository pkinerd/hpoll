using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hpoll.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerEmailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeLatestLocations",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SummaryWindowCount",
                table: "Customers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SummaryWindowHours",
                table: "Customers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SummaryWindowOffsetHours",
                table: "Customers",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeLatestLocations",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SummaryWindowCount",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SummaryWindowHours",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SummaryWindowOffsetHours",
                table: "Customers");
        }
    }
}
