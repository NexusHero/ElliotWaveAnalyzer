using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth.Migrations
{
    /// <inheritdoc />
    internal partial class AddAccountDeletionAuditAndUserCascades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountDeletionAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedByIp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDeletionAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionAudits_DeletedUserId",
                table: "AccountDeletionAudits",
                column: "DeletedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AnalysisSnapshots_AspNetUsers_UserId",
                table: "AnalysisSnapshots",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedDepots_AspNetUsers_UserId",
                table: "SavedDepots",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_AspNetUsers_UserId",
                table: "Sessions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserApiKeys_AspNetUsers_UserId",
                table: "UserApiKeys",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserLlmUsagePeriods_AspNetUsers_UserId",
                table: "UserLlmUsagePeriods",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnalysisSnapshots_AspNetUsers_UserId",
                table: "AnalysisSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedDepots_AspNetUsers_UserId",
                table: "SavedDepots");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_AspNetUsers_UserId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserApiKeys_AspNetUsers_UserId",
                table: "UserApiKeys");

            migrationBuilder.DropForeignKey(
                name: "FK_UserLlmUsagePeriods_AspNetUsers_UserId",
                table: "UserLlmUsagePeriods");

            migrationBuilder.DropTable(
                name: "AccountDeletionAudits");
        }
    }
}
