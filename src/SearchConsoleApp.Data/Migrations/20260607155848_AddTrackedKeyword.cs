using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchConsoleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackedKeyword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackedKeyword",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    Keyword = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Position = table.Column<double>(type: "float", nullable: false),
                    Impressions = table.Column<int>(type: "int", nullable: false),
                    Clicks = table.Column<int>(type: "int", nullable: false),
                    Ctr = table.Column<double>(type: "float", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedKeyword", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedKeyword_AuditRunId",
                table: "TrackedKeyword",
                column: "AuditRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedKeyword_EntityId",
                table: "TrackedKeyword",
                column: "EntityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackedKeyword");
        }
    }
}
