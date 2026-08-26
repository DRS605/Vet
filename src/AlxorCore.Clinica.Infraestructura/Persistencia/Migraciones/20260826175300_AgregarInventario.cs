using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "articulo_inventario",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    categoria = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    stock = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    stock_minimo = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    caducidad = table.Column<DateOnly>(type: "date", nullable: true),
                    notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_articulo_inventario", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_articulo_inventario_empresa_nombre",
                schema: "clinica",
                table: "articulo_inventario",
                columns: new[] { "empresa_id", "nombre" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "articulo_inventario"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "articulo_inventario"));

            migrationBuilder.DropTable(
                name: "articulo_inventario",
                schema: "clinica");
        }
    }
}
