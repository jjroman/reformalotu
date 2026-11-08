using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reformk.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Videos",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE Videos
                SET Title = CASE VideoId
                    WHEN 'CiOG_t2Dqyw' THEN 'Instalación de cerámica 6: colocar baldosas impermeabilizando'
                    WHEN 'uOjPmfks8Y0' THEN 'Cómo montar jardín vertical modular con riego'
                    WHEN 'iOXMR9pforg' THEN 'Renovar muebles y cerámica de la cocina con pintura'
                    WHEN '0shmgc-rIFY' THEN 'Montar guías, cajones y gaveteros de cocina'
                    ELSE Title
                END
                WHERE VideoId IN ('CiOG_t2Dqyw', 'uOjPmfks8Y0', 'iOXMR9pforg', '0shmgc-rIFY');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Videos");
        }
    }
}
