using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reformk.Data.CatalogMigrations
{
    /// <inheritdoc />
    public partial class InitialVideoCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    VideoId = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false, defaultValue: ""),
                    TranscriptText = table.Column<string>(type: "TEXT", nullable: false),
                    Categories = table.Column<string>(type: "TEXT", nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", nullable: false),
                    AnalysisTranscriptHash = table.Column<string>(type: "TEXT", nullable: true),
                    AnalysisPromptVersion = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.VideoId);
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
                name: "VideoSemanticIndexes",
                columns: table => new
                {
                    VideoId = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Embedding = table.Column<byte[]>(type: "BLOB", nullable: false),
                    TranscriptHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PromptVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SummaryModel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EmbeddingModel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EmbeddingDimensions = table.Column<int>(type: "INTEGER", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSemanticIndexes", x => x.VideoId);
                    table.ForeignKey(
                        name: "FK_VideoSemanticIndexes_Videos_VideoId",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoInputs");

            migrationBuilder.DropTable(
                name: "VideoSemanticIndexes");

            migrationBuilder.DropTable(
                name: "VideoTaskDefinitions");

            migrationBuilder.DropTable(
                name: "Videos");
        }
    }
}
