using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth.Migrations
{
    /// <inheritdoc />
    internal partial class AddBacktestRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacktestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EngineVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Config = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ScenarioCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BacktestBucketRow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BacktestRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dimension = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    Concluded = table.Column<int>(type: "integer", nullable: false),
                    TargetReached = table.Column<int>(type: "integer", nullable: false),
                    Invalidated = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestBucketRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacktestBucketRow_BacktestRuns_BacktestRunId",
                        column: x => x.BacktestRunId,
                        principalTable: "BacktestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacktestBucketRow_BacktestRunId",
                table: "BacktestBucketRow",
                column: "BacktestRunId");

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_DatasetHash",
                table: "BacktestRuns",
                column: "DatasetHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacktestBucketRow");

            migrationBuilder.DropTable(
                name: "BacktestRuns");
        }
    }
}
