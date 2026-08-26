using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Organizacion.Dominio;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Facturacion.Aplicacion;

/// <summary>Línea de la factura a emitir. Si se indica <see cref="ProductoId"/>, se toman sus datos por defecto.</summary>
public sealed record LineaComando(
    decimal Cantidad,
    string? Descripcion = null,
    decimal? PrecioUnitario = null,
    string? CodigoIva = null,
    decimal PorcentajeDescuento = 0m,
    Guid? ProductoId = null,
    decimal? CosteUnitario = null);

/// <summary>Datos para emitir una factura. <c>DiasVencimiento</c> es el plazo de pago (0 = contado).</summary>
public sealed record EmitirFacturaComando(
    Guid ClienteId,
    IReadOnlyList<LineaComando> Lineas,
    DateOnly? FechaEmision = null,
    DateOnly? FechaOperacion = null,
    decimal? PorcentajeIrpf = null,
    string? Serie = null,
    int? DiasVencimiento = null,
    bool RecargoEquivalencia = false,
    string? Observaciones = null);

/// <summary>
/// Caso de uso estrella: emitir una factura. Compone cliente (Terceros), productos/impuestos
/// (Catálogo) y numeración correlativa (Organización). El número se asigna de forma atómica
/// <b>después</b> de validar todo, para minimizar el riesgo de huecos (invariante F1).
/// </summary>
public sealed class EmitirFactura
{
    private readonly IConsultaClientes _clientes;
    private readonly IConsultaProductos _productos;
    private readonly IServicioNumeracion _numeracion;
    private readonly IRepositorioFacturas _facturas;
    private readonly IConsultaEmpresas _empresas;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;
    private readonly IStockVentas _stock;
    private readonly IReloj _reloj;

    public EmitirFactura(
        IConsultaClientes clientes,
        IConsultaProductos productos,
        IServicioNumeracion numeracion,
        IRepositorioFacturas facturas,
        IConsultaEmpresas empresas,
        IUnidadDeTrabajoFacturacion unidadDeTrabajo,
        IStockVentas stock,
        IReloj reloj)
    {
        _clientes = clientes;
        _productos = productos;
        _numeracion = numeracion;
        _facturas = facturas;
        _empresas = empresas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _stock = stock;
        _reloj = reloj;
    }

    public async Task<Resultado<FacturaDto>> EjecutarAsync(Guid empresaId, EmitirFacturaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (comando.Lineas is null || comando.Lineas.Count == 0)
        {
            return Resultado.Fallo<FacturaDto>(Error.Validacion("factura.sin_lineas", "La factura debe tener al menos una línea."));
        }

        var cliente = await _clientes.ObtenerAsync(comando.ClienteId, ct).ConfigureAwait(false);
        if (cliente is null)
        {
            return Resultado.Fallo<FacturaDto>(Error.NoEncontrado("cliente.no_encontrado", "El cliente no existe."));
        }

        var resolucion = await ResolucionLineasFactura.ResolverAsync(comando.Lineas, _productos, ct, comando.RecargoEquivalencia).ConfigureAwait(false);
        if (resolucion.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(resolucion.Error);
        }

        var lineas = resolucion.Valor;
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var fechaEmision = comando.FechaEmision ?? hoy;
        var fechaOperacion = comando.FechaOperacion ?? fechaEmision;
        var fechaVencimiento = fechaEmision.AddDays(Math.Max(0, comando.DiasVencimiento ?? 0));
        var porcentajeIrpf = comando.PorcentajeIrpf ?? cliente.PorcentajeIrpfDefecto;

        var clienteFacturado = new ClienteFacturado(
            cliente.Id, cliente.Nombre, cliente.NifFiscal,
            cliente.Calle, cliente.CodigoPostal, cliente.Poblacion, cliente.Provincia, cliente.Pais);

        // La numeración es lo último antes de crear y guardar (minimiza huecos).
        var numero = await _numeracion.SiguienteAsync(empresaId, TipoDocumento.Factura, fechaEmision.Year, comando.Serie, ct).ConfigureAwait(false);
        if (numero.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(numero.Error);
        }

        var numeroFactura = new NumeroFactura(numero.Valor.Prefijo, numero.Valor.Ejercicio, numero.Valor.Numero);
        var factura = Factura.Emitir(empresaId, numeroFactura, fechaEmision, fechaOperacion, clienteFacturado, lineas, porcentajeIrpf, _reloj, fechaVencimiento, comando.Observaciones);
        if (factura.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(factura.Error);
        }

        await RegistroVerifactu.AplicarAsync(empresaId, factura.Valor, _empresas, _facturas, _reloj, ct).ConfigureAwait(false);
        _facturas.Agregar(factura.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);

        // Descuento de existencias de los artículos con control de stock (mejor esfuerzo; la
        // factura ya está emitida y es la verdad fiscal).
        var lineasVenta = factura.Valor.Lineas
            .Where(l => l.ProductoId is not null)
            .Select(l => new LineaVenta(l.ProductoId!.Value, l.Cantidad))
            .ToList();
        if (lineasVenta.Count > 0)
        {
            await _stock.DescontarVentaAsync(empresaId, lineasVenta, ct).ConfigureAwait(false);
        }

        return Resultado.Ok(FacturaDto.Desde(factura.Valor));
    }
}

