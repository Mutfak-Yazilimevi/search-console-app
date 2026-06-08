using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchConsoleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKeywordWatchAndExternalBacklinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExternalBacklinkCount",
                table: "BacklinkSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalReferringDomainCount",
                table: "BacklinkSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSource",
                table: "BacklinkSummary",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalTopDomainsJson",
                table: "BacklinkSummary",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KeywordSerpSnapshot",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    Keyword = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    MatchedUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeywordSerpSnapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteKeywordWatch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    SiteHost = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Keyword = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteKeywordWatch", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KeywordSerpSnapshot_AuditRunId",
                table: "KeywordSerpSnapshot",
                column: "AuditRunId");

            migrationBuilder.CreateIndex(
                name: "IX_KeywordSerpSnapshot_EntityId",
                table: "KeywordSerpSnapshot",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteKeywordWatch_CustomerId",
                table: "SiteKeywordWatch",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteKeywordWatch_CustomerId_SiteHost_Keyword",
                table: "SiteKeywordWatch",
                columns: new[] { "CustomerId", "SiteHost", "Keyword" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteKeywordWatch_EntityId",
                table: "SiteKeywordWatch",
                column: "EntityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KeywordSerpSnapshot");

            migrationBuilder.DropTable(
                name: "SiteKeywordWatch");

            migrationBuilder.DropColumn(
                name: "ExternalBacklinkCount",
                table: "BacklinkSummary");

            migrationBuilder.DropColumn(
                name: "ExternalReferringDomainCount",
                table: "BacklinkSummary");

            migrationBuilder.DropColumn(
                name: "ExternalSource",
                table: "BacklinkSummary");

            migrationBuilder.DropColumn(
                name: "ExternalTopDomainsJson",
                table: "BacklinkSummary");
        }
    }
}
