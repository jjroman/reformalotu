using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReformApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Projects",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE Projects SET Name = CASE WHEN length(trim(Description)) = 0 " +
                "THEN 'Mi proyecto' ELSE substr(trim(Description), 1, 200) END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Projects");
        }
    }
}
