using AlxorCore.Api.Comun;
using AlxorCore.Api.Contratos;
using AlxorCore.Identidad.Aplicacion.CasosDeUso;
using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Terceros.Aplicacion;
using AlxorCore.Auditoria.Infraestructura;
using AlxorCore.Catalogo.Infraestructura;
using AlxorCore.Facturacion.Infraestructura;
using AlxorCore.Gastos.Infraestructura;
using AlxorCore.Organizacion.Infraestructura.Persistencia;
using AlxorCore.Terceros.Infraestructura;
using AlxorCore.Tesoreria.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints de la cuenta/empresa: derechos RGPD (portabilidad y supresión).</summary>
public static class EndpointsCuenta
{
    private static readonly System.Text.Json.JsonSerializerOptions OpcionesExport = CrearOpciones();

    private static System.Text.Json.JsonSerializerOptions CrearOpciones()
    {
        var opciones = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        opciones.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return opciones;
    }

    public static IEndpointRouteBuilder MapearCuenta(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var cuenta = rutas.MapGroup("/cuenta").WithTags("Cuenta / RGPD");

        cuenta.MapPut("/perfil", ActualizarPerfilAsync)
            .WithSummary("Actualiza el perfil del usuario autenticado (nombre y sexo).")
            .RequireAuthorization();

        cuenta.MapPost("/cambiar-clave", CambiarClaveAsync)
            .WithSummary("Cambia la contraseña del usuario autenticado (requiere la contraseña actual).")
            .RequireAuthorization();

        cuenta.MapGet("/exportar", ExportarAsync)
            .WithSummary("Exporta todos los datos de la empresa activa (RGPD: acceso y portabilidad).")
            .RequierePermiso(Permisos.DatosExportar);

        cuenta.MapDelete("", EliminarAsync)
            .WithSummary("Elimina la empresa activa y todos sus datos (RGPD: derecho de supresión).")
            .RequierePermiso(Permisos.UsuarioGestionar);

        return rutas;
    }

    private static async Task<IResult> ActualizarPerfilAsync(
        ActualizarPerfilPeticion peticion,
        System.Security.Claims.ClaimsPrincipal usuario,
        ActualizarPerfil casoDeUso,
        CancellationToken ct)
    {
        var usuarioId = usuario.ObtenerUsuarioId();
        if (usuarioId is null)
        {
            return ResultadosHttp.AProblema(Error.NoAutenticado("auth.token_invalido", "El token no identifica al usuario."));
        }

        var resultado = await casoDeUso
            .EjecutarAsync(usuarioId.Value, new ActualizarPerfilComando(peticion.Nombre, peticion.Sexo), ct)
            .ConfigureAwait(false);
        return resultado.AOk();
    }

    private static async Task<IResult> CambiarClaveAsync(
        CambiarClavePeticion peticion,
        System.Security.Claims.ClaimsPrincipal usuario,
        CambiarContrasena casoDeUso,
        CancellationToken ct)
    {
        var usuarioId = usuario.ObtenerUsuarioId();
        if (usuarioId is null)
        {
            return ResultadosHttp.AProblema(Error.NoAutenticado("auth.token_invalido", "El token no identifica al usuario."));
        }

        var resultado = await casoDeUso
            .EjecutarAsync(usuarioId.Value, new CambiarContrasenaComando(peticion.ClaveActual, peticion.NuevaClave), ct)
            .ConfigureAwait(false);
        return resultado.ASinContenido();
    }

    private static async Task<IResult> ExportarAsync(
        IContextoEmpresa contexto,
        IConsultaEmpresas empresas,
        IConsultaClientes clientes,
        IConsultaProveedores proveedores,
        IConsultaProductos productos,
        IConsultaFacturas facturas,
        IConsultaGastos gastos,
        CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var id = contexto.EmpresaId.Value;
        var datos = new
        {
            generadoEn = DateTimeOffset.UtcNow,
            empresa = await empresas.ObtenerAsync(id, ct).ConfigureAwait(false),
            clientes = await clientes.ListarAsync(id, incluirInactivos: true, ct).ConfigureAwait(false),
            proveedores = await proveedores.ListarAsync(id, incluirInactivos: true, ct).ConfigureAwait(false),
            productos = await productos.ListarAsync(id, incluirInactivos: true, ct).ConfigureAwait(false),
            facturas = await facturas.ListarAsync(id, ct).ConfigureAwait(false),
            gastos = await gastos.ListarAsync(id, ct).ConfigureAwait(false),
        };

        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(datos, OpcionesExport);
        var nombre = $"alxor-export-{DateTime.UtcNow:yyyyMMdd}.json";
        return Results.File(bytes, "application/json", nombre);
    }

    private static async Task<IResult> EliminarAsync(
        IContextoEmpresa contexto,
        FacturacionDbContext facturacion,
        GastosDbContext gastos,
        TesoreriaDbContext tesoreria,
        TercerosDbContext terceros,
        CatalogoDbContext catalogo,
        AuditoriaDbContext auditoria,
        OrganizacionDbContext organizacion,
        CancellationToken ct)
    {
        if (contexto.EmpresaId is not { } id)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        // Borramos los datos de la empresa en cada módulo. El filtro por empresa (EF + RLS) garantiza
        // que solo se eliminan los de la empresa activa; las líneas (owned) caen en cascada.
        await facturacion.Facturas.Where(f => f.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await facturacion.FacturasRecurrentes.Where(r => r.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await gastos.Gastos.Where(g => g.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await tesoreria.Movimientos.Where(m => m.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await terceros.Clientes.Where(c => c.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await terceros.Proveedores.Where(p => p.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await catalogo.HistoricoPrecios.Where(h => h.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await catalogo.Productos.Where(p => p.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await auditoria.Registros.Where(a => a.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await organizacion.Series.Where(s => s.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await organizacion.Membresias.Where(m => m.EmpresaId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await organizacion.Empresas.Where(e => e.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);

        return Results.Ok(new { eliminada = id });
    }
}
