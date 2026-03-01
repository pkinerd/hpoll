using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hpoll.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerSendTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SendTimesLocal",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextSendTimeUtc",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_NextSendTimeUtc",
                table: "Customers",
                column: "NextSendTimeUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_NextSendTimeUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SendTimesLocal",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "NextSendTimeUtc",
                table: "Customers");
        }
    }
}
