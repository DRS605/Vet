using AlxorCore.Organizacion.Aplicacion.CasosDeUso;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Organizacion.Infraestructura.Numeracion;
using AlxorCore.Organizacion.Infraestructura.Persistencia;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Organizacion.Infraestructura;

/// <summary>Composición del módulo Organización.</summary>
public static class RegistroServicios
{
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloOrganizacion(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{CadenaConexion}».");

        servicios.AddScoped<InterceptorEmpresa>();

        servicios.AddDbContext<OrganizacionDbContext>((sp, opciones) =>
            opciones
                .UseNpgsql(conexion, npgsql =>
                    npgsql.MigrationsHistoryTable("__historial_migraciones", OrganizacionDbContext.Esquema))
                .AddInterceptors(sp.GetRequiredService<InterceptorEmpresa>()));

        servicios.AddScoped<IUnidadDeTrabajoOrganizacion>(sp => sp.GetRequiredService<OrganizacionDbContext>());
        servicios.AddScoped<RepositorioEmpresas>();
        servicios.AddScoped<IRepositorioEmpresas>(sp => sp.GetRequiredService<RepositorioEmpresas>());
        servicios.AddScoped<IConsultaEmpresas>(sp => sp.GetRequiredService<RepositorioEmpresas>());
        servicios.AddScoped<IRepositorioMembresias, RepositorioMembresias>();
        servicios.AddScoped<IRepositorioSeries, RepositorioSeries>();
        servicios.AddScoped<IConsultasOrganizacion, ConsultasOrganizacion>();
        servicios.AddScoped<IServicioNumeracion, ServicioNumeracion>();

        servicios.AddScoped<CrearEmpresa>();
        servicios.AddScoped<ConsultarEstadoInstalacion>();
        servicios.AddScoped<ActualizarDatosCobro>();
        servicios.AddScoped<ActualizarEmpresa>();
        servicios.AddScoped<ListarMisEmpresas>();
        servicios.AddScoped<ObtenerEmpresa>();
        servicios.AddScoped<SeleccionarEmpresa>();
        servicios.AddScoped<CrearSerie>();
        servicios.AddScoped<ListarSeries>();
        servicios.AddScoped<ListarMembresias>();
        servicios.AddScoped<AgregarMembresia>();
        servicios.AddScoped<CambiarRolMembresia>();
        servicios.AddScoped<MarcarVeterinarioMembresia>();
        servicios.AddScoped<RevocarMembresia>();

        return servicios;
    }
}
