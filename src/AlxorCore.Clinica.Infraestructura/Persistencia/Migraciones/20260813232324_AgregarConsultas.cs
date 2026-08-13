using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarConsultas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consulta",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    diagnostico = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    tratamiento = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    peso_kg = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    veterinario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consulta", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consulta_empresa_animal",
                schema: "clinica",
                table: "consulta",
                columns: new[] { "empresa_id", "animal_id" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "consulta"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "consulta"));

            migrationBuilder.DropTable(
                name: "consulta",
                schema: "clinica");
        }
    }
}
