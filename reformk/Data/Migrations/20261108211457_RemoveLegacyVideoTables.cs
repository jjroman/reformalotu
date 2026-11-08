using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reformk.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyVideoTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VideoInputs");
            migrationBuilder.DropTable(name: "VideoSemanticIndexes");
            migrationBuilder.DropTable(name: "VideoTaskDefinitions");
            migrationBuilder.DropTable(name: "Videos");
            migrationBuilder.Sql("DROP TABLE CatalogTransferCompleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("The separated catalog cannot be merged automatically. Restore both database files from a coordinated backup.");
        }
    }
}
