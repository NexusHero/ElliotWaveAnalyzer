using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth.Migrations
{
    /// <inheritdoc />
    internal partial class SavedDepotHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavedDepots_UserId",
                table: "SavedDepots");

            migrationBuilder.CreateIndex(
                name: "IX_SavedDepots_UserId_ImportedAt",
                table: "SavedDepots",
                columns: new[] { "UserId", "ImportedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavedDepots_UserId_ImportedAt",
                table: "SavedDepots");

            migrationBuilder.CreateIndex(
                name: "IX_SavedDepots_UserId",
                table: "SavedDepots",
                column: "UserId",
                unique: true);
        }
    }
}
