using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MigracionInicialClinica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clinica");

            migrationBuilder.CreateTable(
                name: "animal",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    especie = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    raza = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sexo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_nacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    microchip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    esterilizado = table.Column<bool>(type: "boolean", nullable: false),
                    peso_kg = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    notas = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_animal", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_animal_empresa_cliente",
                schema: "clinica",
                table: "animal",
                columns: new[] { "empresa_id", "cliente_id" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "animal"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "animal"));

            migrationBuilder.DropTable(
                name: "animal",
                schema: "clinica");
        }
    }
}
