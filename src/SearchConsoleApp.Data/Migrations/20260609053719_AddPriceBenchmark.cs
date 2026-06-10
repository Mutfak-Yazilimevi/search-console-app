using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchConsoleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceBenchmark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceBenchmarkItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    PageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OurPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PriceCurrency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    MinMarketPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxMarketPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    WeightedAvgMarketPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MinOfferLink = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    MinOfferSource = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MaxOfferLink = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    MaxOfferSource = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MarketOfferCount = table.Column<int>(type: "int", nullable: false),
                    DeltaPercent = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    MarketPosition = table.Column<int>(type: "int", nullable: false),
                    ShoppingError = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ExtractedDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShoppingOffersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceBenchmarkItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceBenchmarkRun",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InputUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    NormalizedUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalProducts = table.Column<int>(type: "int", nullable: false),
                    SerpApiConfigured = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ProgressPhase = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProgressMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceBenchmarkRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceBenchmarkItem_EntityId",
                table: "PriceBenchmarkItem",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceBenchmarkItem_RunId",
                table: "PriceBenchmarkItem",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceBenchmarkItem_RunId_PageUrl",
                table: "PriceBenchmarkItem",
                columns: new[] { "RunId", "PageUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceBenchmarkRun_CreatedAt",
                table: "PriceBenchmarkRun",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PriceBenchmarkRun_EntityId",
                table: "PriceBenchmarkRun",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceBenchmarkRun_NormalizedUrl",
                table: "PriceBenchmarkRun",
                column: "NormalizedUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceBenchmarkItem");

            migrationBuilder.DropTable(
                name: "PriceBenchmarkRun");
        }
    }
}
