using AlxorCore.Clinica.Aplicacion;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Clinica.Infraestructura;

/// <summary>Composición del módulo Clínica.</summary>
public static class RegistroServicios
{
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloClinica(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{CadenaConexion}».");

        servicios.AddScoped<InterceptorEmpresa>();
        servicios.AddDbContext<ClinicaDbContext>((sp, opciones) =>
            opciones
                .UseNpgsql(conexion, npgsql =>
                    npgsql.MigrationsHistoryTable("__historial_migraciones", ClinicaDbContext.Esquema))
                .AddInterceptors(sp.GetRequiredService<InterceptorEmpresa>()));

        servicios.AddScoped<IUnidadDeTrabajoClinica>(sp => sp.GetRequiredService<ClinicaDbContext>());
        servicios.AddScoped<RepositorioAnimales>();
        servicios.AddScoped<IRepositorioAnimales>(sp => sp.GetRequiredService<RepositorioAnimales>());
        servicios.AddScoped<IConsultaAnimales>(sp => sp.GetRequiredService<RepositorioAnimales>());

        servicios.AddScoped<RepositorioConsultas>();
        servicios.AddScoped<IRepositorioConsultas>(sp => sp.GetRequiredService<RepositorioConsultas>());
        servicios.AddScoped<IConsultaConsultas>(sp => sp.GetRequiredService<RepositorioConsultas>());

        servicios.AddScoped<CrearAnimal>();
        servicios.AddScoped<ActualizarAnimal>();
        servicios.AddScoped<ListarAnimales>();
        servicios.AddScoped<ListarAnimalesDeCliente>();
        servicios.AddScoped<ObtenerAnimal>();
        servicios.AddScoped<DarDeBajaAnimal>();

        servicios.AddScoped<RegistrarConsulta>();
        servicios.AddScoped<ActualizarConsulta>();
        servicios.AddScoped<ObtenerConsulta>();
        servicios.AddScoped<ListarConsultasDeAnimal>();
        servicios.AddScoped<AnularConsulta>();

        return servicios;
    }
}
