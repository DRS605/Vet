using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarRecordatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recordatorio",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fecha_objetivo = table.Column<DateOnly>(type: "date", nullable: false),
                    notas = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    referencia_tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_envio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recordatorio", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recordatorio_empresa_animal",
                schema: "clinica",
                table: "recordatorio",
                columns: new[] { "empresa_id", "animal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recordatorio_empresa_estado_fecha",
                schema: "clinica",
                table: "recordatorio",
                columns: new[] { "empresa_id", "estado", "fecha_objetivo" });

            migrationBuilder.CreateIndex(
                name: "ix_recordatorio_referencia",
                schema: "clinica",
                table: "recordatorio",
                columns: new[] { "empresa_id", "referencia_tipo", "referencia_id" },
                unique: true,
                filter: "referencia_id IS NOT NULL");

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "recordatorio"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "recordatorio"));

            migrationBuilder.DropTable(
                name: "recordatorio",
                schema: "clinica");
        }
    }
}
