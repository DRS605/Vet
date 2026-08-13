using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Se ha registrado una vacunación (dosis aplicada) a un animal.</summary>
public sealed record VacunacionRegistrada(Guid VacunacionId, Guid EmpresaId, Guid AnimalId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Vacunación: una <b>dosis concreta</b> aplicada a un <see cref="AnimalId">animal</see>. Es la
/// cuarta raíz de agregado del producto veterinario y cuelga del animal (solo guarda su
/// identificador, sin clave foránea entre esquemas). Puede apoyarse en una <see cref="PautaVacunal"/>
/// (guardando su identificador) o ser ad-hoc; en ambos casos el <see cref="Nombre"/> se conserva como
/// instantánea (snapshot) para que el historial sea estable aunque la pauta cambie o se borre. El
/// historial no se borra físicamente: una vacunación se «anula» con una baja lógica.
/// </summary>
public sealed class Vacunacion : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 120;
    public const int LongitudMaximaLote = 60;
    public const int LongitudMaximaVeterinario = 120;
    public const int LongitudMaximaNotas = 1000;

    private Vacunacion(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private Vacunacion(
        Guid id,
        Guid empresaId,
        Guid animalId,
        Guid? pautaVacunalId,
        string nombre,
        DateOnly fechaAplicacion,
        string? lote,
        DateOnly? proximaDosis,
        string? veterinario,
        string? notas,
        DateTimeOffset ahora)
        : base(id, empresaId)
    {
        AnimalId = animalId;
        PautaVacunalId = pautaVacunalId;
        Nombre = nombre;
        FechaAplicacion = fechaAplicacion;
        Lote = lote;
        ProximaDosis = proximaDosis;
        Veterinario = veterinario;
        Notas = notas;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Animal vacunado. Se guarda solo el identificador (sin FK entre esquemas).</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>Pauta maestra usada, si la hubo (null = ad-hoc). Se guarda solo el identificador.</summary>
    public Guid? PautaVacunalId { get; private set; }

    /// <summary>Nombre de la vacuna (obligatorio, máx. 120). Instantánea estable del historial.</summary>
    public string Nombre { get; private set; }

    /// <summary>Fecha de aplicación. Obligatoria. No puede ser futura.</summary>
    public DateOnly FechaAplicacion { get; private set; }

    /// <summary>Lote de la vacuna. Opcional, máx. 60.</summary>
    public string? Lote { get; private set; }

    /// <summary>Fecha de la próxima dosis. Opcional; puede venir dada o calcularse desde la periodicidad de la pauta.</summary>
    public DateOnly? ProximaDosis { get; private set; }

    /// <summary>Profesional que aplica la vacuna (texto libre). Opcional, máx. 120.</summary>
    public string? Veterinario { get; private set; }

    /// <summary>Notas. Opcional, máx. 1000.</summary>
    public string? Notas { get; private set; }

    /// <summary>Baja lógica: una vacunación anulada deja de aparecer en el historial, pero no se borra.</summary>
    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Vacunacion> Crear(
        Guid empresaId,
        Guid animalId,
        string? nombre,
        DateOnly fechaAplicacion,
        IReloj reloj,
        Guid? pautaVacunalId = null,
        string? lote = null,
        DateOnly? proximaDosis = null,
        string? veterinario = null,
        string? notas = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(animalId, nombre, fechaAplicacion, lote, veterinario, notas, reloj);
        if (error is not null)
        {
            return Resultado.Fallo<Vacunacion>(error);
        }

        var vacunacion = new Vacunacion(
            Guid.NewGuid(), empresaId, animalId, pautaVacunalId, nombre!.Trim(), fechaAplicacion,
            Normalizar(lote), proximaDosis, Normalizar(veterinario), Normalizar(notas), reloj.AhoraUtc);
        vacunacion.RegistrarEvento(new VacunacionRegistrada(vacunacion.Id, empresaId, animalId, reloj.AhoraUtc));
        return Resultado.Ok(vacunacion);
    }

    public Resultado Actualizar(
        string? nombre,
        DateOnly fechaAplicacion,
        IReloj reloj,
        Guid? pautaVacunalId = null,
        string? lote = null,
        DateOnly? proximaDosis = null,
        string? veterinario = null,
        string? notas = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(AnimalId, nombre, fechaAplicacion, lote, veterinario, notas, reloj);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        PautaVacunalId = pautaVacunalId;
        Nombre = nombre!.Trim();
        FechaAplicacion = fechaAplicacion;
        Lote = Normalizar(lote);
        ProximaDosis = proximaDosis;
        Veterinario = Normalizar(veterinario);
        Notas = Normalizar(notas);
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Anula la vacunación (baja lógica): deja de aparecer en el historial, pero no se borra.</summary>
    public void Anular(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static Error? Validar(
        Guid animalId,
        string? nombre,
        DateOnly fechaAplicacion,
        string? lote,
        string? veterinario,
        string? notas,
        IReloj reloj)
    {
        if (animalId == Guid.Empty)
        {
            return Error.Validacion("vacunacion.animal_obligatorio", "La vacunación debe estar asociada a un animal.");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("vacunacion.nombre_vacio", "El nombre de la vacuna es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("vacunacion.nombre_largo", "El nombre de la vacuna es demasiado largo.");
        }

        if (fechaAplicacion > DateOnly.FromDateTime(reloj.AhoraUtc.UtcDateTime))
        {
            return Error.Validacion("vacunacion.fecha_futura", "La fecha de aplicación no puede ser futura.");
        }

        if (lote is not null && lote.Trim().Length > LongitudMaximaLote)
        {
            return Error.Validacion("vacunacion.lote_largo", "El lote es demasiado largo.");
        }

        if (veterinario is not null && veterinario.Trim().Length > LongitudMaximaVeterinario)
        {
            return Error.Validacion("vacunacion.veterinario_largo", "El nombre del veterinario es demasiado largo.");
        }

        if (notas is not null && notas.Trim().Length > LongitudMaximaNotas)
        {
            return Error.Validacion("vacunacion.notas_largas", "Las notas son demasiado largas.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
