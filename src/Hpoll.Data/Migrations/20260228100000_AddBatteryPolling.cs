using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hpoll.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatteryPolling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastBatteryPollUtc",
                table: "Hubs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastBatteryPollUtc",
                table: "Hubs");
        }
    }
}
