using AlxorCore.Clinica.Dominio;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>Datos de un acto clínico (línea facturable) para registrar o actualizar.</summary>
public sealed record DatosActoClinico(
    Guid AnimalId,
    string Concepto,
    decimal Importe,
    decimal? PorcentajeIva = null,
    DateOnly? Fecha = null,
    string? ReferenciaTipo = null,
    Guid? ReferenciaId = null);

/// <summary>
/// Caso de uso: registrar un acto clínico facturable de un animal de la empresa activa. Verifica que
/// el animal existe en la empresa (vía <see cref="IConsultaAnimales"/>) y resuelve del animal el
/// <c>ClienteId</c> del propietario, que se guarda como snapshot en el acto. El acto nace
/// <see cref="EstadoActo.Pendiente"/>; facturarlo o cobrarlo con ticket es un paso aparte.
/// </summary>
public sealed class RegistrarActoClinico
{
    private readonly IRepositorioActosClinicos _actos;
    private readonly IConsultaAnimales _animales;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegistrarActoClinico(IRepositorioActosClinicos actos, IConsultaAnimales animales, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _actos = actos;
        _animales = animales;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ActoClinicoDto>> EjecutarAsync(Guid empresaId, DatosActoClinico datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // El filtro multiempresa de EF Core garantiza que solo se encuentra el animal si pertenece a la empresa activa.
        var animal = await _animales.ObtenerAsync(datos.AnimalId, ct).ConfigureAwait(false);
        if (animal is null)
        {
            return Resultado.Fallo<ActoClinicoDto>(Error.Validacion("acto.animal_no_encontrado", "El animal no existe en esta empresa."));
        }

        var fecha = datos.Fecha ?? DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var acto = ActoClinico.Crear(
            empresaId, datos.AnimalId, animal.ClienteId, fecha, datos.Concepto, datos.Importe, _reloj,
            datos.PorcentajeIva, datos.ReferenciaTipo, datos.ReferenciaId);
        if (acto.EsFallo)
        {
            return Resultado.Fallo<ActoClinicoDto>(acto.Error);
        }

        _actos.Agregar(acto.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ActoClinicoDto.Desde(acto.Valor));
    }
}

/// <summary>Caso de uso: actualizar un acto clínico pendiente (el animal y el cliente no cambian).</summary>
public sealed class ActualizarActoClinico
{
    private readonly IRepositorioActosClinicos _actos;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarActoClinico(IRepositorioActosClinicos actos, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _actos = actos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ActoClinicoDto>> EjecutarAsync(Guid actoId, DatosActoClinico datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var acto = await _actos.ObtenerPorIdAsync(actoId, ct).ConfigureAwait(false);
        if (acto is null)
        {
            return Resultado.Fallo<ActoClinicoDto>(Error.NoEncontrado("acto.no_encontrado", "El acto clínico no existe."));
        }

        var fecha = datos.Fecha ?? acto.Fecha;
        var actualizado = acto.Actualizar(fecha, datos.Concepto, datos.Importe, datos.PorcentajeIva, _reloj);
        if (actualizado.EsFallo)
        {
            return Resultado.Fallo<ActoClinicoDto>(actualizado.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ActoClinicoDto.Desde(acto));
    }
}

/// <summary>Caso de uso: cobrar un acto con ticket (Pendiente → Ticket).</summary>
public sealed class MarcarActoTicket
{
    private readonly IRepositorioActosClinicos _actos;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public MarcarActoTicket(IRepositorioActosClinicos actos, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _actos = actos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ActoClinicoDto>> EjecutarAsync(Guid actoId, CancellationToken ct = default)
    {
        var acto = await _actos.ObtenerPorIdAsync(actoId, ct).ConfigureAwait(false);
        if (acto is null)
        {
            return Resultado.Fallo<ActoClinicoDto>(Error.NoEncontrado("acto.no_encontrado", "El acto clínico no existe."));
        }

        var marcado = acto.MarcarTicket(_reloj);
        if (marcado.EsFallo)
        {
            return Resultado.Fallo<ActoClinicoDto>(marcado.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ActoClinicoDto.Desde(acto));
    }
}

/// <summary>Caso de uso: anular un acto clínico pendiente.</summary>
public sealed class AnularActoClinico
{
    private readonly IRepositorioActosClinicos _actos;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AnularActoClinico(IRepositorioActosClinicos actos, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _actos = actos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid actoId, CancellationToken ct = default)
    {
        var acto = await _actos.ObtenerPorIdAsync(actoId, ct).ConfigureAwait(false);
        if (acto is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("acto.no_encontrado", "El acto clínico no existe."));
        }

        var anulado = acto.Anular(_reloj);
        if (anulado.EsFallo)
        {
            return Resultado.Fallo(anulado.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: obtener un acto clínico por su identificador.</summary>
public sealed class ObtenerActoClinico
{
    private readonly IConsultaActosClinicos _consulta;

    public ObtenerActoClinico(IConsultaActosClinicos consulta) => _consulta = consulta;

    public async Task<Resultado<ActoClinicoDto>> EjecutarAsync(Guid actoId, CancellationToken ct = default)
    {
        var acto = await _consulta.ObtenerAsync(actoId, ct).ConfigureAwait(false);
        return acto is null
            ? Resultado.Fallo<ActoClinicoDto>(Error.NoEncontrado("acto.no_encontrado", "El acto clínico no existe."))
            : Resultado.Ok(acto);
    }
}

/// <summary>Caso de uso: listar los actos de la empresa por estado (por defecto, los pendientes de facturar).</summary>
public sealed class ListarActosClinicos
{
    private readonly IConsultaActosClinicos _consulta;

    public ListarActosClinicos(IConsultaActosClinicos consulta) => _consulta = consulta;

    public Task<IReadOnlyList<ActoClinicoDto>> EjecutarAsync(Guid empresaId, EstadoActo? estado = null, CancellationToken ct = default) =>
        _consulta.ListarPorEstadoAsync(empresaId, estado ?? EstadoActo.Pendiente, ct);
}

/// <summary>Caso de uso: listar los actos clínicos de un animal.</summary>
public sealed class ListarActosDeAnimal
{
    private readonly IConsultaActosClinicos _consulta;

    public ListarActosDeAnimal(IConsultaActosClinicos consulta) => _consulta = consulta;

    public Task<IReadOnlyList<ActoClinicoDto>> EjecutarAsync(Guid animalId, CancellationToken ct = default) =>
        _consulta.ListarPorAnimalAsync(animalId, ct);
}

/// <summary>
/// Datos para facturar un lote de actos clínicos. Además de los <see cref="ActoIds"/> a incluir y marcar
/// como facturados, admite <see cref="Lineas"/> libres compuestas por la SPA (los actos precargados con su
/// importe ya editable + las líneas añadidas a mano o del maestro de conceptos) y un texto de
/// <see cref="Observaciones"/> para el pie de la factura. Si <see cref="Lineas"/> viene vacío, se compone
/// una línea por acto con su importe original.
/// </summary>
public sealed record FacturarActosComando(
    IReadOnlyList<Guid> ActoIds,
    IReadOnlyList<LineaComando>? Lineas = null,
    string? Observaciones = null);

/// <summary>
/// Caso de uso estrella del puente de facturación: emite <b>una única factura VeriFactu</b> a partir
/// de varios actos clínicos. Carga los actos indicados y valida que <b>todos existen</b>, están
/// <see cref="EstadoActo.Pendiente"/> y son del <b>mismo cliente</b> (si no, devuelve un
/// <see cref="Error"/> sin tocar nada). Construye la factura con <b>una línea por acto</b> (concepto,
/// cantidad 1, precio = importe base, IVA = porcentaje del acto) e <b>invoca el caso de uso
/// <see cref="EmitirFactura"/> del módulo Facturación</b>, que aporta la numeración correlativa, el
/// cálculo de IVA/IRPF y el registro VeriFactu. Solo si la emisión va bien marca cada acto como
/// <see cref="EstadoActo.Facturado"/> con el <c>FacturaId</c> devuelto y persiste el cambio.
/// </summary>
/// <remarks>
/// La factura es la <b>verdad fiscal</b>: se emite (y se confirma en la unidad de trabajo de
/// Facturación) <b>antes</b> de marcar los actos, de forma que si la emisión falla ningún acto queda a
/// medias. Marcar los actos ocurre después, en la unidad de trabajo de Clínica, siguiendo la misma
/// premisa que el resto del ERP (la factura ya emitida es irreversible; el efecto secundario —aquí,
/// enlazar los actos— se aplica a continuación). No se reimplementa numeración ni impuestos.
/// </remarks>
public sealed class FacturarActos
{
    private readonly IRepositorioActosClinicos _actos;
    private readonly EmitirFactura _emitirFactura;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public FacturarActos(IRepositorioActosClinicos actos, EmitirFactura emitirFactura, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _actos = actos;
        _emitirFactura = emitirFactura;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public Task<Resultado<FacturaDto>> EjecutarAsync(Guid empresaId, IReadOnlyList<Guid> actoIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actoIds);
        return EjecutarAsync(empresaId, new FacturarActosComando(actoIds), ct);
    }

    /// <summary>
    /// Factura los actos indicados admitiendo, además, <b>líneas libres añadidas</b> por el usuario y un
    /// campo <b>Observaciones</b>. Si el comando trae <see cref="FacturarActosComando.Lineas"/>, esas líneas
    /// (compuestas en la SPA a partir de los actos + las añadidas, con importes ya editados) son las de la
    /// factura; si no, se compone una línea por acto (comportamiento clásico). En ambos casos se valida que
    /// los actos existen, están pendientes y son del mismo cliente, y al emitir se marcan como facturados.
    /// </summary>
    public async Task<Resultado<FacturaDto>> EjecutarAsync(Guid empresaId, FacturarActosComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var ids = (comando.ActoIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return Resultado.Fallo<FacturaDto>(Error.Validacion("acto.facturar_sin_actos", "Indica al menos un acto a facturar."));
        }

        var actos = await _actos.ObtenerVariosAsync(ids, ct).ConfigureAwait(false);
        if (actos.Count != ids.Count)
        {
            return Resultado.Fallo<FacturaDto>(Error.NoEncontrado("acto.no_encontrado", "Alguno de los actos no existe en esta empresa."));
        }

        if (actos.Any(a => a.Estado != EstadoActo.Pendiente))
        {
            return Resultado.Fallo<FacturaDto>(Error.Conflicto("acto.no_facturable", "Solo se pueden facturar actos pendientes."));
        }

        var clienteId = actos[0].ClienteId;
        if (actos.Any(a => a.ClienteId != clienteId))
        {
            return Resultado.Fallo<FacturaDto>(Error.Validacion("acto.clientes_distintos", "Todos los actos de una factura deben ser del mismo cliente."));
        }

        // Si la SPA envía las líneas (actos precargados + añadidas, con importes ya editados), se usan tal
        // cual; si no, una línea por acto: concepto, cantidad 1, precio base = importe, IVA = del acto.
        var lineas = comando.Lineas is { Count: > 0 }
            ? comando.Lineas
            : actos
                .Select(a => new LineaComando(Cantidad: 1m, Descripcion: a.Concepto, PrecioUnitario: a.Importe, CodigoIva: a.CodigoIva()))
                .ToList();

        // Reutiliza el caso de uso de Facturación: numeración correlativa, IVA y VeriFactu.
        var factura = await _emitirFactura.EjecutarAsync(empresaId, new EmitirFacturaComando(clienteId, lineas, Observaciones: comando.Observaciones), ct).ConfigureAwait(false);
        if (factura.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(factura.Error);
        }

        foreach (var acto in actos)
        {
            var marcado = acto.MarcarFacturado(factura.Valor.Id, _reloj);
            if (marcado.EsFallo)
            {
                return Resultado.Fallo<FacturaDto>(marcado.Error);
            }
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(factura.Valor);
    }
}
