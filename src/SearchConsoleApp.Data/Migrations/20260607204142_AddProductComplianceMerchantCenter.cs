using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchConsoleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductComplianceMerchantCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MerchantCenterOAuthToken",
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
                    table.PrimaryKey("PK_MerchantCenterOAuthToken", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductComplianceIssue",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    PageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RuleId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Field = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    FixHint = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    DocUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    GmcIssueCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Evidence = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductComplianceIssue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductComplianceItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    PageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OfferId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GmcStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ComplianceScore = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExtractedDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssueCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductComplianceItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductComplianceRun",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InputUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    NormalizedUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AnalysisMode = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    MerchantCenterAccountId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalProducts = table.Column<int>(type: "int", nullable: false),
                    CompliantCount = table.Column<int>(type: "int", nullable: false),
                    PartialCount = table.Column<int>(type: "int", nullable: false),
                    NonCompliantCount = table.Column<int>(type: "int", nullable: false),
                    ComplianceScore = table.Column<int>(type: "int", nullable: true),
                    SiteReadinessScore = table.Column<int>(type: "int", nullable: true),
                    CriticalCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    InfoCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ProgressPhase = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProgressMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    GmcSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SiteCheckHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriorityActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductComplianceRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantCenterOAuthToken_CustomerId",
                table: "MerchantCenterOAuthToken",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantCenterOAuthToken_EntityId",
                table: "MerchantCenterOAuthToken",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceIssue_EntityId",
                table: "ProductComplianceIssue",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceIssue_ItemId",
                table: "ProductComplianceIssue",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceIssue_RuleId",
                table: "ProductComplianceIssue",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceIssue_RunId",
                table: "ProductComplianceIssue",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceItem_EntityId",
                table: "ProductComplianceItem",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceItem_RunId",
                table: "ProductComplianceItem",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceItem_RunId_PageUrl",
                table: "ProductComplianceItem",
                columns: new[] { "RunId", "PageUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceRun_CreatedAt",
                table: "ProductComplianceRun",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceRun_CustomerId",
                table: "ProductComplianceRun",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceRun_EntityId",
                table: "ProductComplianceRun",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductComplianceRun_NormalizedUrl",
                table: "ProductComplianceRun",
                column: "NormalizedUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantCenterOAuthToken");

            migrationBuilder.DropTable(
                name: "ProductComplianceIssue");

            migrationBuilder.DropTable(
                name: "ProductComplianceItem");

            migrationBuilder.DropTable(
                name: "ProductComplianceRun");
        }
    }
}
