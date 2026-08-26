using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Clinica.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarEspecies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "especie",
                schema: "clinica",
                table: "pauta_vacunal",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "especie",
                schema: "clinica",
                table: "animal",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateTable(
                name: "especie",
                schema: "clinica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    meses_cachorro = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_especie", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_especie_empresa_nombre",
                schema: "clinica",
                table: "especie",
                columns: new[] { "empresa_id", "nombre" },
                unique: true);

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("clinica", "especie"));

            // Siembra el maestro de especies por empresa existente: las 7 especies por defecto (Conejo
            // hasta los 6 meses cachorro; el resto, 12) más CUALQUIER valor de especie ya en uso en
            // animal/pauta_vacunal que no esté entre ellas. Así, tras migrar, ningún animal ni pauta
            // queda apuntando a una especie inexistente en el maestro. Idempotente por el índice único.
            migrationBuilder.Sql("""
                INSERT INTO "clinica"."especie" (id, empresa_id, nombre, meses_cachorro, activo, creado_en, actualizado_en)
                SELECT gen_random_uuid(), e.id, d.nombre, d.meses, true, now(), now()
                FROM "organizacion"."empresa" e
                CROSS JOIN (VALUES
                    ('Perro', 12), ('Gato', 12), ('Conejo', 6), ('Ave', 12),
                    ('Huron', 12), ('Reptil', 12), ('Otro', 12)
                ) AS d(nombre, meses)
                ON CONFLICT (empresa_id, nombre) DO NOTHING;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "clinica"."especie" (id, empresa_id, nombre, meses_cachorro, activo, creado_en, actualizado_en)
                SELECT gen_random_uuid(), u.empresa_id, u.especie, 12, true, now(), now()
                FROM (
                    SELECT empresa_id, especie FROM "clinica"."animal"
                    UNION
                    SELECT empresa_id, especie FROM "clinica"."pauta_vacunal"
                ) AS u
                WHERE u.especie IS NOT NULL AND btrim(u.especie) <> ''
                ON CONFLICT (empresa_id, nombre) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("clinica", "especie"));

            migrationBuilder.DropTable(
                name: "especie",
                schema: "clinica");

            migrationBuilder.AlterColumn<string>(
                name: "especie",
                schema: "clinica",
                table: "pauta_vacunal",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "especie",
                schema: "clinica",
                table: "animal",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);
        }
    }
}
