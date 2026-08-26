using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Catalogo.Infraestructura;

/// <summary>Composición del módulo Catálogo.</summary>
public static class RegistroServicios
{
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloCatalogo(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{CadenaConexion}».");

        servicios.AddScoped<InterceptorEmpresa>();
        servicios.AddDbContext<CatalogoDbContext>((sp, opciones) =>
            opciones
                .UseNpgsql(conexion, npgsql =>
                    npgsql.MigrationsHistoryTable("__historial_migraciones", CatalogoDbContext.Esquema))
                .AddInterceptors(sp.GetRequiredService<InterceptorEmpresa>()));

        servicios.AddScoped<IUnidadDeTrabajoCatalogo>(sp => sp.GetRequiredService<CatalogoDbContext>());
        servicios.AddScoped<RepositorioProductos>();
        servicios.AddScoped<IRepositorioProductos>(sp => sp.GetRequiredService<RepositorioProductos>());
        servicios.AddScoped<IConsultaProductos>(sp => sp.GetRequiredService<RepositorioProductos>());
        servicios.AddScoped<RepositorioHistoricoPrecios>();
        servicios.AddScoped<IRepositorioHistoricoPrecios>(sp => sp.GetRequiredService<RepositorioHistoricoPrecios>());
        servicios.AddScoped<IConsultaHistoricoPrecios>(sp => sp.GetRequiredService<RepositorioHistoricoPrecios>());
        servicios.AddScoped<RepositorioMovimientosStock>();
        servicios.AddScoped<IRepositorioMovimientosStock>(sp => sp.GetRequiredService<RepositorioMovimientosStock>());
        servicios.AddScoped<IConsultaMovimientosStock>(sp => sp.GetRequiredService<RepositorioMovimientosStock>());

        servicios.AddScoped<CrearProducto>();
        servicios.AddScoped<ImportarProductos>();
        servicios.AddScoped<ActualizarProducto>();
        servicios.AddScoped<DesactivarProducto>();
        servicios.AddScoped<ListarProductos>();
        servicios.AddScoped<ObtenerProducto>();
        servicios.AddScoped<ListarHistoricoPrecios>();
        servicios.AddScoped<RegistrarMovimientoStock>();
        servicios.AddScoped<ListarMovimientosStock>();
        servicios.AddScoped<IStockVentas, StockVentas>();

        return servicios;
    }
}
