using AlxorCore.Api.Comun;
using AlxorCore.Clinica.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>
/// Endpoints de la <b>Cartilla Viva</b>: el portal del dueño de la mascota.
///
/// Hay dos grupos con seguridad muy distinta:
/// <list type="bullet">
/// <item>Lado clínica (autenticado, permiso <c>cliente.gestionar</c> —el portal es un dato del
/// cliente, así que se reutiliza su permiso de gestión—): generar/regenerar, consultar y revocar el
/// acceso de un cliente.</item>
/// <item>Lado portal (PÚBLICO, sin JWT, autorizado SOLO por el token de la ruta): consultar la
/// cartilla y confirmar una cita. El contexto de empresa se fija dentro del caso de uso a partir del
/// token antes de cualquier consulta, de modo que el filtro multiempresa (y la RLS) sigan aplicando.
/// Un token inválido o revocado devuelve 404 (no se filtra si existe).</item>
/// </list>
/// </summary>
public static class EndpointsPortal
{
    public static IEndpointRouteBuilder MapearPortal(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        // --- Lado clínica (autenticado) ---
        var gestion = rutas.MapGroup("/clientes").WithTags("Portal del dueño");

        gestion.MapPost("/{clienteId:guid}/portal", GenerarAsync)
            .WithSummary("Genera (o regenera) el acceso de portal de un cliente y devuelve el enlace de la Cartilla Viva.")
            .RequierePermiso(Permisos.ClienteGestionar);

        gestion.MapGet("/{clienteId:guid}/portal", ObtenerAsync)
            .WithSummary("Obtiene el estado y el enlace del acceso de portal de un cliente.")
            .RequierePermiso(Permisos.ClienteGestionar);

        gestion.MapDelete("/{clienteId:guid}/portal", RevocarAsync)
            .WithSummary("Revoca el acceso de portal de un cliente.")
            .RequierePermiso(Permisos.ClienteGestionar);

        // --- Lado portal (PÚBLICO, autorizado solo por el token) ---
        var portal = rutas.MapGroup("/portal").WithTags("Cartilla Viva (público)").AllowAnonymous();

        portal.MapGet("/{token}", CartillaAsync)
            .WithSummary("Cartilla Viva del dueño resuelta por el token (pública).");

        portal.MapPost("/{token}/citas/{citaId:guid}/confirmar", ConfirmarCitaAsync)
            .WithSummary("Confirma una cita desde la Cartilla Viva (pública, por token).");

        return rutas;
    }

    private static async Task<IResult> GenerarAsync(Guid clienteId, IContextoEmpresa contexto, GenerarAccesoPortal caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, clienteId, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> ObtenerAsync(Guid clienteId, ObtenerAccesoPortal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(clienteId, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> RevocarAsync(Guid clienteId, RevocarAccesoPortal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(clienteId, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> CartillaAsync(string token, ObtenerCartillaPorToken caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(token, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ConfirmarCitaAsync(string token, Guid citaId, ConfirmarCitaPorToken caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(token, citaId, ct).ConfigureAwait(false)).AOk();
}
