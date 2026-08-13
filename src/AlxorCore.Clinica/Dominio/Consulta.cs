using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Se ha registrado una consulta (entrada del historial clínico) de un animal.</summary>
public sealed record ConsultaRegistrada(Guid ConsultaId, Guid EmpresaId, Guid AnimalId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Consulta veterinaria: una entrada del historial clínico de un <see cref="AnimalId">animal</see>.
/// Es la segunda raíz de agregado del producto veterinario y cuelga del animal (solo guarda su
/// identificador, sin clave foránea entre esquemas). El historial no se borra físicamente: una
/// consulta se «anula» con una baja lógica.
/// </summary>
public sealed class Consulta : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaMotivo = 200;
    public const int LongitudMaximaDiagnostico = 2000;
    public const int LongitudMaximaTratamiento = 2000;
    public const int LongitudMaximaVeterinario = 120;

    private Consulta(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private Consulta(
        Guid id,
        Guid empresaId,
        Guid animalId,
        DateOnly fecha,
        string? motivo,
        string? diagnostico,
        string? tratamiento,
        decimal? pesoKg,
        string? veterinario,
        DateTimeOffset ahora)
        : base(id, empresaId)
    {
        AnimalId = animalId;
        Fecha = fecha;
        Motivo = motivo;
        Diagnostico = diagnostico;
        Tratamiento = tratamiento;
        PesoKg = pesoKg;
        Veterinario = veterinario;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Animal atendido en la consulta. Se guarda solo el identificador (sin FK entre esquemas).</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>Fecha de la consulta. Obligatoria. No puede ser futura.</summary>
    public DateOnly Fecha { get; private set; }

    /// <summary>Motivo de la visita. Opcional, máx. 200.</summary>
    public string? Motivo { get; private set; }

    /// <summary>Diagnóstico. Opcional, máx. 2000.</summary>
    public string? Diagnostico { get; private set; }

    /// <summary>Tratamiento indicado. Opcional, máx. 2000.</summary>
    public string? Tratamiento { get; private set; }

    /// <summary>Peso tomado en la visita, si se ha medido. Debe ser mayor que cero.</summary>
    public decimal? PesoKg { get; private set; }

    /// <summary>Profesional que atiende (texto libre por ahora). Opcional, máx. 120.</summary>
    public string? Veterinario { get; private set; }

    /// <summary>Baja lógica: una consulta anulada deja de aparecer en el historial, pero no se borra.</summary>
    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Consulta> Crear(
        Guid empresaId,
        Guid animalId,
        DateOnly fecha,
        IReloj reloj,
        string? motivo = null,
        string? diagnostico = null,
        string? tratamiento = null,
        decimal? pesoKg = null,
        string? veterinario = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(animalId, fecha, motivo, diagnostico, tratamiento, pesoKg, veterinario, reloj);
        if (error is not null)
        {
            return Resultado.Fallo<Consulta>(error);
        }

        var consulta = new Consulta(
            Guid.NewGuid(), empresaId, animalId, fecha, Normalizar(motivo), Normalizar(diagnostico),
            Normalizar(tratamiento), pesoKg, Normalizar(veterinario), reloj.AhoraUtc);
        consulta.RegistrarEvento(new ConsultaRegistrada(consulta.Id, empresaId, animalId, reloj.AhoraUtc));
        return Resultado.Ok(consulta);
    }

    public Resultado Actualizar(
        DateOnly fecha,
        IReloj reloj,
        string? motivo = null,
        string? diagnostico = null,
        string? tratamiento = null,
        decimal? pesoKg = null,
        string? veterinario = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(AnimalId, fecha, motivo, diagnostico, tratamiento, pesoKg, veterinario, reloj);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Fecha = fecha;
        Motivo = Normalizar(motivo);
        Diagnostico = Normalizar(diagnostico);
        Tratamiento = Normalizar(tratamiento);
        PesoKg = pesoKg;
        Veterinario = Normalizar(veterinario);
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Anula la consulta (baja lógica): deja de aparecer en el historial, pero no se borra.</summary>
    public void Anular(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static Error? Validar(
        Guid animalId,
        DateOnly fecha,
        string? motivo,
        string? diagnostico,
        string? tratamiento,
        decimal? pesoKg,
        string? veterinario,
        IReloj reloj)
    {
        if (animalId == Guid.Empty)
        {
            return Error.Validacion("consulta.animal_obligatorio", "La consulta debe estar asociada a un animal.");
        }

        if (fecha > DateOnly.FromDateTime(reloj.AhoraUtc.UtcDateTime))
        {
            return Error.Validacion("consulta.fecha_futura", "La fecha de la consulta no puede ser futura.");
        }

        if (motivo is not null && motivo.Trim().Length > LongitudMaximaMotivo)
        {
            return Error.Validacion("consulta.motivo_largo", "El motivo es demasiado largo.");
        }

        if (diagnostico is not null && diagnostico.Trim().Length > LongitudMaximaDiagnostico)
        {
            return Error.Validacion("consulta.diagnostico_largo", "El diagnóstico es demasiado largo.");
        }

        if (tratamiento is not null && tratamiento.Trim().Length > LongitudMaximaTratamiento)
        {
            return Error.Validacion("consulta.tratamiento_largo", "El tratamiento es demasiado largo.");
        }

        if (veterinario is not null && veterinario.Trim().Length > LongitudMaximaVeterinario)
        {
            return Error.Validacion("consulta.veterinario_largo", "El nombre del veterinario es demasiado largo.");
        }

        if (pesoKg is { } peso && peso <= 0m)
        {
            return Error.Validacion("consulta.peso_invalido", "El peso debe ser mayor que cero.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
