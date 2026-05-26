using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReviewInsights.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveChurnFromReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "churn_causes",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "churn_probability",
                table: "reviews");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "churn_probability",
                table: "reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "churn_causes",
                table: "reviews",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }
    }
}
