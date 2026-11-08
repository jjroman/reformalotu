using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reformk.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImplementProjectVideoSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectRecommendations");

            migrationBuilder.DropColumn(
                name: "Equipment",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Materials",
                table: "Videos");

            migrationBuilder.AddColumn<int>(
                name: "PromptVersion",
                table: "VideoSemanticIndexes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AnalysisPromptVersion",
                table: "Videos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisTranscriptHash",
                table: "Videos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedVideoId",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkDescription",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectInputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Brand = table.Column<string>(type: "TEXT", nullable: true),
                    Specification = table.Column<string>(type: "TEXT", nullable: true),
                    Quantity = table.Column<double>(type: "REAL", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    Level = table.Column<string>(type: "TEXT", nullable: true),
                    Headcount = table.Column<int>(type: "INTEGER", nullable: true),
                    EstimatedHours = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsManual = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectInputs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsDone = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoInputs",
                columns: table => new
                {
                    VideoId = table.Column<string>(type: "TEXT", nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Brand = table.Column<string>(type: "TEXT", nullable: true),
                    Specification = table.Column<string>(type: "TEXT", nullable: true),
                    Quantity = table.Column<double>(type: "REAL", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    Level = table.Column<string>(type: "TEXT", nullable: true),
                    Headcount = table.Column<int>(type: "INTEGER", nullable: true),
                    EstimatedHours = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoInputs", x => new { x.VideoId, x.SourceId });
                    table.ForeignKey(
                        name: "FK_VideoInputs_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "VideoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoTaskDefinitions",
                columns: table => new
                {
                    VideoId = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    EstimatedMinutes = table.Column<double>(type: "REAL", nullable: true),
                    MaterialIds = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    LaborIds = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoTaskDefinitions", x => new { x.VideoId, x.Order });
                    table.ForeignKey(
                        name: "FK_VideoTaskDefinitions_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "VideoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTaskInputs",
                columns: table => new
                {
                    InputsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TasksId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTaskInputs", x => new { x.InputsId, x.TasksId });
                    table.ForeignKey(
                        name: "FK_ProjectTaskInputs_ProjectInputs_InputsId",
                        column: x => x.InputsId,
                        principalTable: "ProjectInputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTaskInputs_ProjectTasks_TasksId",
                        column: x => x.TasksId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_SelectedVideoId",
                table: "Projects",
                column: "SelectedVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInputs_ProjectId",
                table: "ProjectInputs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTaskInputs_TasksId",
                table: "ProjectTaskInputs",
                column: "TasksId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_ProjectId",
                table: "ProjectTasks",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Videos_SelectedVideoId",
                table: "Projects",
                column: "SelectedVideoId",
                principalTable: "Videos",
                principalColumn: "VideoId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Videos_SelectedVideoId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProjectTaskInputs");

            migrationBuilder.DropTable(
                name: "VideoInputs");

            migrationBuilder.DropTable(
                name: "VideoTaskDefinitions");

            migrationBuilder.DropTable(
                name: "ProjectInputs");

            migrationBuilder.DropTable(
                name: "ProjectTasks");

            migrationBuilder.DropIndex(
                name: "IX_Projects_SelectedVideoId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "VideoSemanticIndexes");

            migrationBuilder.DropColumn(
                name: "AnalysisPromptVersion",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "AnalysisTranscriptHash",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "SelectedVideoId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "WorkDescription",
                table: "Projects");

            migrationBuilder.AddColumn<string>(
                name: "Equipment",
                table: "Videos",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Materials",
                table: "Videos",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ProjectRecommendations",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VideoId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRecommendations", x => new { x.ProjectId, x.VideoId });
                    table.ForeignKey(
                        name: "FK_ProjectRecommendations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectRecommendations_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "VideoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRecommendations_ProjectId_Position",
                table: "ProjectRecommendations",
                columns: new[] { "ProjectId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRecommendations_VideoId",
                table: "ProjectRecommendations",
                column: "VideoId");
        }
    }
}
