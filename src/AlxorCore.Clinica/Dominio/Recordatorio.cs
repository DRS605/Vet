using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Naturaleza del recordatorio (qué le toca al animal).</summary>
public enum TipoRecordatorio
{
    /// <summary>Una dosis de vacuna que vence.</summary>
    Vacuna,

    /// <summary>Una revisión posquirúrgica.</summary>
    Revision,

    /// <summary>Un tratamiento (desparasitación, medicación, etc.).</summary>
    Tratamiento,

    /// <summary>Un seguimiento quirúrgico genérico.</summary>
    Cirugia,

    /// <summary>Cualquier otro aviso manual.</summary>
    Otro,
}

/// <summary>Estado del ciclo de vida de un recordatorio.</summary>
public enum EstadoRecordatorio
{
    /// <summary>Preparado, aún sin enviar.</summary>
    Pendiente,

    /// <summary>Enviado por correo al propietario.</summary>
    Enviado,

    /// <summary>Atendido: el animal ha acudido o se ha resuelto.</summary>
    Completado,

    /// <summary>Descartado: ya no procede avisar.</summary>
    Cancelado,
}

/// <summary>Se ha creado un recordatorio para un animal.</summary>
public sealed record RecordatorioCreado(Guid RecordatorioId, Guid EmpresaId, Guid AnimalId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Recordatorio: un <b>aviso</b> asociado a un <see cref="AnimalId">animal</see> sobre algo que vence
/// o que hay que hacer (una vacuna, una revisión, un tratamiento…). Es la sexta raíz de agregado del
/// producto veterinario y cuelga del animal (solo guarda su identificador, sin clave foránea entre
/// esquemas). No se envía por temporizador: la clínica los prepara y decide cuándo enviarlos por
/// correo (uno a uno o «enviar pendientes»). Puede nacer de un vencimiento del historial
/// (<see cref="ReferenciaTipo"/> + <see cref="ReferenciaId"/> permiten deduplicar) o ser manual.
/// </summary>
public sealed class Recordatorio : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaTitulo = 200;
    public const int LongitudMaximaNotas = 1000;
    public const int LongitudMaximaReferenciaTipo = 40;

    private Recordatorio(Guid id)
        : base(id, Guid.Empty)
    {
        Titulo = null!;
    }

    private Recordatorio(
        Guid id,
        Guid empresaId,
        Guid animalId,
        TipoRecordatorio tipo,
        string titulo,
        DateOnly fechaObjetivo,
        string? notas,
        string? referenciaTipo,
        Guid? referenciaId,
        DateTimeOffset ahora)
        : base(id, empresaId)
    {
        AnimalId = animalId;
        Tipo = tipo;
        Titulo = titulo;
        FechaObjetivo = fechaObjetivo;
        Notas = notas;
        ReferenciaTipo = referenciaTipo;
        ReferenciaId = referenciaId;
        Estado = EstadoRecordatorio.Pendiente;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Animal al que se refiere el aviso. Se guarda solo el identificador (sin FK entre esquemas).</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>Naturaleza del recordatorio.</summary>
    public TipoRecordatorio Tipo { get; private set; }

    /// <summary>Asunto legible (p. ej. «Vacuna polivalente de Nala»). Obligatorio, máx. 200.</summary>
    public string Titulo { get; private set; }

    /// <summary>Fecha en que vence o toca. Obligatoria.</summary>
    public DateOnly FechaObjetivo { get; private set; }

    /// <summary>Notas internas. Opcional, máx. 1000.</summary>
    public string? Notas { get; private set; }

    /// <summary>Tipo de origen para deduplicar (p. ej. «vacunacion»). Opcional, máx. 40. Los manuales van sin referencia.</summary>
    public string? ReferenciaTipo { get; private set; }

    /// <summary>Identificador del origen para deduplicar (p. ej. id de la vacunación). Opcional.</summary>
    public Guid? ReferenciaId { get; private set; }

    /// <summary>Estado del ciclo de vida. Empieza en <see cref="EstadoRecordatorio.Pendiente"/>.</summary>
    public EstadoRecordatorio Estado { get; private set; }

    /// <summary>Momento del envío por correo. Se fija al marcar enviado.</summary>
    public DateTimeOffset? FechaEnvio { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Recordatorio> Crear(
        Guid empresaId,
        Guid animalId,
        TipoRecordatorio tipo,
        string? titulo,
        DateOnly fechaObjetivo,
        IReloj reloj,
        string? notas = null,
        string? referenciaTipo = null,
        Guid? referenciaId = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(animalId, tipo, titulo, notas);
        if (error is not null)
        {
            return Resultado.Fallo<Recordatorio>(error);
        }

        var recordatorio = new Recordatorio(
            Guid.NewGuid(), empresaId, animalId, tipo, titulo!.Trim(), fechaObjetivo,
            Normalizar(notas), Normalizar(referenciaTipo), referenciaId, reloj.AhoraUtc);
        recordatorio.RegistrarEvento(new RecordatorioCreado(recordatorio.Id, empresaId, animalId, reloj.AhoraUtc));
        return Resultado.Ok(recordatorio);
    }

    /// <summary>Actualiza el asunto, la fecha objetivo y las notas (el animal, el tipo y la referencia no cambian).</summary>
    public Resultado Actualizar(string? titulo, DateOnly fechaObjetivo, string? notas, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(AnimalId, Tipo, titulo, notas);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Titulo = titulo!.Trim();
        FechaObjetivo = fechaObjetivo;
        Notas = Normalizar(notas);
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Marca el recordatorio como enviado y fija la fecha de envío. Solo es válido desde <see cref="EstadoRecordatorio.Pendiente"/>.</summary>
    public Resultado MarcarEnviado(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoRecordatorio.Pendiente)
        {
            return Resultado.Fallo(Error.Conflicto("recordatorio.no_enviable", "Solo un recordatorio pendiente puede enviarse."));
        }

        Estado = EstadoRecordatorio.Enviado;
        FechaEnvio = reloj.AhoraUtc;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Marca el recordatorio como completado (atendido). Válido desde pendiente o enviado.</summary>
    public Resultado MarcarCompletado(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado is not (EstadoRecordatorio.Pendiente or EstadoRecordatorio.Enviado))
        {
            return Resultado.Fallo(Error.Conflicto("recordatorio.no_completable", "Solo un recordatorio pendiente o enviado puede completarse."));
        }

        Estado = EstadoRecordatorio.Completado;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Cancela el recordatorio (deja de proceder). Válido desde pendiente o enviado.</summary>
    public Resultado Cancelar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado is not (EstadoRecordatorio.Pendiente or EstadoRecordatorio.Enviado))
        {
            return Resultado.Fallo(Error.Conflicto("recordatorio.no_cancelable", "Solo un recordatorio pendiente o enviado puede cancelarse."));
        }

        Estado = EstadoRecordatorio.Cancelado;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    private static Error? Validar(Guid animalId, TipoRecordatorio tipo, string? titulo, string? notas)
    {
        if (animalId == Guid.Empty)
        {
            return Error.Validacion("recordatorio.animal_obligatorio", "El recordatorio debe estar asociado a un animal.");
        }

        if (!Enum.IsDefined(tipo))
        {
            return Error.Validacion("recordatorio.tipo_invalido", "El tipo de recordatorio no es válido.");
        }

        if (string.IsNullOrWhiteSpace(titulo))
        {
            return Error.Validacion("recordatorio.titulo_vacio", "El título del recordatorio es obligatorio.");
        }

        if (titulo.Trim().Length > LongitudMaximaTitulo)
        {
            return Error.Validacion("recordatorio.titulo_largo", "El título del recordatorio es demasiado largo.");
        }

        if (notas is not null && notas.Trim().Length > LongitudMaximaNotas)
        {
            return Error.Validacion("recordatorio.notas_largas", "Las notas son demasiado largas.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
