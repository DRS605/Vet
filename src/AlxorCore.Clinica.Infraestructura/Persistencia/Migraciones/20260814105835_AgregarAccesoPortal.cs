using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarAccesoPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acceso_portal",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revocado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acceso_portal", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_acceso_portal_cliente_activo",
                schema: "clinica",
                table: "acceso_portal",
                columns: new[] { "empresa_id", "cliente_id" },
                unique: true,
                filter: "activo");

            migrationBuilder.CreateIndex(
                name: "ux_acceso_portal_token",
                schema: "clinica",
                table: "acceso_portal",
                column: "token",
                unique: true);

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "acceso_portal"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "acceso_portal"));

            migrationBuilder.DropTable(
                name: "acceso_portal",
                schema: "clinica");
        }
    }
}
