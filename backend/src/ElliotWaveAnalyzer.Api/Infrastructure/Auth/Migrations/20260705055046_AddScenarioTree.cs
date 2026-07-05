using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth.Migrations
{
    /// <inheritdoc />
    internal partial class AddScenarioTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EntryHigh",
                table: "AnalysisSnapshots",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EntryLow",
                table: "AnalysisSnapshots",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EntryZoneAlerted",
                table: "AnalysisSnapshots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AnalysisScenarioRow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Structure = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Bullish = table.Column<bool>(type: "boolean", nullable: false),
                    InvalidationPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    InvalidationAbove = table.Column<bool>(type: "boolean", nullable: false),
                    EntryLow = table.Column<decimal>(type: "numeric", nullable: true),
                    EntryHigh = table.Column<decimal>(type: "numeric", nullable: true),
                    TargetLow = table.Column<decimal>(type: "numeric", nullable: true),
                    TargetHigh = table.Column<decimal>(type: "numeric", nullable: true),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: true),
                    Retired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisScenarioRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisScenarioRow_AnalysisSnapshots_AnalysisSnapshotId",
                        column: x => x.AnalysisSnapshotId,
                        principalTable: "AnalysisSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisSwitchEventRow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FromLabel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToLabel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisSwitchEventRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisSwitchEventRow_AnalysisSnapshots_AnalysisSnapshotId",
                        column: x => x.AnalysisSnapshotId,
                        principalTable: "AnalysisSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisScenarioRow_AnalysisSnapshotId",
                table: "AnalysisScenarioRow",
                column: "AnalysisSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisSwitchEventRow_AnalysisSnapshotId",
                table: "AnalysisSwitchEventRow",
                column: "AnalysisSnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisScenarioRow");

            migrationBuilder.DropTable(
                name: "AnalysisSwitchEventRow");

            migrationBuilder.DropColumn(
                name: "EntryHigh",
                table: "AnalysisSnapshots");

            migrationBuilder.DropColumn(
                name: "EntryLow",
                table: "AnalysisSnapshots");

            migrationBuilder.DropColumn(
                name: "EntryZoneAlerted",
                table: "AnalysisSnapshots");
        }
    }
}
