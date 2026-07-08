using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth.Migrations
{
    /// <inheritdoc />
    internal partial class AddWorkspaceDraftsAndWatchlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchlistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchlistEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Interval = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    AnnotationsJson = table.Column<string>(type: "text", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceDrafts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistEntries_UserId_SortOrder",
                table: "WatchlistEntries",
                columns: new[] { "UserId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistEntries_UserId_Symbol",
                table: "WatchlistEntries",
                columns: new[] { "UserId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceDrafts_UserId_Symbol_Interval",
                table: "WorkspaceDrafts",
                columns: new[] { "UserId", "Symbol", "Interval" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceDrafts_UserId_UpdatedAt",
                table: "WorkspaceDrafts",
                columns: new[] { "UserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchlistEntries");

            migrationBuilder.DropTable(
                name: "WorkspaceDrafts");
        }
    }
}
