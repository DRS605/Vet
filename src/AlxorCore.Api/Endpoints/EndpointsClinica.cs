using AlxorCore.Api.Comun;
using AlxorCore.Clinica.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Clínica (animales del producto veterinario).</summary>
public static class EndpointsClinica
{
    public static IEndpointRouteBuilder MapearClinica(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var animales = rutas.MapGroup("/animales").WithTags("Animales");

        animales.MapGet("", ListarAsync)
            .WithSummary("Lista los animales de la empresa activa.")
            .RequierePermiso(Permisos.AnimalLeer);

        animales.MapGet("/{id:guid}", ObtenerAsync)
            .WithSummary("Obtiene un animal.")
            .RequierePermiso(Permisos.AnimalLeer);

        animales.MapPost("", CrearAsync)
            .WithSummary("Crea un animal (mascota).")
            .RequierePermiso(Permisos.AnimalGestionar);

        animales.MapPut("/{id:guid}", ActualizarAsync)
            .WithSummary("Actualiza un animal.")
            .RequierePermiso(Permisos.AnimalGestionar);

        animales.MapDelete("/{id:guid}", DarDeBajaAsync)
            .WithSummary("Da de baja (baja lógica) un animal.")
            .RequierePermiso(Permisos.AnimalGestionar);

        var clientes = rutas.MapGroup("/clientes").WithTags("Animales");

        clientes.MapGet("/{clienteId:guid}/animales", ListarPorClienteAsync)
            .WithSummary("Lista los animales de un cliente.")
            .RequierePermiso(Permisos.AnimalLeer);

        return rutas;
    }

    private static async Task<IResult> ListarAsync(IContextoEmpresa contexto, ListarAnimales caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ObtenerAnimal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CrearAsync(DatosAnimal datos, IContextoEmpresa contexto, CrearAnimal caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/animales/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarAsync(Guid id, DatosActualizarAnimal datos, ActualizarAnimal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> DarDeBajaAsync(Guid id, DarDeBajaAnimal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarPorClienteAsync(Guid clienteId, ListarAnimalesDeCliente caso, CancellationToken ct) =>
        Results.Ok(await caso.EjecutarAsync(clienteId, ct).ConfigureAwait(false));
}
