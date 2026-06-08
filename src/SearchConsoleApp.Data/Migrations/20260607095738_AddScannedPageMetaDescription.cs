using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchConsoleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScannedPageMetaDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "ScannedPage",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "ScannedPage");
        }
    }
}
