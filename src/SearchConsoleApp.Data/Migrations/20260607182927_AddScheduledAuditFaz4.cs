using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchConsoleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledAuditFaz4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ScheduledAuditId",
                table: "AuditRun",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScheduledAudit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    SearchConsolePropertyUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    MigrationSourceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Ga4PropertyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IntervalDays = table.Column<int>(type: "int", nullable: false),
                    NextRunUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAuditRunId = table.Column<long>(type: "bigint", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledAudit", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRun_ScheduledAuditId",
                table: "AuditRun",
                column: "ScheduledAuditId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledAudit_CustomerId",
                table: "ScheduledAudit",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledAudit_EntityId",
                table: "ScheduledAudit",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledAudit_IsEnabled_NextRunUtc",
                table: "ScheduledAudit",
                columns: new[] { "IsEnabled", "NextRunUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledAudit");

            migrationBuilder.DropIndex(
                name: "IX_AuditRun_ScheduledAuditId",
                table: "AuditRun");

            migrationBuilder.DropColumn(
                name: "ScheduledAuditId",
                table: "AuditRun");
        }
    }
}
