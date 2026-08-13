using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Se ha creado una pauta vacunal (cuadro maestro de vacunación por especie).</summary>
public sealed record PautaVacunalCreada(Guid PautaId, Guid EmpresaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Carácter (obligatoriedad) de una vacuna dentro de la pauta.</summary>
public enum CaracterVacuna
{
    /// <summary>Legalmente obligatoria (p. ej. la rabia en algunas comunidades).</summary>
    Legal,

    /// <summary>Recomendada por el protocolo clínico habitual.</summary>
    Recomendada,

    /// <summary>Opcional, según circunstancias del animal.</summary>
    Opcional,
}

/// <summary>
/// Pauta vacunal: el <b>cuadro maestro</b> de vacunación de una especie dentro de la empresa
/// (p. ej. «Polivalente (DHPPi/L)» para perros, «Rabia»). Es la tercera raíz de agregado del
/// producto veterinario. Describe qué vacuna se pone, a qué edad se empieza y cada cuánto se
/// refuerza; las dosis concretas aplicadas a cada animal son <see cref="Vacunacion"/>.
/// </summary>
public sealed class PautaVacunal : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 120;

    private PautaVacunal(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private PautaVacunal(
        Guid id,
        Guid empresaId,
        EspecieAnimal especie,
        string nombre,
        CaracterVacuna caracter,
        int? edadInicioSemanas,
        int? periodicidadRefuerzoMeses,
        DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Especie = especie;
        Nombre = nombre;
        Caracter = caracter;
        EdadInicioSemanas = edadInicioSemanas;
        PeriodicidadRefuerzoMeses = periodicidadRefuerzoMeses;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Especie a la que aplica la pauta. Persistida como texto.</summary>
    public EspecieAnimal Especie { get; private set; }

    /// <summary>Nombre de la vacuna (obligatorio, máx. 120).</summary>
    public string Nombre { get; private set; }

    /// <summary>Carácter (obligatoriedad) de la vacuna. Persistido como texto.</summary>
    public CaracterVacuna Caracter { get; private set; }

    /// <summary>Edad recomendada de inicio, en semanas. Opcional; si se indica, &gt;= 0.</summary>
    public int? EdadInicioSemanas { get; private set; }

    /// <summary>Meses entre refuerzos (12 = anual). Opcional; si se indica, &gt; 0. Null = dosis única / sin refuerzo periódico.</summary>
    public int? PeriodicidadRefuerzoMeses { get; private set; }

    /// <summary>Baja lógica: una pauta desactivada deja de ofrecerse, pero no se borra.</summary>
    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<PautaVacunal> Crear(
        Guid empresaId,
        EspecieAnimal especie,
        string? nombre,
        CaracterVacuna caracter,
        IReloj reloj,
        int? edadInicioSemanas = null,
        int? periodicidadRefuerzoMeses = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(especie, nombre, caracter, edadInicioSemanas, periodicidadRefuerzoMeses);
        if (error is not null)
        {
            return Resultado.Fallo<PautaVacunal>(error);
        }

        var pauta = new PautaVacunal(
            Guid.NewGuid(), empresaId, especie, nombre!.Trim(), caracter,
            edadInicioSemanas, periodicidadRefuerzoMeses, reloj.AhoraUtc);
        pauta.RegistrarEvento(new PautaVacunalCreada(pauta.Id, empresaId, reloj.AhoraUtc));
        return Resultado.Ok(pauta);
    }

    public Resultado Actualizar(
        EspecieAnimal especie,
        string? nombre,
        CaracterVacuna caracter,
        IReloj reloj,
        int? edadInicioSemanas = null,
        int? periodicidadRefuerzoMeses = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(especie, nombre, caracter, edadInicioSemanas, periodicidadRefuerzoMeses);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Especie = especie;
        Nombre = nombre!.Trim();
        Caracter = caracter;
        EdadInicioSemanas = edadInicioSemanas;
        PeriodicidadRefuerzoMeses = periodicidadRefuerzoMeses;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Desactiva la pauta (baja lógica): deja de ofrecerse, pero no se borra.</summary>
    public void Desactivar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    /// <summary>
    /// Próxima dosis a partir de una fecha de aplicación y una periodicidad en meses:
    /// <c>fechaAplicacion.AddMonths(periodicidad)</c> si la periodicidad es &gt; 0; en otro caso <c>null</c>.
    /// </summary>
    public static DateOnly? CalcularProximaDosis(DateOnly fechaAplicacion, int? periodicidadMeses) =>
        periodicidadMeses is { } meses && meses > 0 ? fechaAplicacion.AddMonths(meses) : null;

    private static Error? Validar(
        EspecieAnimal especie,
        string? nombre,
        CaracterVacuna caracter,
        int? edadInicioSemanas,
        int? periodicidadRefuerzoMeses)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("pauta_vacunal.nombre_vacio", "El nombre de la vacuna es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("pauta_vacunal.nombre_largo", "El nombre de la vacuna es demasiado largo.");
        }

        if (!Enum.IsDefined(especie))
        {
            return Error.Validacion("pauta_vacunal.especie_invalida", "La especie indicada no es válida.");
        }

        if (!Enum.IsDefined(caracter))
        {
            return Error.Validacion("pauta_vacunal.caracter_invalido", "El carácter indicado no es válido.");
        }

        if (edadInicioSemanas is { } edad && edad < 0)
        {
            return Error.Validacion("pauta_vacunal.edad_invalida", "La edad de inicio no puede ser negativa.");
        }

        if (periodicidadRefuerzoMeses is { } periodicidad && periodicidad <= 0)
        {
            return Error.Validacion("pauta_vacunal.periodicidad_invalida", "La periodicidad de refuerzo debe ser mayor que cero.");
        }

        return null;
    }
}