/// <summary>
/// Genera el registro VeriFactu de una factura recién emitida: obtiene la huella del registro
/// anterior de la empresa (encadenamiento) y el NIF del emisor, y calcula la huella. Lo comparten la
/// emisión de facturas y de tickets.
/// </summary>
/// <remarks>
/// La huella anterior se lee justo antes de calcular; se asume emisión secuencial por empresa (misma
/// premisa que la numeración). Endurecer la cadena con un bloqueo por empresa ante emisión concurrente
/// es una mejora futura documentada.
/// </remarks>
internal static class RegistroVerifactu
{
    public static async Task AplicarAsync(
        Guid empresaId, Factura factura, IConsultaEmpresas empresas, IRepositorioFacturas facturas, IReloj reloj, CancellationToken ct)
    {
        var emisor = await empresas.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        var huellaAnterior = await facturas.UltimaHuellaAsync(empresaId, ct).ConfigureAwait(false);
        factura.RegistrarVerifactu(emisor?.Nif ?? string.Empty, huellaAnterior, reloj.AhoraUtc);
    }
}

/// <summary>
/// Resuelve las líneas de comando a líneas de dominio (<see cref="NuevaLinea"/>), tomando los datos
/// por defecto del producto cuando se indica <c>ProductoId</c>. Lo comparten la emisión de facturas
/// y de tickets.
/// </summary>
internal static class ResolucionLineasFactura
{
    public static async Task<Resultado<List<NuevaLinea>>> ResolverAsync(
        IReadOnlyList<LineaComando> lineas, IConsultaProductos productos, CancellationToken ct, bool recargoEquivalencia = false)
    {
        var resueltas = new List<NuevaLinea>(lineas.Count);
        foreach (var linea in lineas)
        {
            string? descripcion = linea.Descripcion;
            decimal? precio = linea.PrecioUnitario;
            string? codigoIva = linea.CodigoIva;
            decimal? coste = linea.CosteUnitario;

            if (linea.ProductoId is not null)
            {
                var producto = await productos.ObtenerAsync(linea.ProductoId.Value, ct).ConfigureAwait(false);
                if (producto is null)
                {
                    return Resultado.Fallo<List<NuevaLinea>>(Error.NoEncontrado("producto.no_encontrado", "El producto de una línea no existe."));
                }

                descripcion ??= producto.Nombre;
                precio ??= producto.PrecioUnitario;
                codigoIva ??= producto.CodigoIva;
                coste ??= producto.PrecioCompra;
            }

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                return Resultado.Fallo<List<NuevaLinea>>(Error.Validacion("factura.linea_sin_descripcion", "Cada línea necesita una descripción."));
            }

            if (precio is null)
            {
                return Resultado.Fallo<List<NuevaLinea>>(Error.Validacion("factura.linea_sin_precio", "Cada línea necesita un precio."));
            }

            var impuesto = Impuesto.PorCodigoImpuesto(codigoIva ?? Impuesto.IvaGeneral.Codigo);
            if (impuesto.EsFallo)
            {
                return Resultado.Fallo<List<NuevaLinea>>(impuesto.Error);
            }

            var porcentajeRecargo = recargoEquivalencia ? Impuesto.RecargoEquivalencia(impuesto.Valor.Porcentaje) : 0m;
            resueltas.Add(new NuevaLinea(
                descripcion, linea.Cantidad, precio.Value, impuesto.Valor.Codigo, impuesto.Valor.Porcentaje, linea.PorcentajeDescuento, linea.ProductoId, coste ?? 0m, porcentajeRecargo));
        }

        return Resultado.Ok(resueltas);
    }
}

/// <summary>Caso de uso: listar las facturas de la empresa activa.</summary>
public sealed class ListarFacturas
{
    private readonly IConsultaFacturas _consulta;

    public ListarFacturas(IConsultaFacturas consulta) => _consulta = consulta;

    public Task<IReadOnlyList<FacturaResumen>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, ct);
}

/// <summary>Caso de uso: obtener una factura por su identificador.</summary>
public sealed class ObtenerFactura
{
    private readonly IConsultaFacturas _consulta;

    public ObtenerFactura(IConsultaFacturas consulta) => _consulta = consulta;

    public async Task<Resultado<FacturaDto>> EjecutarAsync(Guid facturaId, CancellationToken ct = default)
    {
        var factura = await _consulta.ObtenerAsync(facturaId, ct).ConfigureAwait(false);
        return factura is null
            ? Resultado.Fallo<FacturaDto>(Error.NoEncontrado("factura.no_encontrada", "La factura no existe."))
            : Resultado.Ok(factura);
    }
}
