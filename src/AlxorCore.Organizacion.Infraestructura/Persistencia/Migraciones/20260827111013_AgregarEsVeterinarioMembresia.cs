using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Organizacion.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarEsVeterinarioMembresia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_veterinario",
                schema: "organizacion",
                table: "membresia",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "es_veterinario",
                schema: "organizacion",
                table: "membresia");
        }
    }
}
