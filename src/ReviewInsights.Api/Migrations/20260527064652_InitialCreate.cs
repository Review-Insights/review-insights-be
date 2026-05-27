using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using ReviewInsights.Api.Domain.ValueObjects;

#nullable disable

namespace ReviewInsights.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_uploads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    total_records = table.Column<int>(type: "integer", nullable: false),
                    analyzed_records = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_uploads", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    filters = table.Column<ReportFilters>(type: "jsonb", nullable: false),
                    scope = table.Column<ReportScope>(type: "jsonb", nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_records = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<ReportSummary>(type: "jsonb", nullable: true),
                    insights = table.Column<List<ReportInsight>>(type: "jsonb", nullable: false),
                    suggestions = table.Column<List<ReportSuggestion>>(type: "jsonb", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clothing_id = table.Column<int>(type: "integer", nullable: false),
                    age = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    review_text = table.Column<string>(type: "text", nullable: true),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    recommended_ind = table.Column<bool>(type: "boolean", nullable: false),
                    positive_feedback_count = table.Column<int>(type: "integer", nullable: false),
                    division_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    department_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    class_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    overall_sentiment = table.Column<int>(type: "integer", nullable: true),
                    aspect_sentiments = table.Column<List<AspectSentiment>>(type: "jsonb", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: true),
                    priority_rule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    priority_reason = table.Column<string>(type: "text", nullable: true),
                    upload_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    analyzed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_reviews_file_uploads_upload_id",
                        column: x => x.upload_id,
                        principalTable: "file_uploads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_uploads_created_at",
                table: "file_uploads",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_file_uploads_status",
                table: "file_uploads",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_reports_generated_at",
                table: "reports",
                column: "generated_at");

            migrationBuilder.CreateIndex(
                name: "ix_reports_status",
                table: "reports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_clothing_id",
                table: "reviews",
                column: "clothing_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_created_at",
                table: "reviews",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_department",
                table: "reviews",
                column: "department_name");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_priority",
                table: "reviews",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_rating",
                table: "reviews",
                column: "rating");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_sentiment",
                table: "reviews",
                column: "overall_sentiment");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_upload_id",
                table: "reviews",
                column: "upload_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "file_uploads");
        }
    }
}
