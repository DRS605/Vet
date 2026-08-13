using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Se ha registrado una cirugía (intervención quirúrgica) de un animal.</summary>
public sealed record CirugiaRegistrada(Guid CirugiaId, Guid EmpresaId, Guid AnimalId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Cirugía: una <b>intervención quirúrgica</b> realizada a un <see cref="AnimalId">animal</see>. Es la
/// quinta raíz de agregado del producto veterinario y cuelga del animal (solo guarda su identificador,
/// sin clave foránea entre esquemas). Cierra el historial clínico: junto a consultas y vacunaciones,
/// deja constancia de las operaciones. El historial no se borra físicamente: una cirugía se «anula»
/// con una baja lógica.
/// </summary>
public sealed class Cirugia : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 200;
    public const int LongitudMaximaDescripcion = 2000;
    public const int LongitudMaximaCirujano = 120;
    public const int LongitudMaximaAnestesia = 200;
    public const int LongitudMaximaComplicaciones = 2000;

    private Cirugia(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private Cirugia(
        Guid id,
        Guid empresaId,
        Guid animalId,
        DateOnly fecha,
        string nombre,
        string? descripcion,
        string? cirujano,
        string? anestesia,
        string? complicaciones,
        DateOnly? proximaRevision,
        DateTimeOffset ahora)
        : base(id, empresaId)
    {
        AnimalId = animalId;
        Fecha = fecha;
        Nombre = nombre;
        Descripcion = descripcion;
        Cirujano = cirujano;
        Anestesia = anestesia;
        Complicaciones = complicaciones;
        ProximaRevision = proximaRevision;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Animal intervenido. Se guarda solo el identificador (sin FK entre esquemas).</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>Fecha de la intervención. Obligatoria. No puede ser futura.</summary>
    public DateOnly Fecha { get; private set; }

    /// <summary>Procedimiento realizado (p. ej. «Esterilización (OVH)»). Obligatorio, máx. 200.</summary>
    public string Nombre { get; private set; }

    /// <summary>Detalle o notas de la intervención. Opcional, máx. 2000.</summary>
    public string? Descripcion { get; private set; }

    /// <summary>Cirujano que interviene (texto libre por ahora). Opcional, máx. 120.</summary>
    public string? Cirujano { get; private set; }

    /// <summary>Tipo o pauta de anestesia empleada. Opcional, máx. 200.</summary>
    public string? Anestesia { get; private set; }

    /// <summary>Complicaciones surgidas durante o tras la intervención. Opcional, máx. 2000.</summary>
    public string? Complicaciones { get; private set; }

    /// <summary>Fecha de la próxima revisión (p. ej. retirada de puntos). Opcional; si se indica, no puede ser anterior a <see cref="Fecha"/>.</summary>
    public DateOnly? ProximaRevision { get; private set; }

    /// <summary>Baja lógica: una cirugía anulada deja de aparecer en el historial, pero no se borra.</summary>
    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Cirugia> Crear(
        Guid empresaId,
        Guid animalId,
        DateOnly fecha,
        string? nombre,
        IReloj reloj,
        string? descripcion = null,
        string? cirujano = null,
        string? anestesia = null,
        string? complicaciones = null,
        DateOnly? proximaRevision = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(animalId, fecha, nombre, descripcion, cirujano, anestesia, complicaciones, proximaRevision, reloj);
        if (error is not null)
        {
            return Resultado.Fallo<Cirugia>(error);
        }

        var cirugia = new Cirugia(
            Guid.NewGuid(), empresaId, animalId, fecha, nombre!.Trim(), Normalizar(descripcion),
            Normalizar(cirujano), Normalizar(anestesia), Normalizar(complicaciones), proximaRevision, reloj.AhoraUtc);
        cirugia.RegistrarEvento(new CirugiaRegistrada(cirugia.Id, empresaId, animalId, reloj.AhoraUtc));
        return Resultado.Ok(cirugia);
    }

    public Resultado Actualizar(
        DateOnly fecha,
        string? nombre,
        IReloj reloj,
        string? descripcion = null,
        string? cirujano = null,
        string? anestesia = null,
        string? complicaciones = null,
        DateOnly? proximaRevision = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(AnimalId, fecha, nombre, descripcion, cirujano, anestesia, complicaciones, proximaRevision, reloj);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Fecha = fecha;
        Nombre = nombre!.Trim();
        Descripcion = Normalizar(descripcion);
        Cirujano = Normalizar(cirujano);
        Anestesia = Normalizar(anestesia);
        Complicaciones = Normalizar(complicaciones);
        ProximaRevision = proximaRevision;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Anula la cirugía (baja lógica): deja de aparecer en el historial, pero no se borra.</summary>
    public void Anular(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static Error? Validar(
        Guid animalId,
        DateOnly fecha,
        string? nombre,
        string? descripcion,
        string? cirujano,
        string? anestesia,
        string? complicaciones,
        DateOnly? proximaRevision,
        IReloj reloj)
    {
        if (animalId == Guid.Empty)
        {
            return Error.Validacion("cirugia.animal_obligatorio", "La cirugía debe estar asociada a un animal.");
        }

        if (fecha > DateOnly.FromDateTime(reloj.AhoraUtc.UtcDateTime))
        {
            return Error.Validacion("cirugia.fecha_futura", "La fecha de la cirugía no puede ser futura.");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("cirugia.nombre_vacio", "El nombre del procedimiento es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("cirugia.nombre_largo", "El nombre del procedimiento es demasiado largo.");
        }

        if (descripcion is not null && descripcion.Trim().Length > LongitudMaximaDescripcion)
        {
            return Error.Validacion("cirugia.descripcion_larga", "La descripción es demasiado larga.");
        }

        if (cirujano is not null && cirujano.Trim().Length > LongitudMaximaCirujano)
        {
            return Error.Validacion("cirugia.cirujano_largo", "El nombre del cirujano es demasiado largo.");
        }

        if (anestesia is not null && anestesia.Trim().Length > LongitudMaximaAnestesia)
        {
            return Error.Validacion("cirugia.anestesia_larga", "La anestesia es demasiado larga.");
        }

        if (complicaciones is not null && complicaciones.Trim().Length > LongitudMaximaComplicaciones)
        {
            return Error.Validacion("cirugia.complicaciones_largas", "Las complicaciones son demasiado largas.");
        }

        if (proximaRevision is { } revision && revision < fecha)
        {
            return Error.Validacion("cirugia.revision_anterior_a_fecha", "La próxima revisión no puede ser anterior a la fecha de la cirugía.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
