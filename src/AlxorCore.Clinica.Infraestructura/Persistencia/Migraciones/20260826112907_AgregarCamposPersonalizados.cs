using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarCamposPersonalizados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campo_personalizado",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    etiqueta = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    clave = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    opciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campo_personalizado", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "valor_campo_personalizado",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_valor_campo_personalizado", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_campo_personalizado_empresa_entidad_clave",
                schema: "clinica",
                table: "campo_personalizado",
                columns: new[] { "empresa_id", "entidad", "clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_valor_campo_empresa_registro",
                schema: "clinica",
                table: "valor_campo_personalizado",
                columns: new[] { "empresa_id", "registro_id" });

            migrationBuilder.CreateIndex(
                name: "ux_valor_campo_campo_registro",
                schema: "clinica",
                table: "valor_campo_personalizado",
                columns: new[] { "campo_id", "registro_id" },
                unique: true);

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "campo_personalizado"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "valor_campo_personalizado"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "valor_campo_personalizado"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "campo_personalizado"));

            migrationBuilder.DropTable(
                name: "campo_personalizado",
                schema: "clinica");

            migrationBuilder.DropTable(
                name: "valor_campo_personalizado",
                schema: "clinica");
        }
    }
}
