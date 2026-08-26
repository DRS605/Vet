using AlxorCore.Api.Comun;
using AlxorCore.Api.Contratos;
using AlxorCore.Clinica.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Organizacion.Aplicacion.CasosDeUso;
using System.Security.Claims;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Organización (empresas y series).</summary>
public static class EndpointsOrganizacion
{
    public static IEndpointRouteBuilder MapearOrganizacion(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        rutas.MapGet("/estado-instalacion", EstadoInstalacionAsync)
            .WithTags("Instalación")
            .WithSummary("Indica si la instalación ya está inicializada (existe alguna empresa). Público.")
            .AllowAnonymous();

        var empresas = rutas.MapGroup("/empresas").WithTags("Empresas");

        empresas.MapPost("", CrearAsync)
            .WithSummary("Crea una empresa; el usuario pasa a ser su propietario.")
            .RequireAuthorization();

        empresas.MapGet("", ListarMiasAsync)
            .WithSummary("Lista las empresas del usuario autenticado.")
            .RequireAuthorization();

        empresas.MapPost("/{empresaId:guid}/seleccionar", SeleccionarAsync)
            .WithSummary("Selecciona la empresa activa y devuelve un token con su alcance.")
            .RequireAuthorization();

        empresas.MapGet("/actual", ActualAsync)
            .WithSummary("Devuelve la empresa activa.")
            .RequireAuthorization();

        empresas.MapPut("/actual", ActualizarAsync)
            .WithSummary("Actualiza los datos maestros de la empresa activa (NIF, razón social, dirección e IVA).")
            .RequierePermiso(Permisos.EmpresaAjustes);

        empresas.MapPut("/actual/cobro", DatosCobroAsync)
            .WithSummary("Fija los datos de cobro por domiciliación (IBAN e identificador del acreedor SEPA).")
            .RequierePermiso(Permisos.EmpresaAjustes);

        var series = rutas.MapGroup("/series").WithTags("Series");

        series.MapGet("", ListarSeriesAsync)
            .WithSummary("Lista las series de la empresa activa.")
            .RequireAuthorization();

        series.MapPost("", CrearSerieAsync)
            .WithSummary("Crea una serie de numeración.")
            .RequierePermiso(Permisos.EmpresaAjustes);

        return rutas;
    }

    private static async Task<IResult> EstadoInstalacionAsync(ConsultarEstadoInstalacion caso, CancellationToken ct)
    {
        var inicializada = await caso.EstaInicializadaAsync(ct).ConfigureAwait(false);
        return Results.Ok(new { inicializada });
    }

    private static async Task<IResult> CrearAsync(
        CrearEmpresaPeticion peticion,
        ClaimsPrincipal usuario,
        CrearEmpresa caso,
        SembrarEspeciesPorDefecto sembrarEspecies,
        IContextoEmpresaMutable contextoEmpresa,
        CancellationToken ct)
    {
        var usuarioId = usuario.ObtenerUsuarioId();
        if (usuarioId is null)
        {
            return ResultadosHttp.AProblema(Error.NoAutenticado("auth.token_invalido", "El token no identifica al usuario."));
        }

        var comando = new CrearEmpresaComando(
            usuarioId.Value, peticion.Nif, peticion.RazonSocial,
            peticion.Calle, peticion.CodigoPostal, peticion.Poblacion, peticion.Provincia, peticion.RegimenIva);

        var resultado = await caso.EjecutarAsync(comando, ct).ConfigureAwait(false);
        if (resultado.EsFallo)
        {
            return ResultadosHttp.AProblema(resultado.Error);
        }

        // Toda clínica nueva arranca con el maestro de especies por defecto (Perro, Gato, Conejo, Ave,
        // Hurón, Reptil, Otro). Se fija el contexto a la empresa recién creada para que el aislamiento
        // (filtro de EF + RLS) apunte a ella, ya que el usuario aún no la ha «seleccionado».
        contextoEmpresa.Fijar(resultado.Valor.Id);
        await sembrarEspecies.EjecutarAsync(resultado.Valor.Id, ct).ConfigureAwait(false);

        return resultado.ACreado("/empresas/actual");
    }

    private static async Task<IResult> ListarMiasAsync(ClaimsPrincipal usuario, ListarMisEmpresas caso, CancellationToken ct)
    {
        var usuarioId = usuario.ObtenerUsuarioId();
        if (usuarioId is null)
        {
            return ResultadosHttp.AProblema(Error.NoAutenticado("auth.token_invalido", "El token no identifica al usuario."));
        }

        var empresas = await caso.EjecutarAsync(usuarioId.Value, ct).ConfigureAwait(false);
        return Results.Ok(empresas);
    }

    private static async Task<IResult> SeleccionarAsync(Guid empresaId, ClaimsPrincipal usuario, SeleccionarEmpresa caso, CancellationToken ct)
    {
        var identidad = usuario.ObtenerIdentidad();
        if (identidad is null)
        {
            return ResultadosHttp.AProblema(Error.NoAutenticado("auth.token_invalido", "El token no identifica al usuario."));
        }

        var resultado = await caso.EjecutarAsync(identidad, empresaId, ct).ConfigureAwait(false);
        return resultado.AOk();
    }

    private static async Task<IResult> ActualAsync(IContextoEmpresa contexto, ObtenerEmpresa caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false);
        return resultado.AOk();
    }

    private static async Task<IResult> ActualizarAsync(ActualizarEmpresaPeticion peticion, IContextoEmpresa contexto, ActualizarEmpresa caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var comando = new ActualizarEmpresaComando(
            peticion.Nif, peticion.RazonSocial,
            peticion.Calle, peticion.CodigoPostal, peticion.Poblacion, peticion.Provincia, peticion.RegimenIva);

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, comando, ct).ConfigureAwait(false);
        return resultado.AOk();
    }

    private static async Task<IResult> DatosCobroAsync(DatosCobroComando comando, IContextoEmpresa contexto, ActualizarDatosCobro caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, comando, ct).ConfigureAwait(false);
        return resultado.AOk();
    }

    private static async Task<IResult> ListarSeriesAsync(IContextoEmpresa contexto, ListarSeries caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var series = await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false);
        return Results.Ok(series);
    }

    private static async Task<IResult> CrearSerieAsync(CrearSeriePeticion peticion, IContextoEmpresa contexto, CrearSerie caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var comando = new CrearSerieComando(peticion.TipoDocumento, peticion.Ejercicio, peticion.Prefijo);
        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, comando, ct).ConfigureAwait(false);
        return resultado.AOk();
    }
}
