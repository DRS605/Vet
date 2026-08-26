using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Aplicacion;

namespace AlxorCore.Facturacion.Aplicacion;

/// <summary>Vista de una línea de factura.</summary>
public sealed record LineaFacturaDto(
    string Descripcion, decimal Cantidad, decimal PrecioUnitario, decimal PorcentajeDescuento,
    string CodigoIva, decimal PorcentajeIva, decimal Base, decimal CuotaIva,
    decimal CosteUnitario, decimal Margen, decimal PorcentajeRecargo, decimal CuotaRecargo);

/// <summary>Datos de una línea de venta para el cálculo de márgenes (informe de beneficio).</summary>
public sealed record LineaMargenDto(Guid? ProductoId, string Descripcion, decimal Cantidad, decimal Ingreso, decimal Coste);

/// <summary>Vista de una factura.</summary>
public sealed record FacturaDto(
    Guid Id,
    string NumeroCompleto,
    DateOnly FechaEmision,
    DateOnly FechaOperacion,
    DateOnly FechaVencimiento,
    Guid? ClienteId,
    string ClienteNombre,
    string? ClienteNif,
    decimal BaseImponible,
    decimal CuotaIva,
    decimal PorcentajeIrpf,
    decimal RetencionIrpf,
    bool RecargoEquivalencia,
    decimal RecargoTotal,
    decimal Total,
    string Estado,
    string Tipo,
    string? Huella,
    string? HuellaAnterior,
    DateTimeOffset? FechaHoraGenRegistro,
    Guid? RectificaFacturaId,
    string? MotivoRectificacion,
    string? Observaciones,
    IReadOnlyList<LineaFacturaDto> Lineas)
{
    public static FacturaDto Desde(Factura f) => new(
        f.Id, f.NumeroCompleto, f.FechaEmision, f.FechaOperacion, f.FechaVencimiento, f.ClienteId, f.ClienteNombre, f.ClienteNif,
        f.BaseImponible, f.CuotaIva, f.PorcentajeIrpf, f.RetencionIrpf, f.RecargoEquivalencia, f.RecargoTotal, f.Total, f.Estado.ToString(), f.TipoFactura.ToString(), f.Huella, f.HuellaAnterior,
        f.FechaHoraGenRegistro,
        f.RectificaFacturaId, f.MotivoRectificacion, f.Observaciones,
        f.Lineas.Select(l => new LineaFacturaDto(
            l.Descripcion, l.Cantidad, l.PrecioUnitario, l.PorcentajeDescuento, l.CodigoIva, l.PorcentajeIva, l.Base, l.CuotaIva,
            l.CosteUnitario, l.Margen, l.PorcentajeRecargo, l.CuotaRecargo)).ToList());
}

/// <summary>Resumen de factura para listados y libros de IVA.</summary>
public sealed record FacturaResumen(
    Guid Id, string NumeroCompleto, DateOnly FechaEmision, DateOnly FechaVencimiento, string ClienteNombre,
    string? ClienteNif, decimal BaseImponible, decimal CuotaIva, decimal RetencionIrpf, decimal Total, string Estado, string Tipo);

/// <summary>Repositorio de facturas (escritura).</summary>
public interface IRepositorioFacturas
{
    Task<Factura?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Factura factura);

    /// <summary>Huella del último registro VeriFactu de la empresa (para el encadenamiento), o null si es el primero.</summary>
    Task<string?> UltimaHuellaAsync(Guid empresaId, CancellationToken ct = default);
}

/// <summary>Consultas de lectura de facturas (las usan la API, Tesorería e Informes).</summary>
public interface IConsultaFacturas
{
    Task<FacturaDto?> ObtenerAsync(Guid facturaId, CancellationToken ct = default);

    Task<IReadOnlyList<FacturaResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default);

    /// <summary>Líneas de las facturas emitidas en un periodo, para el cálculo de márgenes.</summary>
    Task<IReadOnlyList<LineaMargenDto>> ListarLineasMargenAsync(Guid empresaId, DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}

/// <summary>Unidad de trabajo del módulo Facturación.</summary>
public interface IUnidadDeTrabajoFacturacion : IUnidadDeTrabajo;
