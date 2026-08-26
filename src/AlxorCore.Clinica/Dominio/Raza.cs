using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Se ha creado una raza (maestro de razas por especie de la empresa).</summary>
public sealed record RazaCreada(Guid RazaId, Guid EmpresaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Raza de animal. Es un <b>maestro editable por empresa</b>, agrupado por <see cref="Especie">especie</see>
/// (referenciada por su nombre, igual que en <c>Animal</c> y las pautas). Al dar de alta un animal, la
/// raza se elige de este maestro filtrado por la especie seleccionada.
/// </summary>
public sealed class Raza : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaEspecie = 60;
    public const int LongitudMaximaNombre = 100;

    private Raza(Guid id)
        : base(id, Guid.Empty)
    {
        Especie = null!;
        Nombre = null!;
    }

    private Raza(Guid id, Guid empresaId, string especie, string nombre, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Especie = especie;
        Nombre = nombre;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Especie a la que pertenece la raza (nombre del maestro de especies).</summary>
    public string Especie { get; private set; }

    /// <summary>Nombre de la raza (obligatorio, único por empresa y especie).</summary>
    public string Nombre { get; private set; }

    /// <summary>Baja lógica: una raza desactivada deja de ofrecerse, pero no se borra.</summary>
    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Raza> Crear(Guid empresaId, string? especie, string? nombre, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(especie, nombre);
        if (error is not null)
        {
            return Resultado.Fallo<Raza>(error);
        }

        var raza = new Raza(Guid.NewGuid(), empresaId, especie!.Trim(), nombre!.Trim(), reloj.AhoraUtc);
        raza.RegistrarEvento(new RazaCreada(raza.Id, empresaId, reloj.AhoraUtc));
        return Resultado.Ok(raza);
    }

    /// <summary>Actualiza el nombre de la raza. La especie no cambia (es la clave de agrupación).</summary>
    public Resultado Actualizar(string? nombre, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(Especie, nombre);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Nombre = nombre!.Trim();
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public void Desactivar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static Error? Validar(string? especie, string? nombre)
    {
        if (string.IsNullOrWhiteSpace(especie))
        {
            return Error.Validacion("raza.especie_vacia", "La especie de la raza es obligatoria.");
        }

        if (especie.Trim().Length > LongitudMaximaEspecie)
        {
            return Error.Validacion("raza.especie_larga", "La especie indicada es demasiado larga.");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("raza.nombre_vacio", "El nombre de la raza es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("raza.nombre_largo", "El nombre de la raza es demasiado largo.");
        }

        return null;
    }
}
