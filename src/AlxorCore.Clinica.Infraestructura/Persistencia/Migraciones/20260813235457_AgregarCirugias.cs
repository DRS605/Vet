using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarCirugias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cirugia",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    cirujano = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    anestesia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    complicaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    proxima_revision = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cirugia", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cirugia_empresa_animal",
                schema: "clinica",
                table: "cirugia",
                columns: new[] { "empresa_id", "animal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cirugia_empresa_proxima_revision",
                schema: "clinica",
                table: "cirugia",
                columns: new[] { "empresa_id", "proxima_revision" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "cirugia"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "cirugia"));

            migrationBuilder.DropTable(
                name: "cirugia",
                schema: "clinica");
        }
    }
}
