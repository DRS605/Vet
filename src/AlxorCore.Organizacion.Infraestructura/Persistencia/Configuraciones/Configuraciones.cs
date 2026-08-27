using AlxorCore.Nucleo.Comun;
using AlxorCore.Organizacion.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Organizacion.Infraestructura.Persistencia.Configuraciones;

internal sealed class ConfiguracionEmpresa : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("empresa");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.Nif)
            .HasColumnName("nif")
            .HasMaxLength(20)
            .HasConversion(n => n.Valor, v => Nif.Rehidratar(v))
            .IsRequired();
        builder.HasIndex(e => e.Nif).IsUnique().HasDatabaseName("ux_empresa_nif");

        builder.Property(e => e.RazonSocial)
            .HasColumnName("razon_social")
            .HasMaxLength(Empresa.LongitudMaximaRazonSocial)
            .IsRequired();

        builder.OwnsOne(e => e.Direccion, d =>
        {
            d.Property(p => p.Calle).HasColumnName("direccion_calle").HasMaxLength(200);
            d.Property(p => p.CodigoPostal).HasColumnName("direccion_cp").HasMaxLength(10);
            d.Property(p => p.Poblacion).HasColumnName("direccion_poblacion").HasMaxLength(120);
            d.Property(p => p.Provincia).HasColumnName("direccion_provincia").HasMaxLength(120);
            d.Property(p => p.Pais).HasColumnName("direccion_pais").HasMaxLength(2);
        });

        builder.Property(e => e.RegimenIva).HasColumnName("regimen_iva").HasMaxLength(30).HasConversion<string>().IsRequired();
        builder.Property(e => e.Moneda).HasColumnName("moneda").HasMaxLength(3).IsRequired();
        builder.Property(e => e.Pais).HasColumnName("pais").HasMaxLength(2).IsRequired();
        builder.Property(e => e.Iban).HasColumnName("iban").HasMaxLength(34);
        builder.Property(e => e.IdentificadorAcreedor).HasColumnName("identificador_acreedor").HasMaxLength(35);
        builder.Property(e => e.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(e => e.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.Ignore(e => e.EventosDominio);
    }
}

internal sealed class ConfiguracionMembresia : IEntityTypeConfiguration<Membresia>
{
    public void Configure(EntityTypeBuilder<Membresia> builder)
    {
        builder.ToTable("membresia");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(m => m.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(m => m.RolCodigo).HasColumnName("rol_codigo").HasMaxLength(30).IsRequired();
        builder.Property(m => m.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(m => m.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(m => m.EsVeterinario).HasColumnName("es_veterinario").HasDefaultValue(false).IsRequired();

        builder.HasIndex(m => new { m.UsuarioId, m.EmpresaId }).IsUnique().HasDatabaseName("ux_membresia_usuario_empresa");
        builder.Ignore(m => m.EventosDominio);
    }
}

internal sealed class ConfiguracionSerie : IEntityTypeConfiguration<SerieNumeracion>
{
    public void Configure(EntityTypeBuilder<SerieNumeracion> builder)
    {
        builder.ToTable("serie_numeracion");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(s => s.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(s => s.Ejercicio).HasColumnName("ejercicio").IsRequired();
        builder.Property(s => s.Prefijo).HasColumnName("prefijo").HasMaxLength(SerieNumeracion.LongitudMaximaPrefijo).IsRequired();
        builder.Property(s => s.SiguienteNumero).HasColumnName("siguiente_numero").IsRequired();
        builder.Property(s => s.CreadoEn).HasColumnName("creado_en").IsRequired();

        builder.HasIndex(s => new { s.EmpresaId, s.TipoDocumento, s.Ejercicio, s.Prefijo })
            .IsUnique()
            .HasDatabaseName("ux_serie_empresa_tipo_ejercicio_prefijo");
        builder.Ignore(s => s.EventosDominio);
    }
}
