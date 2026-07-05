using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth.Migrations
{
    /// <inheritdoc />
    internal partial class AddSavedDepots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedDepots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric", nullable: true),
                    GainAbsolute = table.Column<decimal>(type: "numeric", nullable: true),
                    GainRelativePercent = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedDepots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedDepotPosition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedDepotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Wkn = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    CostValue = table.Column<decimal>(type: "numeric", nullable: true),
                    MarketPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    MarketValue = table.Column<decimal>(type: "numeric", nullable: true),
                    GainAbsolute = table.Column<decimal>(type: "numeric", nullable: true),
                    GainRelativePercent = table.Column<decimal>(type: "numeric", nullable: true),
                    Exchange = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedDepotPosition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedDepotPosition_SavedDepots_SavedDepotId",
                        column: x => x.SavedDepotId,
                        principalTable: "SavedDepots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedDepotPosition_SavedDepotId",
                table: "SavedDepotPosition",
                column: "SavedDepotId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedDepots_UserId",
                table: "SavedDepots",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedDepotPosition");

            migrationBuilder.DropTable(
                name: "SavedDepots");
        }
    }
}
