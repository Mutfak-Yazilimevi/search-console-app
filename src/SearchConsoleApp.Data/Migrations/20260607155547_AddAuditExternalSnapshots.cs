using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchConsoleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditExternalSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProgressMessage",
                table: "AuditRun",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressPhase",
                table: "AuditRun",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BacklinkSummary",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    InternalLinkCount = table.Column<int>(type: "int", nullable: false),
                    UniqueInternalTargets = table.Column<int>(type: "int", nullable: false),
                    OrphanPageCount = table.Column<int>(type: "int", nullable: false),
                    TopLinkedPagesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacklinkSummary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IndexStatusSnapshot",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EstimatedIndexedPages = table.Column<int>(type: "int", nullable: false),
                    CrawledPages = table.Column<int>(type: "int", nullable: false),
                    CoverageRatio = table.Column<double>(type: "float", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexStatusSnapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PageSpeedResult",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    PerformanceScore = table.Column<int>(type: "int", nullable: false),
                    Lcp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Inp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Cls = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Strategy = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DiagnosticsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageSpeedResult", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacklinkSummary_AuditRunId",
                table: "BacklinkSummary",
                column: "AuditRunId");

            migrationBuilder.CreateIndex(
                name: "IX_BacklinkSummary_EntityId",
                table: "BacklinkSummary",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndexStatusSnapshot_AuditRunId",
                table: "IndexStatusSnapshot",
                column: "AuditRunId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexStatusSnapshot_EntityId",
                table: "IndexStatusSnapshot",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PageSpeedResult_AuditRunId",
                table: "PageSpeedResult",
                column: "AuditRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PageSpeedResult_EntityId",
                table: "PageSpeedResult",
                column: "EntityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacklinkSummary");

            migrationBuilder.DropTable(
                name: "IndexStatusSnapshot");

            migrationBuilder.DropTable(
                name: "PageSpeedResult");

            migrationBuilder.DropColumn(
                name: "ProgressMessage",
                table: "AuditRun");

            migrationBuilder.DropColumn(
                name: "ProgressPhase",
                table: "AuditRun");
        }
    }
}
