using AlxorCore.Identidad.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Identidad.Infraestructura.Persistencia.Configuraciones;

/// <summary>Mapeo de la entidad <see cref="Usuario"/> a la tabla <c>usuario</c>.</summary>
internal sealed class ConfiguracionUsuario : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnName("id");

        // Value object Email <-> texto normalizado.
        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(Email.LongitudMaxima)
            .HasConversion(email => email.Valor, valor => Email.Rehidratar(valor))
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ux_usuario_email");

        builder.Property(u => u.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(Usuario.LongitudMaximaNombre)
            .IsRequired();

        // Value object HashContrasena <-> texto opaco.
        builder.Property(u => u.HashContrasena)
            .HasColumnName("hash_contrasena")
            .HasMaxLength(256)
            .HasConversion(hash => hash.Valor, valor => HashContrasena.DesdeHash(valor))
            .IsRequired();

        builder.Property(u => u.Estado)
            .HasColumnName("estado")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(u => u.EmailVerificado)
            .HasColumnName("email_verificado")
            .IsRequired();

        builder.Property(u => u.Sexo)
            .HasColumnName("sexo")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(SexoUsuario.NoIndicado)
            .IsRequired();

        builder.Property(u => u.CreadoEn)
            .HasColumnName("creado_en")
            .IsRequired();

        builder.Property(u => u.ActualizadoEn)
            .HasColumnName("actualizado_en")
            .IsRequired();

        // Tokens de cuenta (solo el hash).
        builder.Property(u => u.TokenVerificacionHash).HasColumnName("token_verificacion_hash").HasMaxLength(64);
        builder.Property(u => u.TokenVerificacionExpira).HasColumnName("token_verificacion_expira");
        builder.Property(u => u.TokenRestablecimientoHash).HasColumnName("token_restablecimiento_hash").HasMaxLength(64);
        builder.Property(u => u.TokenRestablecimientoExpira).HasColumnName("token_restablecimiento_expira");
        builder.HasIndex(u => u.TokenVerificacionHash).HasDatabaseName("ix_usuario_token_verificacion");
        builder.HasIndex(u => u.TokenRestablecimientoHash).HasDatabaseName("ix_usuario_token_restablecimiento");

        // Los eventos de dominio no se persisten.
        builder.Ignore(u => u.EventosDominio);
    }
}
