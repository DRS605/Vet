using AlxorCore.Api.Comun;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Terceros (clientes).</summary>
public static class EndpointsTerceros
{
    public static IEndpointRouteBuilder MapearTerceros(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var clientes = rutas.MapGroup("/clientes").WithTags("Clientes");

        clientes.MapGet("", ListarAsync)
            .WithSummary("Lista los clientes de la empresa activa.")
            .RequireAuthorization();

        clientes.MapGet("/{id:guid}", ObtenerAsync)
            .WithSummary("Obtiene un cliente.")
            .RequireAuthorization();

        clientes.MapPost("", CrearAsync)
            .WithSummary("Crea un cliente.")
            .RequierePermiso(Permisos.ClienteGestionar);

        clientes.MapPut("/{id:guid}", ActualizarAsync)
            .WithSummary("Actualiza un cliente.")
            .RequierePermiso(Permisos.ClienteGestionar);

        clientes.MapDelete("/{id:guid}", DarDeBajaAsync)
            .WithSummary("Da de baja (baja lógica) un cliente.")
            .RequierePermiso(Permisos.ClienteGestionar);

        clientes.MapPost("/importar", ImportarClientesAsync)
            .WithSummary("Importa clientes desde CSV (previsualiza o confirma).")
            .RequierePermiso(Permisos.ClienteGestionar);

        var proveedores = rutas.MapGroup("/proveedores").WithTags("Proveedores");

        proveedores.MapGet("", ListarProvAsync)
            .WithSummary("Lista los proveedores de la empresa activa.")
            .RequireAuthorization();

        proveedores.MapGet("/{id:guid}", ObtenerProvAsync)
            .WithSummary("Obtiene un proveedor.")
            .RequireAuthorization();

        proveedores.MapPost("", CrearProvAsync)
            .WithSummary("Crea un proveedor.")
            .RequierePermiso(Permisos.GastoGestionar);

        proveedores.MapPut("/{id:guid}", ActualizarProvAsync)
            .WithSummary("Actualiza un proveedor.")
            .RequierePermiso(Permisos.GastoGestionar);

        return rutas;
    }

    private static async Task<IResult> ListarProvAsync(IContextoEmpresa contexto, ListarProveedores caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerProvAsync(Guid id, ObtenerProveedor caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CrearProvAsync(DatosProveedor datos, IContextoEmpresa contexto, CrearProveedor caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/proveedores/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarProvAsync(Guid id, DatosProveedor datos, ActualizarProveedor caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ListarAsync(IContextoEmpresa contexto, ListarClientes caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ObtenerCliente caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CrearAsync(DatosCliente datos, IContextoEmpresa contexto, CrearCliente caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/clientes/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ImportarClientesAsync(ImportarCsvPeticion peticion, IContextoEmpresa contexto, ImportarClientes caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var filas = new List<FilaImportacionCliente>();
        foreach (var fila in LectorCsv.Parsear(peticion.Contenido ?? string.Empty))
        {
            var datos = new DatosCliente(
                Nombre: fila.Campo("nombre", "razon social", "cliente") ?? string.Empty,
                NifFiscal: fila.Campo("nif", "cif", "dni", "nif fiscal"),
                Email: fila.Campo("email", "correo", "e-mail"),
                Telefono: fila.Campo("telefono", "teléfono", "movil", "móvil", "tel"),
                Calle: fila.Campo("direccion", "calle"),
                CodigoPostal: fila.Campo("cp", "codigo postal"),
                Poblacion: fila.Campo("poblacion", "ciudad", "localidad"),
                Provincia: fila.Campo("provincia"),
                PorcentajeIrpfDefecto: ImportacionCsv.Numero(fila.Campo("irpf")));
            filas.Add(new FilaImportacionCliente(fila.Numero, datos));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, filas, peticion.Previsualizar, ct).ConfigureAwait(false);
        return Results.Ok(resultado);
    }

    private static async Task<IResult> ActualizarAsync(Guid id, DatosCliente datos, ActualizarCliente caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> DarDeBajaAsync(Guid id, DesactivarCliente caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();
}
