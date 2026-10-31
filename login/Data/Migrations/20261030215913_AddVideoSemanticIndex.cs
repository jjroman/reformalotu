using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace login.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoSemanticIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoSemanticIndexes",
                columns: table => new
                {
                    VideoId = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Embedding = table.Column<byte[]>(type: "BLOB", nullable: false),
                    TranscriptHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoSemanticIndexes");
        }
    }
}
