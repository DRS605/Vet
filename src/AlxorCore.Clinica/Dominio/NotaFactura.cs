using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>
/// Nota interna (no fiscal) asociada a una factura, para el panel de facturas de la clínica. Vive en el
/// módulo Clínica a propósito: NO forma parte del documento VeriFactu (no afecta a la huella ni al QR),
/// es solo una anotación de gestión de la clínica sobre esa factura.
/// </summary>
public sealed class NotaFactura : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaTexto = 2000;

    private NotaFactura(Guid id)
        : base(id, Guid.Empty)
    {
        Texto = null!;
    }

    private NotaFactura(Guid id, Guid empresaId, Guid facturaId, string texto, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        FacturaId = facturaId;
        Texto = texto;
        ActualizadoEn = ahora;
    }

    /// <summary>Factura a la que pertenece la nota (una nota por factura y empresa).</summary>
    public Guid FacturaId { get; private set; }

    /// <summary>Texto libre de la nota (normalizado; se recorta y se limita a 2000).</summary>
    public string Texto { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static NotaFactura Crear(Guid empresaId, Guid facturaId, string texto, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        return new NotaFactura(Guid.NewGuid(), empresaId, facturaId, Normalizar(texto), reloj.AhoraUtc);
    }

    public void Establecer(string texto, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Texto = Normalizar(texto);
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static string Normalizar(string? texto)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length > LongitudMaximaTexto ? t[..LongitudMaximaTexto] : t;
    }
}
