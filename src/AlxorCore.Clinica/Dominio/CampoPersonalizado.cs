using System.Globalization;
using System.Text;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Se ha creado un campo personalizado (maestro de campos definidos por la empresa).</summary>
public sealed record CampoPersonalizadoCreado(Guid CampoId, Guid EmpresaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Entidad a la que se pueden añadir campos personalizados. La clínica amplía libremente los
/// mantenimientos de <see cref="Cliente"/> y <see cref="Animal"/> sin tocar código.
/// </summary>
public enum EntidadPersonalizable
{
    /// <summary>Ficha de cliente (propietario).</summary>
    Cliente,

    /// <summary>Ficha de animal (mascota).</summary>
    Animal,
}

/// <summary>Tipo de dato de un campo personalizado. Determina cómo se pinta y se valida su valor.</summary>
public enum TipoCampo
{
    /// <summary>Texto corto de una línea.</summary>
    Texto,

    /// <summary>Texto largo de varias líneas.</summary>
    TextoLargo,

    /// <summary>Número decimal.</summary>
    Numero,

    /// <summary>Fecha (sin hora).</summary>
    Fecha,

    /// <summary>Sí/No.</summary>
    Booleano,

    /// <summary>Lista desplegable de opciones fijas.</summary>
    Lista,
}

/// <summary>
/// Definición de un campo personalizado del maestro de la empresa. Cada clínica añade a la ficha de
/// clientes o de animales los campos que necesite (p. ej. «Nº de chip aseguradora», «Alergias»,
/// «Consentimiento firmado»). El valor concreto de cada ficha vive en <see cref="ValorCampoPersonalizado"/>.
/// </summary>
public sealed class CampoPersonalizado : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaEtiqueta = 60;
    public const int LongitudMaximaClave = 60;
    public const int LongitudMaximaOpciones = 1000;

    private CampoPersonalizado(Guid id)
        : base(id, Guid.Empty)
    {
        Etiqueta = null!;
        Clave = null!;
    }

    private CampoPersonalizado(
        Guid id,
        Guid empresaId,
        EntidadPersonalizable entidad,
        string etiqueta,
        string clave,
        TipoCampo tipo,
        string? opciones,
        bool obligatorio,
        int orden,
        DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Entidad = entidad;
        Etiqueta = etiqueta;
        Clave = clave;
        Tipo = tipo;
        Opciones = opciones;
        Obligatorio = obligatorio;
        Orden = orden;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Entidad a la que pertenece el campo (cliente o animal).</summary>
    public EntidadPersonalizable Entidad { get; private set; }

    /// <summary>Etiqueta visible del campo (obligatoria, máx. 60).</summary>
    public string Etiqueta { get; private set; }

    /// <summary>Clave normalizada de la etiqueta. Única por empresa y entidad; evita duplicados «Chip»/«chip».</summary>
    public string Clave { get; private set; }

    public TipoCampo Tipo { get; private set; }

    /// <summary>Opciones de una <see cref="TipoCampo.Lista"/>, una por línea. Nulo para el resto de tipos.</summary>
    public string? Opciones { get; private set; }

    /// <summary>Si es obligatorio, no se puede guardar la ficha sin valor.</summary>
    public bool Obligatorio { get; private set; }

    /// <summary>Orden de aparición dentro del formulario (ascendente).</summary>
    public int Orden { get; private set; }

    /// <summary>Baja lógica: un campo desactivado deja de pedirse, pero no se borra (conserva valores).</summary>
    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>Opciones de la lista como colección (líneas no vacías). Vacío si no es lista.</summary>
    public IReadOnlyList<string> OpcionesLista =>
        Tipo != TipoCampo.Lista || string.IsNullOrWhiteSpace(Opciones)
            ? Array.Empty<string>()
            : Opciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static Resultado<CampoPersonalizado> Crear(
        Guid empresaId,
        EntidadPersonalizable entidad,
        string? etiqueta,
        TipoCampo tipo,
        string? opciones,
        bool obligatorio,
        int orden,
        IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var opcionesNormalizadas = NormalizarOpciones(tipo, opciones);
        var error = Validar(etiqueta, tipo, opcionesNormalizadas);
        if (error is not null)
        {
            return Resultado.Fallo<CampoPersonalizado>(error);
        }

        var campo = new CampoPersonalizado(
            Guid.NewGuid(), empresaId, entidad, etiqueta!.Trim(), Clavear(etiqueta), tipo,
            opcionesNormalizadas, obligatorio, orden, reloj.AhoraUtc);
        campo.RegistrarEvento(new CampoPersonalizadoCreado(campo.Id, empresaId, reloj.AhoraUtc));
        return Resultado.Ok(campo);
    }

    /// <summary>Actualiza la definición. La <see cref="Entidad"/> no cambia (los valores ya asociados lo son a esa entidad).</summary>
    public Resultado Actualizar(string? etiqueta, TipoCampo tipo, string? opciones, bool obligatorio, int orden, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var opcionesNormalizadas = NormalizarOpciones(tipo, opciones);
        var error = Validar(etiqueta, tipo, opcionesNormalizadas);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Etiqueta = etiqueta!.Trim();
        Clave = Clavear(etiqueta);
        Tipo = tipo;
        Opciones = opcionesNormalizadas;
        Obligatorio = obligatorio;
        Orden = orden;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public void Desactivar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    /// <summary>
    /// Valida y normaliza un valor entrante para este campo, devolviendo el texto que se debe persistir
    /// (o <c>null</c> si queda vacío). Un valor vacío en un campo <see cref="Obligatorio"/> es un fallo.
    /// </summary>
    public Resultado<string?> NormalizarValor(string? valor)
    {
        var bruto = valor?.Trim();
        if (string.IsNullOrEmpty(bruto))
        {
            return Obligatorio
                ? Resultado.Fallo<string?>(Error.Validacion("campo.valor_obligatorio", $"El campo «{Etiqueta}» es obligatorio."))
                : Resultado.Ok<string?>(null);
        }

        switch (Tipo)
        {
            case TipoCampo.Numero:
                // El proyecto corre en modo invariante (sin culturas). Aceptamos coma o punto decimal
                // sin depender de una cultura: si viene coma y no punto, la tratamos como separador decimal.
                var candidato = bruto.Contains(',', StringComparison.Ordinal) && !bruto.Contains('.', StringComparison.Ordinal)
                    ? bruto.Replace(',', '.')
                    : bruto;
                const NumberStyles estiloNumero = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign
                    | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
                if (!decimal.TryParse(candidato, estiloNumero, CultureInfo.InvariantCulture, out var numeroInv))
                {
                    return Resultado.Fallo<string?>(Error.Validacion("campo.numero_invalido", $"El campo «{Etiqueta}» debe ser un número."));
                }

                return Resultado.Ok<string?>(numeroInv.ToString(CultureInfo.InvariantCulture));

            case TipoCampo.Fecha:
                // Aceptamos ISO (yyyy-MM-dd) y el formato español dd/MM/yyyy; siempre se almacena en ISO.
                string[] formatos = { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy" };
                if (!DateOnly.TryParseExact(bruto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
                {
                    return Resultado.Fallo<string?>(Error.Validacion("campo.fecha_invalida", $"El campo «{Etiqueta}» debe ser una fecha válida."));
                }

                return Resultado.Ok<string?>(fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            case TipoCampo.Booleano:
                var esCierto = bruto is "true" or "True" or "sí" or "Sí" or "si" or "Si" or "1";
                return Resultado.Ok<string?>(esCierto ? "true" : "false");

            case TipoCampo.Lista:
                var opcion = OpcionesLista.FirstOrDefault(o => string.Equals(o, bruto, StringComparison.OrdinalIgnoreCase));
                return opcion is null
                    ? Resultado.Fallo<string?>(Error.Validacion("campo.opcion_invalida", $"«{bruto}» no es una opción válida del campo «{Etiqueta}»."))
                    : Resultado.Ok<string?>(opcion);

            case TipoCampo.Texto:
                return bruto.Length > 500
                    ? Resultado.Fallo<string?>(Error.Validacion("campo.texto_largo", $"El campo «{Etiqueta}» es demasiado largo."))
                    : Resultado.Ok<string?>(bruto);

            case TipoCampo.TextoLargo:
            default:
                return bruto.Length > ValorCampoPersonalizado.LongitudMaximaValor
                    ? Resultado.Fallo<string?>(Error.Validacion("campo.texto_largo", $"El campo «{Etiqueta}» es demasiado largo."))
                    : Resultado.Ok<string?>(bruto);
        }
    }

    private static Error? Validar(string? etiqueta, TipoCampo tipo, string? opcionesNormalizadas)
    {
        if (string.IsNullOrWhiteSpace(etiqueta))
        {
            return Error.Validacion("campo.etiqueta_vacia", "La etiqueta del campo es obligatoria.");
        }

        if (etiqueta.Trim().Length > LongitudMaximaEtiqueta)
        {
            return Error.Validacion("campo.etiqueta_larga", "La etiqueta del campo es demasiado larga.");
        }

        if (!Enum.IsDefined(tipo))
        {
            return Error.Validacion("campo.tipo_invalido", "El tipo de campo no es válido.");
        }

        if (Clavear(etiqueta).Length == 0)
        {
            return Error.Validacion("campo.etiqueta_invalida", "La etiqueta debe contener al menos una letra o número.");
        }

        if (tipo == TipoCampo.Lista && string.IsNullOrWhiteSpace(opcionesNormalizadas))
        {
            return Error.Validacion("campo.lista_sin_opciones", "Una lista debe tener al menos una opción.");
        }

        if (opcionesNormalizadas is not null && opcionesNormalizadas.Length > LongitudMaximaOpciones)
        {
            return Error.Validacion("campo.opciones_largas", "La lista de opciones es demasiado larga.");
        }

        return null;
    }

    // Solo se conservan las opciones para el tipo Lista; el resto de tipos las ignora.
    private static string? NormalizarOpciones(TipoCampo tipo, string? opciones)
    {
        if (tipo != TipoCampo.Lista || string.IsNullOrWhiteSpace(opciones))
        {
            return null;
        }

        var lineas = opciones
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lineas.Length == 0 ? null : string.Join('\n', lineas);
    }

    // Clave estable a partir de la etiqueta: minúsculas, sin acentos, alfanumérico y guiones bajos.
    private static string Clavear(string? etiqueta)
    {
        if (string.IsNullOrWhiteSpace(etiqueta))
        {
            return string.Empty;
        }

        // Nota: el proyecto corre en modo de globalización invariante, donde string.Normalize(FormD)
        // no descompone los acentos; por eso se pliegan a mano las vocales acentuadas, la ñ y la ç.
        var normalizada = etiqueta.Trim().ToLowerInvariant();
        var sb = new StringBuilder(normalizada.Length);
        foreach (var original in normalizada)
        {
            var c = PlegarAcento(original);
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                sb.Append(c);
            }
            else if (sb.Length > 0 && sb[^1] != '_')
            {
                sb.Append('_');
            }
        }

        var clave = sb.ToString().Trim('_');
        return clave.Length > LongitudMaximaClave ? clave[..LongitudMaximaClave] : clave;
    }

    private static char PlegarAcento(char c) => c switch
    {
        'á' or 'à' or 'ä' or 'â' or 'ã' => 'a',
        'é' or 'è' or 'ë' or 'ê' => 'e',
        'í' or 'ì' or 'ï' or 'î' => 'i',
        'ó' or 'ò' or 'ö' or 'ô' or 'õ' => 'o',
        'ú' or 'ù' or 'ü' or 'û' => 'u',
        'ñ' => 'n',
        'ç' => 'c',
        _ => c,
    };
}

/// <summary>Valor concreto de un <see cref="CampoPersonalizado"/> para una ficha (cliente o animal).</summary>
public sealed class ValorCampoPersonalizado : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaValor = 2000;

    private ValorCampoPersonalizado(Guid id)
        : base(id, Guid.Empty)
    {
        Valor = null!;
    }

    private ValorCampoPersonalizado(Guid id, Guid empresaId, Guid campoId, EntidadPersonalizable entidad, Guid registroId, string valor, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        CampoId = campoId;
        Entidad = entidad;
        RegistroId = registroId;
        Valor = valor;
        ActualizadoEn = ahora;
    }

    /// <summary>Campo (definición) al que corresponde este valor.</summary>
    public Guid CampoId { get; private set; }

    /// <summary>Entidad del registro (redundante con el campo, pero acelera y aísla las consultas por ficha).</summary>
    public EntidadPersonalizable Entidad { get; private set; }

    /// <summary>Identificador de la ficha (cliente o animal) a la que pertenece el valor.</summary>
    public Guid RegistroId { get; private set; }

    /// <summary>Valor ya normalizado (según el tipo del campo). Se guarda siempre como texto.</summary>
    public string Valor { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static ValorCampoPersonalizado Crear(Guid empresaId, Guid campoId, EntidadPersonalizable entidad, Guid registroId, string valor, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        return new ValorCampoPersonalizado(Guid.NewGuid(), empresaId, campoId, entidad, registroId, valor, reloj.AhoraUtc);
    }

    public void Establecer(string valor, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Valor = valor;
        ActualizadoEn = reloj.AhoraUtc;
    }
}
