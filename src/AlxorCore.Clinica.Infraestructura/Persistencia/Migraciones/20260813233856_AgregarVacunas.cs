using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarVacunas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pauta_vacunal",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    especie = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    caracter = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    edad_inicio_semanas = table.Column<int>(type: "integer", nullable: true),
                    periodicidad_refuerzo_meses = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pauta_vacunal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vacunacion",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pauta_vacunal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    fecha_aplicacion = table.Column<DateOnly>(type: "date", nullable: false),
                    lote = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    proxima_dosis = table.Column<DateOnly>(type: "date", nullable: true),
                    veterinario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    notas = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacunacion", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pauta_vacunal_empresa_especie_nombre",
                schema: "clinica",
                table: "pauta_vacunal",
                columns: new[] { "empresa_id", "especie", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vacunacion_empresa_animal",
                schema: "clinica",
                table: "vacunacion",
                columns: new[] { "empresa_id", "animal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vacunacion_empresa_proxima_dosis",
                schema: "clinica",
                table: "vacunacion",
                columns: new[] { "empresa_id", "proxima_dosis" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "pauta_vacunal"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "vacunacion"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "vacunacion"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "pauta_vacunal"));

            migrationBuilder.DropTable(
                name: "pauta_vacunal",
                schema: "clinica");

            migrationBuilder.DropTable(
                name: "vacunacion",
                schema: "clinica");
        }
    }
}
