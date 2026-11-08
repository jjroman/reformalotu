using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reformk.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoMaterialsAndEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Equipment",
                table: "Videos",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Materials",
                table: "Videos",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Equipment",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Materials",
                table: "Videos");
        }
    }
}
