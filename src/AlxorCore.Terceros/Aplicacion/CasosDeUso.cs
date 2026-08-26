using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Terceros.Dominio;

namespace AlxorCore.Terceros.Aplicacion;

/// <summary>Datos de un cliente para crear o actualizar.</summary>
public sealed record DatosCliente(
    string Nombre,
    string? NifFiscal = null,
    string? Email = null,
    string? Calle = null,
    string? CodigoPostal = null,
    string? Poblacion = null,
    string? Provincia = null,
    string? Pais = null,
    decimal PorcentajeIrpfDefecto = 0m,
    bool RecargoEquivalencia = false,
    string? Iban = null,
    string? MandatoReferencia = null,
    DateOnly? MandatoFecha = null);

/// <summary>Caso de uso: crear un cliente en la empresa activa.</summary>
public sealed class CrearCliente
{
    private readonly IRepositorioClientes _clientes;
    private readonly IUnidadDeTrabajoTerceros _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearCliente(IRepositorioClientes clientes, IUnidadDeTrabajoTerceros unidadDeTrabajo, IReloj reloj)
    {
        _clientes = clientes;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ClienteDto>> EjecutarAsync(Guid empresaId, DatosCliente datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var direccion = Direccion.Crear(datos.Calle, datos.CodigoPostal, datos.Poblacion, datos.Provincia, datos.Pais);
        var cliente = Cliente.Crear(empresaId, datos.Nombre, datos.NifFiscal, datos.Email, direccion, datos.PorcentajeIrpfDefecto, _reloj, datos.RecargoEquivalencia, datos.Iban, datos.MandatoReferencia, datos.MandatoFecha);
        if (cliente.EsFallo)
        {
            return Resultado.Fallo<ClienteDto>(cliente.Error);
        }

        _clientes.Agregar(cliente.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ClienteDto.Desde(cliente.Valor));
    }
}

/// <summary>Caso de uso: actualizar un cliente existente.</summary>
public sealed class ActualizarCliente
{
    private readonly IRepositorioClientes _clientes;
    private readonly IUnidadDeTrabajoTerceros _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarCliente(IRepositorioClientes clientes, IUnidadDeTrabajoTerceros unidadDeTrabajo, IReloj reloj)
    {
        _clientes = clientes;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ClienteDto>> EjecutarAsync(Guid clienteId, DatosCliente datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var cliente = await _clientes.ObtenerPorIdAsync(clienteId, ct).ConfigureAwait(false);
        if (cliente is null)
        {
            return Resultado.Fallo<ClienteDto>(Error.NoEncontrado("cliente.no_encontrado", "El cliente no existe."));
        }

        var direccion = Direccion.Crear(datos.Calle, datos.CodigoPostal, datos.Poblacion, datos.Provincia, datos.Pais);
        var actualizado = cliente.Actualizar(datos.Nombre, datos.NifFiscal, datos.Email, direccion, datos.PorcentajeIrpfDefecto, _reloj, datos.RecargoEquivalencia, datos.Iban, datos.MandatoReferencia, datos.MandatoFecha);
        if (actualizado.EsFallo)
        {
            return Resultado.Fallo<ClienteDto>(actualizado.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ClienteDto.Desde(cliente));
    }
}

/// <summary>Caso de uso: dar de baja (baja lógica) un cliente. No borra: lo marca inactivo.</summary>
public sealed class DesactivarCliente
{
    private readonly IRepositorioClientes _clientes;
    private readonly IUnidadDeTrabajoTerceros _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public DesactivarCliente(IRepositorioClientes clientes, IUnidadDeTrabajoTerceros unidadDeTrabajo, IReloj reloj)
    {
        _clientes = clientes;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await _clientes.ObtenerPorIdAsync(clienteId, ct).ConfigureAwait(false);
        if (cliente is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("cliente.no_encontrado", "El cliente no existe."));
        }

        cliente.Desactivar(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: listar los clientes de la empresa activa.</summary>
public sealed class ListarClientes
{
    private readonly IConsultaClientes _consulta;

    public ListarClientes(IConsultaClientes consulta) => _consulta = consulta;

    public Task<IReadOnlyList<ClienteDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, incluirInactivos: false, ct);
}

/// <summary>Caso de uso: obtener un cliente por su identificador.</summary>
public sealed class ObtenerCliente
{
    private readonly IConsultaClientes _consulta;

    public ObtenerCliente(IConsultaClientes consulta) => _consulta = consulta;

    public async Task<Resultado<ClienteDto>> EjecutarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await _consulta.ObtenerAsync(clienteId, ct).ConfigureAwait(false);
        return cliente is null
            ? Resultado.Fallo<ClienteDto>(Error.NoEncontrado("cliente.no_encontrado", "El cliente no existe."))
            : Resultado.Ok(cliente);
    }
}
