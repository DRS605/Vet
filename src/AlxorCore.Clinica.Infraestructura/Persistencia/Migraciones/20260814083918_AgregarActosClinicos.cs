using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarActosClinicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acto_clinico",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    importe = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    porcentaje_iva = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    referencia_tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cobrado_ticket_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acto_clinico", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acto_clinico_empresa_animal",
                schema: "clinica",
                table: "acto_clinico",
                columns: new[] { "empresa_id", "animal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acto_clinico_empresa_cliente_estado",
                schema: "clinica",
                table: "acto_clinico",
                columns: new[] { "empresa_id", "cliente_id", "estado" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "acto_clinico"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "acto_clinico"));

            migrationBuilder.DropTable(
                name: "acto_clinico",
                schema: "clinica");
        }
    }
}
