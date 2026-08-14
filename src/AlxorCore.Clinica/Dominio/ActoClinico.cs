using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Estado del ciclo de vida de un acto clínico (línea facturable).</summary>
public enum EstadoActo
{
    /// <summary>Registrado, aún sin cobrar ni facturar.</summary>
    Pendiente,

    /// <summary>Cobrado con ticket (fuera del flujo de factura VeriFactu).</summary>
    Ticket,

    /// <summary>Incluido en una factura emitida.</summary>
    Facturado,

    /// <summary>Descartado: ya no procede cobrarlo ni facturarlo.</summary>
    Anulado,
}

/// <summary>Se ha registrado un acto clínico facturable para un animal.</summary>
public sealed record ActoClinicoRegistrado(Guid ActoId, Guid EmpresaId, Guid AnimalId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Acto clínico: la <b>línea facturable</b> del producto veterinario. Registra que a un
/// <see cref="AnimalId">animal</see> se le ha prestado un servicio con un <see cref="Concepto"/> y un
/// <see cref="Importe"/> (base, sin IVA), guardando el <see cref="ClienteId">propietario a facturar</see>
/// resuelto del animal en el momento del alta (snapshot). Es la octava raíz de agregado del producto
/// veterinario y cuelga del animal (solo guarda su identificador, sin clave foránea entre esquemas).
///
/// <para>Concepto clave: el acto se registra <b>siempre</b>; facturar es un paso aparte y opcional. Un
/// acto nace <see cref="EstadoActo.Pendiente"/> y puede: cobrarse con ticket
/// (<see cref="MarcarTicket"/> → <see cref="EstadoActo.Ticket"/>), incluirse en una factura VeriFactu
/// (<see cref="MarcarFacturado"/> → <see cref="EstadoActo.Facturado"/>, guardando el
/// <see cref="FacturaId"/>) o anularse (<see cref="Anular"/>). Los estados no <c>Pendiente</c> son
/// finales: cualquier otra transición devuelve <see cref="Error"/> de conflicto. La <b>emisión de la
/// factura</b> reutiliza el módulo Facturación (numeración correlativa, IVA y VeriFactu); aquí solo se
/// guarda el resultado.</para>
/// </summary>
public sealed class ActoClinico : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaConcepto = 200;
    public const int LongitudMaximaReferenciaTipo = 40;

    /// <summary>IVA estándar (21 %) que se aplica si no se indica otro.</summary>
    public const decimal IvaPorDefecto = 21m;

    private static readonly decimal[] IvasValidos = { 0m, 4m, 10m, 21m };

    private ActoClinico(Guid id)
        : base(id, Guid.Empty)
    {
        Concepto = null!;
    }

    private ActoClinico(
        Guid id,
        Guid empresaId,
        Guid animalId,
        Guid clienteId,
        DateOnly fecha,
        string concepto,
        decimal importe,
        decimal porcentajeIva,
        string? referenciaTipo,
        Guid? referenciaId,
        DateTimeOffset ahora)
        : base(id, empresaId)
    {
        AnimalId = animalId;
        ClienteId = clienteId;
        Fecha = fecha;
        Concepto = concepto;
        Importe = importe;
        PorcentajeIva = porcentajeIva;
        ReferenciaTipo = referenciaTipo;
        ReferenciaId = referenciaId;
        Estado = EstadoActo.Pendiente;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Animal atendido. Se guarda solo el identificador (sin FK entre esquemas).</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>Propietario a facturar. Se resuelve del animal al crear y se guarda (snapshot).</summary>
    public Guid ClienteId { get; private set; }

    /// <summary>Fecha del acto. Obligatoria.</summary>
    public DateOnly Fecha { get; private set; }

    /// <summary>Descripción del servicio (p. ej. «Consulta + vacuna polivalente»). Obligatorio, máx. 200.</summary>
    public string Concepto { get; private set; }

    /// <summary>Precio <b>base</b> (sin IVA) por el que se facturará. Obligatorio, ≥ 0. En EUR.</summary>
    public decimal Importe { get; private set; }

    /// <summary>Porcentaje de IVA a aplicar al facturar (0/4/10/21). Por defecto 21.</summary>
    public decimal PorcentajeIva { get; private set; }

    /// <summary>Tipo de origen del acto (p. ej. «consulta»/«vacunacion»/«cirugia»). Opcional, máx. 40.</summary>
    public string? ReferenciaTipo { get; private set; }

    /// <summary>Identificador del origen. Opcional.</summary>
    public Guid? ReferenciaId { get; private set; }

    /// <summary>Estado del ciclo de vida. Empieza en <see cref="EstadoActo.Pendiente"/>.</summary>
    public EstadoActo Estado { get; private set; }

    /// <summary>Factura en la que se incluyó el acto. Se fija al facturar.</summary>
    public Guid? FacturaId { get; private set; }

    /// <summary>Momento en que se cobró con ticket. Se fija al marcar ticket.</summary>
    public DateTimeOffset? CobradoTicketEn { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<ActoClinico> Crear(
        Guid empresaId,
        Guid animalId,
        Guid clienteId,
        DateOnly fecha,
        string? concepto,
        decimal importe,
        IReloj reloj,
        decimal? porcentajeIva = null,
        string? referenciaTipo = null,
        Guid? referenciaId = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var iva = porcentajeIva ?? IvaPorDefecto;
        var error = Validar(animalId, clienteId, concepto, importe, iva, referenciaTipo);
        if (error is not null)
        {
            return Resultado.Fallo<ActoClinico>(error);
        }

        var acto = new ActoClinico(
            Guid.NewGuid(), empresaId, animalId, clienteId, fecha, concepto!.Trim(), importe, iva,
            Normalizar(referenciaTipo), referenciaId, reloj.AhoraUtc);
        acto.RegistrarEvento(new ActoClinicoRegistrado(acto.Id, empresaId, animalId, reloj.AhoraUtc));
        return Resultado.Ok(acto);
    }

    /// <summary>Actualiza los datos del acto. Solo es válido mientras está <see cref="EstadoActo.Pendiente"/>.</summary>
    public Resultado Actualizar(DateOnly fecha, string? concepto, decimal importe, decimal? porcentajeIva, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoActo.Pendiente)
        {
            return TransicionInvalida("actualizar");
        }

        var iva = porcentajeIva ?? PorcentajeIva;
        var error = Validar(AnimalId, ClienteId, concepto, importe, iva, ReferenciaTipo);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Fecha = fecha;
        Concepto = concepto!.Trim();
        Importe = importe;
        PorcentajeIva = iva;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Cobra el acto con ticket. Solo es válido desde <see cref="EstadoActo.Pendiente"/>.</summary>
    public Resultado MarcarTicket(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoActo.Pendiente)
        {
            return TransicionInvalida("cobrar con ticket");
        }

        Estado = EstadoActo.Ticket;
        CobradoTicketEn = reloj.AhoraUtc;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Marca el acto como facturado y guarda la factura. Solo es válido desde <see cref="EstadoActo.Pendiente"/>.</summary>
    public Resultado MarcarFacturado(Guid facturaId, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (facturaId == Guid.Empty)
        {
            return Resultado.Fallo(Error.Validacion("acto.factura_obligatoria", "La factura del acto es obligatoria."));
        }

        if (Estado != EstadoActo.Pendiente)
        {
            return TransicionInvalida("facturar");
        }

        Estado = EstadoActo.Facturado;
        FacturaId = facturaId;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Anula el acto. Solo es válido desde <see cref="EstadoActo.Pendiente"/>.</summary>
    public Resultado Anular(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoActo.Pendiente)
        {
            return TransicionInvalida("anular");
        }

        Estado = EstadoActo.Anulado;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    private Resultado TransicionInvalida(string accion) =>
        Resultado.Fallo(Error.Conflicto("acto.transicion_invalida", $"No se puede {accion} un acto clínico en estado «{Estado}»."));

    private static Error? Validar(Guid animalId, Guid clienteId, string? concepto, decimal importe, decimal porcentajeIva, string? referenciaTipo)
    {
        if (animalId == Guid.Empty)
        {
            return Error.Validacion("acto.animal_obligatorio", "El acto clínico debe estar asociado a un animal.");
        }

        if (clienteId == Guid.Empty)
        {
            return Error.Validacion("acto.cliente_obligatorio", "El acto clínico debe tener un cliente a facturar.");
        }

        if (string.IsNullOrWhiteSpace(concepto))
        {
            return Error.Validacion("acto.concepto_vacio", "El concepto del acto es obligatorio.");
        }

        if (concepto.Trim().Length > LongitudMaximaConcepto)
        {
            return Error.Validacion("acto.concepto_largo", "El concepto del acto es demasiado largo.");
        }

        if (importe < 0m)
        {
            return Error.Validacion("acto.importe_negativo", "El importe del acto no puede ser negativo.");
        }

        if (Array.IndexOf(IvasValidos, porcentajeIva) < 0)
        {
            return Error.Validacion("acto.iva_invalido", "El porcentaje de IVA debe ser 0, 4, 10 o 21.");
        }

        if (referenciaTipo is not null && referenciaTipo.Trim().Length > LongitudMaximaReferenciaTipo)
        {
            return Error.Validacion("acto.referencia_tipo_largo", "El tipo de referencia es demasiado largo.");
        }

        return null;
    }

    /// <summary>Código de impuesto del catálogo nacional (<c>IVA21</c>, <c>IVA10</c>…) para el porcentaje del acto.</summary>
    public string CodigoIva() => Impuesto.TodosIva.First(i => i.Porcentaje == PorcentajeIva).Codigo;

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
