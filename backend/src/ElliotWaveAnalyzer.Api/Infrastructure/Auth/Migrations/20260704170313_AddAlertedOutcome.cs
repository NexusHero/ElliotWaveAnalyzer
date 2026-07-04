using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth.Migrations
{
    /// <inheritdoc />
    internal partial class AddAlertedOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlertedOutcome",
                table: "AnalysisSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertedOutcome",
                table: "AnalysisSnapshots");
        }
    }
}
