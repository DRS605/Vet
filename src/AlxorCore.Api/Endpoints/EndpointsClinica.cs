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

        animales.MapGet("/{animalId:guid}/consultas", ListarConsultasAsync)
            .WithSummary("Lista el historial clínico (consultas) de un animal.")
            .RequierePermiso(Permisos.ConsultaLeer);

        animales.MapPost("/{animalId:guid}/consultas", RegistrarConsultaAsync)
            .WithSummary("Registra una consulta en el historial de un animal.")
            .RequierePermiso(Permisos.ConsultaGestionar);

        var consultas = rutas.MapGroup("/consultas").WithTags("Consultas");

        consultas.MapGet("/{id:guid}", ObtenerConsultaAsync)
            .WithSummary("Obtiene una consulta.")
            .RequierePermiso(Permisos.ConsultaLeer);

        consultas.MapPut("/{id:guid}", ActualizarConsultaAsync)
            .WithSummary("Actualiza una consulta.")
            .RequierePermiso(Permisos.ConsultaGestionar);

        consultas.MapDelete("/{id:guid}", AnularConsultaAsync)
            .WithSummary("Anula (baja lógica) una consulta.")
            .RequierePermiso(Permisos.ConsultaGestionar);

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

    private static async Task<IResult> ListarConsultasAsync(Guid animalId, ListarConsultasDeAnimal caso, CancellationToken ct) =>
        Results.Ok(await caso.EjecutarAsync(animalId, ct).ConfigureAwait(false));

    private static async Task<IResult> RegistrarConsultaAsync(Guid animalId, DatosConsulta datos, IContextoEmpresa contexto, RegistrarConsulta caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        // La ruta es la fuente de verdad del animal atendido.
        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos with { AnimalId = animalId }, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/consultas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ObtenerConsultaAsync(Guid id, ObtenerConsulta caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ActualizarConsultaAsync(Guid id, DatosActualizarConsulta datos, ActualizarConsulta caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> AnularConsultaAsync(Guid id, AnularConsulta caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();
}
