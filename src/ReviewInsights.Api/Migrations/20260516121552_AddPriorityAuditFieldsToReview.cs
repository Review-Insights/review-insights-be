using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReviewInsights.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityAuditFieldsToReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "priority_reason",
                table: "reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "priority_rule",
                table: "reviews",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "priority_reason",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "priority_rule",
                table: "reviews");
        }
    }
}
