using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth.Migrations
{
    /// <inheritdoc />
    internal partial class AddAnalysisSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Structure = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Bullish = table.Column<bool>(type: "boolean", nullable: false),
                    InvalidationPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    InvalidationAbove = table.Column<bool>(type: "boolean", nullable: false),
                    TargetLow = table.Column<decimal>(type: "numeric", nullable: true),
                    TargetHigh = table.Column<decimal>(type: "numeric", nullable: true),
                    Confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisSnapshots_UserId_CreatedAt",
                table: "AnalysisSnapshots",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisSnapshots");
        }
    }
}
