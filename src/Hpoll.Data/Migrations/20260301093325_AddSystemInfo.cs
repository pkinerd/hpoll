using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hpoll.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemInfo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemInfo", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemInfo_Category",
                table: "SystemInfo",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemInfo");
        }
    }
}
