using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarAdjuntos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "adjunto",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamano = table.Column<int>(type: "integer", nullable: false),
                    datos = table.Column<byte[]>(type: "bytea", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjunto", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_adjunto_empresa_animal",
                schema: "clinica",
                table: "adjunto",
                columns: new[] { "empresa_id", "animal_id" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "adjunto"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "adjunto"));

            migrationBuilder.DropTable(
                name: "adjunto",
                schema: "clinica");
        }
    }
}
