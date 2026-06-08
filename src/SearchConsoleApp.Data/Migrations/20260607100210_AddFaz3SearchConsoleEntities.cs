using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchConsoleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFaz3SearchConsoleEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditRun_Status",
                table: "AuditRun");

            migrationBuilder.AddColumn<long>(
                name: "CustomerId",
                table: "AuditRun",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchConsolePropertyUrl",
                table: "AuditRun",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentQualityScore",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    EeatScore = table.Column<int>(type: "int", nullable: false),
                    ChecklistJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuggestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentQualityScore", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoogleOAuthToken",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AccessTokenExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EncryptedAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleOAuthToken", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchConsoleSnapshot",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    PropertyUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    PerformanceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SitemapsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IndexedPages = table.Column<int>(type: "int", nullable: true),
                    ExcludedPages = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchConsoleSnapshot", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRun_CustomerId",
                table: "AuditRun",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentQualityScore_AuditRunId_Url",
                table: "ContentQualityScore",
                columns: new[] { "AuditRunId", "Url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentQualityScore_EntityId",
                table: "ContentQualityScore",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleOAuthToken_CustomerId",
                table: "GoogleOAuthToken",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleOAuthToken_EntityId",
                table: "GoogleOAuthToken",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchConsoleSnapshot_AuditRunId",
                table: "SearchConsoleSnapshot",
                column: "AuditRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchConsoleSnapshot_EntityId",
                table: "SearchConsoleSnapshot",
                column: "EntityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentQualityScore");

            migrationBuilder.DropTable(
                name: "GoogleOAuthToken");

            migrationBuilder.DropTable(
                name: "SearchConsoleSnapshot");

            migrationBuilder.DropIndex(
                name: "IX_AuditRun_CustomerId",
                table: "AuditRun");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "AuditRun");

            migrationBuilder.DropColumn(
                name: "SearchConsolePropertyUrl",
                table: "AuditRun");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRun_Status",
                table: "AuditRun",
                column: "Status");
        }
    }
}
