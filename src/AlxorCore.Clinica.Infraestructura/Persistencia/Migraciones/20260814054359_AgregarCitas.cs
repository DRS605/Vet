using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarCitas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cita",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duracion_minutos = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    veterinario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notas = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cita", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cita_empresa_animal",
                schema: "clinica",
                table: "cita",
                columns: new[] { "empresa_id", "animal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cita_empresa_estado_inicio",
                schema: "clinica",
                table: "cita",
                columns: new[] { "empresa_id", "estado", "inicio" });

            migrationBuilder.CreateIndex(
                name: "ix_cita_empresa_inicio",
                schema: "clinica",
                table: "cita",
                columns: new[] { "empresa_id", "inicio" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "cita"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "cita"));

            migrationBuilder.DropTable(
                name: "cita",
                schema: "clinica");
        }
    }
}
