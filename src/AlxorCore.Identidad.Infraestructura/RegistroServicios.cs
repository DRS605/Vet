using AlxorCore.Identidad.Aplicacion.CasosDeUso;
using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Infraestructura.Correo;
using AlxorCore.Identidad.Infraestructura.Eventos;
using AlxorCore.Identidad.Infraestructura.Persistencia;
using AlxorCore.Identidad.Infraestructura.Seguridad;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Seguridad;
using AlxorCore.Nucleo.Tiempo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Identidad.Infraestructura;

/// <summary>
/// Composición del módulo Identidad: registra persistencia, seguridad, puertos y casos de uso.
/// Es el único punto por el que el host (la API) conecta con el módulo.
/// </summary>
public static class RegistroServicios
{
    /// <summary>Nombre de la cadena de conexión esperada en la configuración.</summary>
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloIdentidad(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión «{CadenaConexion}» en la configuración.");

        servicios.AddDbContext<IdentidadDbContext>(opciones =>
            opciones.UseNpgsql(conexion, npgsql =>
                npgsql.MigrationsHistoryTable("__historial_migraciones", IdentidadDbContext.Esquema)));

        // Unidad de trabajo respaldada por el DbContext del módulo.
        servicios.AddScoped<IUnidadDeTrabajoIdentidad>(sp => sp.GetRequiredService<IdentidadDbContext>());
        servicios.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
        servicios.AddScoped<IConsultaUsuarios, ConsultaUsuarios>();
        servicios.AddScoped<IPublicadorEventos, PublicadorEventosRegistro>();
        // Correo: SMTP real si está configurado; si no, el stub (registra el enlace en el log).
        servicios.AddOptions<Correo.OpcionesCorreo>().Bind(configuracion.GetSection(Correo.OpcionesCorreo.Seccion));
        var opcionesCorreo = new Correo.OpcionesCorreo();
        configuracion.GetSection(Correo.OpcionesCorreo.Seccion).Bind(opcionesCorreo);
        if (opcionesCorreo.Configurado)
        {
            servicios.AddScoped<IServicioVerificacionEmail, Correo.ServicioCorreoSmtp>();
        }
        else
        {
            servicios.AddScoped<IServicioVerificacionEmail, ServicioVerificacionEmailStub>();
        }

        // Seguridad.
        servicios.AddOptions<OpcionesJwt>()
            .Bind(configuracion.GetSection(OpcionesJwt.Seccion))
            .ValidateDataAnnotations()
            // Rechaza el valor de ejemplo del .env.ejemplo: la app no debe arrancar con el placeholder.
            .Validate(
                opciones => !EsClaveSecretaDeEjemplo(opciones.ClaveSecreta),
                "El secreto JWT (Jwt:ClaveSecreta / JWT_CLAVE_SECRETA) sigue siendo el valor de ejemplo. " +
                "Genera uno real y aleatorio de 32+ caracteres (p. ej. «openssl rand -base64 48») antes de arrancar.");
        servicios.AddSingleton<IHasherContrasena, HasherContrasenaIdentity>();
        servicios.AddScoped<IProveedorTokens, ProveedorTokensJwt>();

        // Reloj del sistema (determinista solo en tests, donde se sustituye).
        servicios.AddSingleton<IReloj, RelojSistema>();

        // Casos de uso.
        servicios.AddScoped<RegistrarUsuario>();
        servicios.AddScoped<IniciarSesion>();
        servicios.AddScoped<ObtenerPerfil>();
        servicios.AddScoped<VerificarEmail>();
        servicios.AddScoped<RecuperarContrasena>();
        servicios.AddScoped<RestablecerContrasena>();
        servicios.AddScoped<CrearUsuarioInvitado>();

        return servicios;
    }

    /// <summary>
    /// Placeholder del secreto JWT que trae <c>despliegue/.env.ejemplo</c>. Se rechaza tal cual y
    /// también cualquier valor que empiece por «CAMBIA», para no arrancar con un secreto de ejemplo.
    /// </summary>
    private const string ClaveSecretaEjemplo =
        "CAMBIA_esto_por_un_secreto_largo_y_aleatorio_de_32+_caracteres";

    private static bool EsClaveSecretaDeEjemplo(string? claveSecreta)
    {
        if (string.IsNullOrWhiteSpace(claveSecreta))
        {
            return false; // La ausencia la detecta [Required]; aquí solo el valor de ejemplo.
        }

        return claveSecreta.Equals(ClaveSecretaEjemplo, StringComparison.Ordinal)
            || claveSecreta.StartsWith("CAMBIA", StringComparison.Ordinal);
    }
}
