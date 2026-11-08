using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reformk.Data.Migrations;

public partial class SeparateVideoCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE _VideoCatalogTransferGuard (Ready INTEGER NOT NULL CHECK (Ready = 1));
            INSERT INTO _VideoCatalogTransferGuard SELECT CASE WHEN EXISTS
                (SELECT 1 FROM CatalogTransferCompleted WHERE Id = 1) THEN 1 ELSE 0 END;
            DROP TABLE _VideoCatalogTransferGuard;
            """);

        migrationBuilder.CreateTable(
            name: "VideoOpenCounts",
            columns: table => new
            {
                VideoId = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                Count = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_VideoOpenCounts", x => x.VideoId));
        migrationBuilder.Sql("INSERT INTO VideoOpenCounts (VideoId, Count) SELECT VideoId, OpenCount FROM Videos WHERE OpenCount <> 0");

        migrationBuilder.DropForeignKey(name: "FK_Projects_Videos_SelectedVideoId", table: "Projects");
        migrationBuilder.DropIndex(name: "IX_Projects_SelectedVideoId", table: "Projects");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("The separated catalog cannot be merged automatically. Restore both database files from a coordinated backup.");
}
