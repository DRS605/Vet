using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Naturaleza de una cita de la agenda.</summary>
public enum TipoCita
{
    /// <summary>Consulta general.</summary>
    Consulta,

    /// <summary>Vacunación.</summary>
    Vacuna,

    /// <summary>Intervención quirúrgica programada.</summary>
    Cirugia,

    /// <summary>Revisión o seguimiento.</summary>
    Revision,

    /// <summary>Cualquier otro motivo.</summary>
    Otro,
}

/// <summary>Estado del ciclo de vida de una cita.</summary>
public enum EstadoCita
{
    /// <summary>Pedida por el cliente o la clínica, pendiente de confirmar.</summary>
    Solicitada,

    /// <summary>Confirmada: la clínica y el cliente han acordado la cita.</summary>
    Confirmada,

    /// <summary>Atendida: el animal ha acudido y se le ha atendido.</summary>
    Atendida,

    /// <summary>Cancelada: la cita no se celebrará.</summary>
    Cancelada,

    /// <summary>No presentado: el animal no acudió a la cita.</summary>
    NoPresentado,
}

/// <summary>Se ha creado una cita para un animal.</summary>
public sealed record CitaCreada(Guid CitaId, Guid EmpresaId, Guid AnimalId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Cita: una <b>entrada de la agenda</b> de la clínica para un <see cref="AnimalId">animal</see>. Es
/// la séptima raíz de agregado del producto veterinario y cuelga del animal (solo guarda su
/// identificador, sin clave foránea entre esquemas). A diferencia del resto del historial clínico, la
/// cita necesita <b>hora</b> (<see cref="Inicio"/>) y una <b>máquina de estados</b>
/// (<see cref="EstadoCita"/>): nace <see cref="EstadoCita.Solicitada"/> y avanza a
/// <see cref="EstadoCita.Confirmada"/> y <see cref="EstadoCita.Atendida"/>, o termina en
/// <see cref="EstadoCita.Cancelada"/> / <see cref="EstadoCita.NoPresentado"/>. Los estados finales no
/// admiten más transiciones (transición inválida → <see cref="Error"/>). Sobre ella se calculan los
/// KPI de confirmación de la agenda.
/// </summary>
public sealed class Cita : RaizAgregadoEmpresa<Guid>
{
    public const int DuracionPorDefectoMinutos = 30;
    public const int LongitudMaximaMotivo = 200;
    public const int LongitudMaximaVeterinario = 120;
    public const int LongitudMaximaNotas = 1000;

    private Cita(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private Cita(
        Guid id,
        Guid empresaId,
        Guid animalId,
        DateTimeOffset inicio,
        int duracionMinutos,
        TipoCita tipo,
        string? motivo,
        string? veterinario,
        string? notas,
        DateTimeOffset ahora)
        : base(id, empresaId)
    {
        AnimalId = animalId;
        Inicio = inicio;
        DuracionMinutos = duracionMinutos;
        Tipo = tipo;
        Motivo = motivo;
        Veterinario = veterinario;
        Notas = notas;
        Estado = EstadoCita.Solicitada;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Animal citado. Se guarda solo el identificador (sin FK entre esquemas).</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>Fecha y hora de inicio de la cita. Obligatoria (la agenda necesita hora).</summary>
    public DateTimeOffset Inicio { get; private set; }

    /// <summary>Duración prevista en minutos. Por defecto 30; debe ser mayor que cero.</summary>
    public int DuracionMinutos { get; private set; }

    /// <summary>Naturaleza de la cita.</summary>
    public TipoCita Tipo { get; private set; }

    /// <summary>Motivo de la cita. Opcional, máx. 200.</summary>
    public string? Motivo { get; private set; }

    /// <summary>Profesional asignado (texto libre por ahora). Opcional, máx. 120.</summary>
    public string? Veterinario { get; private set; }

    /// <summary>Estado del ciclo de vida. Empieza en <see cref="EstadoCita.Solicitada"/>.</summary>
    public EstadoCita Estado { get; private set; }

    /// <summary>Notas internas. Opcional, máx. 1000.</summary>
    public string? Notas { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Cita> Crear(
        Guid empresaId,
        Guid animalId,
        DateTimeOffset inicio,
        IReloj reloj,
        int duracionMinutos = DuracionPorDefectoMinutos,
        TipoCita tipo = TipoCita.Consulta,
        string? motivo = null,
        string? veterinario = null,
        string? notas = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(animalId, duracionMinutos, tipo, motivo, veterinario, notas);
        if (error is not null)
        {
            return Resultado.Fallo<Cita>(error);
        }

        var cita = new Cita(
            Guid.NewGuid(), empresaId, animalId, inicio, duracionMinutos, tipo,
            Normalizar(motivo), Normalizar(veterinario), Normalizar(notas), reloj.AhoraUtc);
        cita.RegistrarEvento(new CitaCreada(cita.Id, empresaId, animalId, reloj.AhoraUtc));
        return Resultado.Ok(cita);
    }

    /// <summary>Actualiza los datos de la cita (el animal no cambia). No altera el estado.</summary>
    public Resultado Actualizar(
        DateTimeOffset inicio,
        int duracionMinutos,
        TipoCita tipo,
        IReloj reloj,
        string? motivo = null,
        string? veterinario = null,
        string? notas = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(AnimalId, duracionMinutos, tipo, motivo, veterinario, notas);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Inicio = inicio;
        DuracionMinutos = duracionMinutos;
        Tipo = tipo;
        Motivo = Normalizar(motivo);
        Veterinario = Normalizar(veterinario);
        Notas = Normalizar(notas);
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Confirma la cita. Solo es válido desde <see cref="EstadoCita.Solicitada"/>.</summary>
    public Resultado Confirmar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoCita.Solicitada)
        {
            return TransicionInvalida("confirmar");
        }

        Estado = EstadoCita.Confirmada;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Reprograma la cita a un nuevo inicio (y, opcionalmente, nueva duración). Solo desde <see cref="EstadoCita.Solicitada"/> o <see cref="EstadoCita.Confirmada"/>.</summary>
    public Resultado Reprogramar(DateTimeOffset nuevoInicio, int? duracionMinutos, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado is not (EstadoCita.Solicitada or EstadoCita.Confirmada))
        {
            return TransicionInvalida("reprogramar");
        }

        var duracion = duracionMinutos ?? DuracionMinutos;
        if (duracion <= 0)
        {
            return Resultado.Fallo(Error.Validacion("cita.duracion_invalida", "La duración de la cita debe ser mayor que cero."));
        }

        Inicio = nuevoInicio;
        DuracionMinutos = duracion;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Marca la cita como atendida. Válido desde <see cref="EstadoCita.Solicitada"/> o <see cref="EstadoCita.Confirmada"/>.</summary>
    public Resultado Atender(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado is not (EstadoCita.Solicitada or EstadoCita.Confirmada))
        {
            return TransicionInvalida("atender");
        }

        Estado = EstadoCita.Atendida;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Marca la cita como no presentado. Válido desde <see cref="EstadoCita.Solicitada"/> o <see cref="EstadoCita.Confirmada"/>.</summary>
    public Resultado MarcarNoPresentado(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado is not (EstadoCita.Solicitada or EstadoCita.Confirmada))
        {
            return TransicionInvalida("marcar como no presentado");
        }

        Estado = EstadoCita.NoPresentado;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Cancela la cita. Válido desde cualquier estado activo (<see cref="EstadoCita.Solicitada"/> o <see cref="EstadoCita.Confirmada"/>).</summary>
    public Resultado Cancelar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado is not (EstadoCita.Solicitada or EstadoCita.Confirmada))
        {
            return TransicionInvalida("cancelar");
        }

        Estado = EstadoCita.Cancelada;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    private Resultado TransicionInvalida(string accion) =>
        Resultado.Fallo(Error.Conflicto("cita.transicion_invalida", $"No se puede {accion} una cita en estado «{Estado}»."));

    private static Error? Validar(
        Guid animalId,
        int duracionMinutos,
        TipoCita tipo,
        string? motivo,
        string? veterinario,
        string? notas)
    {
        if (animalId == Guid.Empty)
        {
            return Error.Validacion("cita.animal_obligatorio", "La cita debe estar asociada a un animal.");
        }

        if (duracionMinutos <= 0)
        {
            return Error.Validacion("cita.duracion_invalida", "La duración de la cita debe ser mayor que cero.");
        }

        if (!Enum.IsDefined(tipo))
        {
            return Error.Validacion("cita.tipo_invalido", "El tipo de cita no es válido.");
        }

        if (motivo is not null && motivo.Trim().Length > LongitudMaximaMotivo)
        {
            return Error.Validacion("cita.motivo_largo", "El motivo es demasiado largo.");
        }

        if (veterinario is not null && veterinario.Trim().Length > LongitudMaximaVeterinario)
        {
            return Error.Validacion("cita.veterinario_largo", "El nombre del veterinario es demasiado largo.");
        }

        if (notas is not null && notas.Trim().Length > LongitudMaximaNotas)
        {
            return Error.Validacion("cita.notas_largas", "Las notas son demasiado largas.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
