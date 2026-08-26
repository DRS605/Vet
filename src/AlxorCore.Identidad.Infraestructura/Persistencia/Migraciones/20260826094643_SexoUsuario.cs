using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Identidad.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class SexoUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sexo",
                schema: "identidad",
                table: "usuario",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NoIndicado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sexo",
                schema: "identidad",
                table: "usuario");
        }
    }
}
