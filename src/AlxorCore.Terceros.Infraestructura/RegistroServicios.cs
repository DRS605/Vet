using AlxorCore.Persistencia;
using AlxorCore.Terceros.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Terceros.Infraestructura;

/// <summary>Composición del módulo Terceros.</summary>
public static class RegistroServicios
{
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloTerceros(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{CadenaConexion}».");

        servicios.AddScoped<InterceptorEmpresa>();
        servicios.AddDbContext<TercerosDbContext>((sp, opciones) =>
            opciones
                .UseNpgsql(conexion, npgsql =>
                    npgsql.MigrationsHistoryTable("__historial_migraciones", TercerosDbContext.Esquema))
                .AddInterceptors(sp.GetRequiredService<InterceptorEmpresa>()));

        servicios.AddScoped<IUnidadDeTrabajoTerceros>(sp => sp.GetRequiredService<TercerosDbContext>());
        servicios.AddScoped<RepositorioClientes>();
        servicios.AddScoped<IRepositorioClientes>(sp => sp.GetRequiredService<RepositorioClientes>());
        servicios.AddScoped<IConsultaClientes>(sp => sp.GetRequiredService<RepositorioClientes>());

        servicios.AddScoped<CrearCliente>();
        servicios.AddScoped<ImportarClientes>();
        servicios.AddScoped<ActualizarCliente>();
        servicios.AddScoped<DesactivarCliente>();
        servicios.AddScoped<ListarClientes>();
        servicios.AddScoped<ObtenerCliente>();

        servicios.AddScoped<RepositorioProveedores>();
        servicios.AddScoped<IRepositorioProveedores>(sp => sp.GetRequiredService<RepositorioProveedores>());
        servicios.AddScoped<IConsultaProveedores>(sp => sp.GetRequiredService<RepositorioProveedores>());
        servicios.AddScoped<CrearProveedor>();
        servicios.AddScoped<ActualizarProveedor>();
        servicios.AddScoped<ListarProveedores>();
        servicios.AddScoped<ObtenerProveedor>();

        return servicios;
    }
}
