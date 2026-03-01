using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hpoll.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPollingLogTimestampIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PollingLogs_Timestamp",
                table: "PollingLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PollingLogs_Timestamp",
                table: "PollingLogs");
        }
    }
}
