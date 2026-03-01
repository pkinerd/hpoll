using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hpoll.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHubDeactivatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "Hubs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "Hubs");
        }
    }
}
