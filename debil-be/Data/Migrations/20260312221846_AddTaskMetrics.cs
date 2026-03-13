using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace debil_be.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TaskType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TaskName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    AvgConfidence = table.Column<double>(type: "double precision", nullable: false),
                    MinConfidence = table.Column<double>(type: "double precision", nullable: false),
                    MaxConfidence = table.Column<double>(type: "double precision", nullable: false),
                    Accuracy = table.Column<double>(type: "double precision", nullable: true),
                    Precision = table.Column<double>(type: "double precision", nullable: true),
                    Recall = table.Column<double>(type: "double precision", nullable: true),
                    F1Score = table.Column<double>(type: "double precision", nullable: true),
                    AucRoc = table.Column<double>(type: "double precision", nullable: true),
                    Support = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskMetrics_Analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "Analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskMetrics_AnalysisId",
                table: "TaskMetrics",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskMetrics_AnalysisId_TaskId",
                table: "TaskMetrics",
                columns: new[] { "AnalysisId", "TaskId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskMetrics");
        }
    }
}
