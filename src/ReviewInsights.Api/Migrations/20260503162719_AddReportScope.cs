using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReviewInsights.Api.Migrations
{
    public partial class AddReportScope : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "reports",
                type: "jsonb",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scope",
                table: "reports");
        }
    }
}
