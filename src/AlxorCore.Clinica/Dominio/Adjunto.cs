using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Se ha subido un adjunto a la ficha de un animal.</summary>
public sealed record AdjuntoCreado(Guid AdjuntoId, Guid EmpresaId, Guid AnimalId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Fichero adjunto a la ficha de un animal: fotos (heridas, radiografías) o documentos (PDF). El
/// contenido se guarda en la base de datos (bytea), así entra en la copia de seguridad de la clínica.
/// </summary>
public sealed class Adjunto : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 200;
    public const int LongitudMaximaTipoMime = 120;

    /// <summary>Tamaño máximo por adjunto (15 MB).</summary>
    public const int TamanoMaximo = 15 * 1024 * 1024;

    private static readonly string[] MimesPermitidos =
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/heic", "image/bmp", "application/pdf",
    };

    private Adjunto(Guid id)
        : base(id, Guid.Empty)
    {
        NombreArchivo = null!;
        TipoMime = null!;
        Datos = Array.Empty<byte>();
    }

    private Adjunto(Guid id, Guid empresaId, Guid animalId, string nombreArchivo, string tipoMime, byte[] datos, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        AnimalId = animalId;
        NombreArchivo = nombreArchivo;
        TipoMime = tipoMime;
        Datos = datos;
        Tamano = datos.Length;
        CreadoEn = ahora;
    }

    public Guid AnimalId { get; private set; }

    public string NombreArchivo { get; private set; }

    public string TipoMime { get; private set; }

    /// <summary>Tamaño en bytes.</summary>
    public int Tamano { get; private set; }

    /// <summary>Contenido binario del fichero.</summary>
    public byte[] Datos { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>¿Es una imagen (para mostrar miniatura en la ficha)?</summary>
    public bool EsImagen => TipoMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public static Resultado<Adjunto> Crear(Guid empresaId, Guid animalId, string? nombreArchivo, string? tipoMime, byte[]? datos, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (animalId == Guid.Empty)
        {
            return Resultado.Fallo<Adjunto>(Error.Validacion("adjunto.animal_obligatorio", "El adjunto debe pertenecer a un animal."));
        }

        if (datos is null || datos.Length == 0)
        {
            return Resultado.Fallo<Adjunto>(Error.Validacion("adjunto.vacio", "El fichero está vacío."));
        }

        if (datos.Length > TamanoMaximo)
        {
            return Resultado.Fallo<Adjunto>(Error.Validacion("adjunto.demasiado_grande", "El fichero supera el tamaño máximo permitido (15 MB)."));
        }

        var mime = (tipoMime ?? string.Empty).Trim().ToLowerInvariant();
        if (!MimesPermitidos.Contains(mime))
        {
            return Resultado.Fallo<Adjunto>(Error.Validacion("adjunto.tipo_no_permitido", "Solo se admiten imágenes (JPG, PNG, GIF, WEBP) o PDF."));
        }

        var nombre = string.IsNullOrWhiteSpace(nombreArchivo) ? "adjunto" : nombreArchivo.Trim();
        if (nombre.Length > LongitudMaximaNombre)
        {
            nombre = nombre[^LongitudMaximaNombre..];
        }

        return Resultado.Ok(new Adjunto(Guid.NewGuid(), empresaId, animalId, nombre, mime, datos, reloj.AhoraUtc));
    }
}
