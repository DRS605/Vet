using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Se ha creado una especie (maestro editable de especies por empresa).</summary>
public sealed record EspecieCreada(Guid EspecieId, Guid EmpresaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Especie de animal atendida en la clínica. Es un <b>maestro editable por empresa</b>: cada clínica
/// da de alta, edita y da de baja sus propias especies. Sustituye al antiguo enumerado fijo
/// <c>EspecieAnimal</c>. Los animales y las pautas vacunales referencian la especie <b>por su nombre</b>
/// (la columna sigue siendo texto), y el umbral de «cachorro» (<see cref="MesesCachorro"/>) pasa a venir
/// de aquí en lugar de estar cableado por especie.
/// </summary>
public sealed class Especie : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 60;

    /// <summary>Umbral por defecto (en meses) por debajo del cual un animal se considera cachorro.</summary>
    public const int MesesCachorroPorDefecto = 12;

    private Especie(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private Especie(Guid id, Guid empresaId, string nombre, int mesesCachorro, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Nombre = nombre;
        MesesCachorro = mesesCachorro;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Nombre de la especie (obligatorio, único por empresa, máx. 60). Es la clave que usan animales y pautas.</summary>
    public string Nombre { get; private set; }

    /// <summary>Umbral (en meses) por debajo del cual un animal de esta especie se considera cachorro. &gt; 0.</summary>
    public int MesesCachorro { get; private set; }

    /// <summary>Baja lógica: una especie desactivada deja de ofrecerse, pero no se borra.</summary>
    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Especie> Crear(Guid empresaId, string? nombre, int mesesCachorro, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, mesesCachorro);
        if (error is not null)
        {
            return Resultado.Fallo<Especie>(error);
        }

        var especie = new Especie(Guid.NewGuid(), empresaId, nombre!.Trim(), mesesCachorro, reloj.AhoraUtc);
        especie.RegistrarEvento(new EspecieCreada(especie.Id, empresaId, reloj.AhoraUtc));
        return Resultado.Ok(especie);
    }

    public Resultado Actualizar(string? nombre, int mesesCachorro, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, mesesCachorro);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Nombre = nombre!.Trim();
        MesesCachorro = mesesCachorro;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Desactiva la especie (baja lógica): deja de ofrecerse, pero no se borra.</summary>
    public void Desactivar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static Error? Validar(string? nombre, int mesesCachorro)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("especie.nombre_vacio", "El nombre de la especie es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("especie.nombre_largo", "El nombre de la especie es demasiado largo.");
        }

        if (mesesCachorro <= 0)
        {
            return Error.Validacion("especie.meses_invalido", "El umbral de cachorro (en meses) debe ser mayor que cero.");
        }

        return null;
    }
}
