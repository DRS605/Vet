using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Se ha creado un animal (mascota).</summary>
public sealed record AnimalCreado(Guid AnimalId, Guid EmpresaId, Guid ClienteId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Especie de un animal atendido en la clínica.</summary>
public enum EspecieAnimal
{
    /// <summary>Perro.</summary>
    Perro,

    /// <summary>Gato.</summary>
    Gato,

    /// <summary>Conejo.</summary>
    Conejo,

    /// <summary>Ave.</summary>
    Ave,

    /// <summary>Hurón.</summary>
    Huron,

    /// <summary>Reptil.</summary>
    Reptil,

    /// <summary>Otra especie.</summary>
    Otro,
}

/// <summary>Sexo de un animal.</summary>
public enum SexoAnimal
{
    /// <summary>Macho.</summary>
    Macho,

    /// <summary>Hembra.</summary>
    Hembra,

    /// <summary>Desconocido.</summary>
    Desconocido,
}

/// <summary>
/// Animal (mascota) atendido en la clínica. Pertenece a un <see cref="ClienteId">cliente</see>
/// (su propietario) de la misma empresa. Es la primera raíz de agregado del producto veterinario:
/// guarda la ficha básica del animal y calcula datos derivados como la edad y si es cachorro.
/// </summary>
public sealed class Animal : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 100;
    public const int LongitudMaximaRaza = 100;
    public const int LongitudMaximaMicrochip = 20;
    public const int LongitudMaximaNotas = 1000;

    private Animal(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private Animal(
        Guid id,
        Guid empresaId,
        Guid clienteId,
        string nombre,
        EspecieAnimal especie,
        string? raza,
        SexoAnimal sexo,
        DateOnly? fechaNacimiento,
        string? microchip,
        bool esterilizado,
        decimal? pesoKg,
        string? notas,
        DateTimeOffset ahora)
        : base(id, empresaId)
    {
        ClienteId = clienteId;
        Nombre = nombre;
        Especie = especie;
        Raza = raza;
        Sexo = sexo;
        FechaNacimiento = fechaNacimiento;
        Microchip = microchip;
        Esterilizado = esterilizado;
        PesoKg = pesoKg;
        Notas = notas;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Propietario del animal (cliente de Terceros). Se guarda solo el identificador (sin FK entre esquemas).</summary>
    public Guid ClienteId { get; private set; }

    public string Nombre { get; private set; }

    public EspecieAnimal Especie { get; private set; }

    public string? Raza { get; private set; }

    public SexoAnimal Sexo { get; private set; }

    /// <summary>Fecha de nacimiento, si se conoce. No puede ser futura.</summary>
    public DateOnly? FechaNacimiento { get; private set; }

    /// <summary>Número de microchip, normalizado (sin espacios y en mayúsculas). Opcional.</summary>
    public string? Microchip { get; private set; }

    public bool Esterilizado { get; private set; }

    /// <summary>Peso en kilogramos, si se ha medido. Debe ser mayor que cero.</summary>
    public decimal? PesoKg { get; private set; }

    public string? Notas { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Animal> Crear(
        Guid empresaId,
        Guid clienteId,
        string? nombre,
        EspecieAnimal especie,
        SexoAnimal sexo,
        IReloj reloj,
        string? raza = null,
        DateOnly? fechaNacimiento = null,
        string? microchip = null,
        bool esterilizado = false,
        decimal? pesoKg = null,
        string? notas = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(clienteId, nombre, especie, sexo, raza, fechaNacimiento, microchip, pesoKg, notas, reloj);
        if (error is not null)
        {
            return Resultado.Fallo<Animal>(error);
        }

        var animal = new Animal(
            Guid.NewGuid(), empresaId, clienteId, nombre!.Trim(), especie, Normalizar(raza), sexo,
            fechaNacimiento, NormalizarMicrochip(microchip), esterilizado, pesoKg, Normalizar(notas), reloj.AhoraUtc);
        animal.RegistrarEvento(new AnimalCreado(animal.Id, empresaId, clienteId, reloj.AhoraUtc));
        return Resultado.Ok(animal);
    }

    public Resultado Actualizar(
        string? nombre,
        EspecieAnimal especie,
        SexoAnimal sexo,
        IReloj reloj,
        string? raza = null,
        DateOnly? fechaNacimiento = null,
        string? microchip = null,
        bool esterilizado = false,
        decimal? pesoKg = null,
        string? notas = null)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(ClienteId, nombre, especie, sexo, raza, fechaNacimiento, microchip, pesoKg, notas, reloj);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Nombre = nombre!.Trim();
        Especie = especie;
        Raza = Normalizar(raza);
        Sexo = sexo;
        FechaNacimiento = fechaNacimiento;
        Microchip = NormalizarMicrochip(microchip);
        Esterilizado = esterilizado;
        PesoKg = pesoKg;
        Notas = Normalizar(notas);
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public void Desactivar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    /// <summary>Edad en meses completos a la fecha indicada, o <c>null</c> si no consta la fecha de nacimiento.</summary>
    public int? EdadMeses(DateOnly hoy)
    {
        if (FechaNacimiento is not { } nacimiento || nacimiento > hoy)
        {
            return null;
        }

        var meses = ((hoy.Year - nacimiento.Year) * 12) + hoy.Month - nacimiento.Month;
        if (hoy.Day < nacimiento.Day)
        {
            meses--;
        }

        return meses < 0 ? 0 : meses;
    }

    /// <summary>¿Es cachorro? Requiere fecha de nacimiento y una edad inferior al umbral de su especie.</summary>
    public bool EsCachorro(DateOnly hoy)
    {
        var edad = EdadMeses(hoy);
        return edad is { } meses && meses < UmbralCachorroMeses(Especie);
    }

    /// <summary>Umbral (en meses) por debajo del cual un animal se considera cachorro, según su especie.</summary>
    public static int UmbralCachorroMeses(EspecieAnimal especie) => especie switch
    {
        EspecieAnimal.Perro => 12,
        EspecieAnimal.Gato => 12,
        EspecieAnimal.Conejo => 6,
        EspecieAnimal.Huron => 12,
        EspecieAnimal.Ave => 12,
        EspecieAnimal.Reptil => 12,
        _ => 12,
    };

    private static Error? Validar(
        Guid clienteId,
        string? nombre,
        EspecieAnimal especie,
        SexoAnimal sexo,
        string? raza,
        DateOnly? fechaNacimiento,
        string? microchip,
        decimal? pesoKg,
        string? notas,
        IReloj reloj)
    {
        if (clienteId == Guid.Empty)
        {
            return Error.Validacion("animal.cliente_obligatorio", "El animal debe tener un propietario (cliente).");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("animal.nombre_vacio", "El nombre del animal es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("animal.nombre_largo", "El nombre del animal es demasiado largo.");
        }

        if (!Enum.IsDefined(especie))
        {
            return Error.Validacion("animal.especie_invalida", "La especie indicada no es válida.");
        }

        if (!Enum.IsDefined(sexo))
        {
            return Error.Validacion("animal.sexo_invalido", "El sexo indicado no es válido.");
        }

        if (raza is not null && raza.Trim().Length > LongitudMaximaRaza)
        {
            return Error.Validacion("animal.raza_larga", "La raza es demasiado larga.");
        }

        if (fechaNacimiento is { } nacimiento && nacimiento > DateOnly.FromDateTime(reloj.AhoraUtc.UtcDateTime))
        {
            return Error.Validacion("animal.fecha_nacimiento_futura", "La fecha de nacimiento no puede ser futura.");
        }

        var microchipNormalizado = NormalizarMicrochip(microchip);
        if (microchipNormalizado is not null && microchipNormalizado.Length > LongitudMaximaMicrochip)
        {
            return Error.Validacion("animal.microchip_largo", "El microchip es demasiado largo.");
        }

        if (pesoKg is { } peso && peso <= 0m)
        {
            return Error.Validacion("animal.peso_invalido", "El peso debe ser mayor que cero.");
        }

        if (notas is not null && notas.Trim().Length > LongitudMaximaNotas)
        {
            return Error.Validacion("animal.notas_largas", "Las notas son demasiado largas.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? NormalizarMicrochip(string? microchip) =>
        string.IsNullOrWhiteSpace(microchip) ? null : microchip.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
