using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Facturacion.Tests;

public sealed class RelojFijo : IReloj
{
    public DateTimeOffset AhoraUtc { get; init; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

public class FacturaTests
{
    private static readonly IReloj Reloj = new RelojFijo();

    private static readonly ClienteFacturado Cliente =
        new(Guid.NewGuid(), "Cliente SL", "B12345674", "Calle 1", "28001", "Madrid", "Madrid", "ES");

    private static NumeroFactura Numero() => new("FA", 2026, 1);

    private static NuevaLinea Linea(decimal cantidad, decimal precio, decimal iva, decimal descuento = 0) =>
        new("Concepto", cantidad, precio, "IVA21", iva, descuento);

    private static readonly DateOnly Fecha = new(2026, 1, 15);

    [Fact]
    public void Emitir_calcula_base_iva_y_total_sin_irpf()
    {
        var factura = Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente,
            [Linea(2m, 100m, 21m)], 0m, Reloj).Valor;

        factura.BaseImponible.Should().Be(200m);
        factura.CuotaIva.Should().Be(42m);
        factura.RetencionIrpf.Should().Be(0m);
        factura.Total.Should().Be(242m);
        factura.Estado.Should().Be(EstadoFactura.Emitida);
        factura.NumeroCompleto.Should().Be("FA2026/000001");
    }

    [Fact]
    public void Emitir_aplica_retencion_de_irpf()
    {
        var factura = Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente,
            [Linea(1m, 1000m, 21m)], 15m, Reloj).Valor;

        factura.BaseImponible.Should().Be(1000m);
        factura.CuotaIva.Should().Be(210m);
        factura.RetencionIrpf.Should().Be(150m);
        factura.Total.Should().Be(1060m); // 1000 + 210 - 150
    }

    [Fact]
    public void Emitir_aplica_recargo_de_equivalencia()
    {
        // Línea al 21 % con recargo de equivalencia del 5,2 %.
        var linea = new NuevaLinea("Concepto", 1m, 1000m, "IVA21", 21m, PorcentajeRecargo: 5.2m);
        var factura = Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente, [linea], 0m, Reloj).Valor;

        factura.BaseImponible.Should().Be(1000m);
        factura.CuotaIva.Should().Be(210m);
        factura.RecargoEquivalencia.Should().BeTrue();
        factura.RecargoTotal.Should().Be(52m);   // 5,2 % de 1000
        factura.Total.Should().Be(1262m);         // 1000 + 210 + 52
    }

    [Fact]
    public void Emitir_redondea_a_dos_decimales()
    {
        // 33,33 × 3 = 99,99; IVA 21% = 20,9979 -> 21,00
        var factura = Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente,
            [Linea(3m, 33.33m, 21m)], 0m, Reloj).Valor;

        factura.BaseImponible.Should().Be(99.99m);
        factura.CuotaIva.Should().Be(21.00m);
        factura.Total.Should().Be(120.99m);
    }

    [Fact]
    public void Emitir_suma_varias_lineas_con_distinto_iva()
    {
        var factura = Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente,
            [Linea(1m, 100m, 21m), new NuevaLinea("Libro", 1m, 50m, "IVA4", 4m)], 0m, Reloj).Valor;

        factura.BaseImponible.Should().Be(150m);
        factura.CuotaIva.Should().Be(23m); // 21 + 2
        factura.Total.Should().Be(173m);
        factura.Lineas.Should().HaveCount(2);
    }

    [Fact]
    public void Emitir_aplica_descuento_de_linea()
    {
        // 100 con 10% descuento = 90; IVA 21% = 18,90
        var factura = Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente,
            [Linea(1m, 100m, 21m, descuento: 10m)], 0m, Reloj).Valor;

        factura.BaseImponible.Should().Be(90m);
        factura.CuotaIva.Should().Be(18.90m);
    }

    [Fact]
    public void Emitir_falla_sin_lineas()
    {
        Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente, [], 0m, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Emitir_falla_si_fecha_operacion_posterior_a_emision()
    {
        var resultado = Factura.Emitir(Guid.NewGuid(), Numero(), new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20),
            Cliente, [Linea(1m, 100m, 21m)], 0m, Reloj);

        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("factura.fechas");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Emitir_falla_con_cantidad_no_positiva(decimal cantidad)
    {
        Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente, [Linea(cantidad, 100m, 21m)], 0m, Reloj)
            .EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Emitir_falla_con_irpf_excesivo()
    {
        Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente, [Linea(1m, 100m, 21m)], 61m, Reloj)
            .EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Emitir_guarda_las_observaciones_recortadas()
    {
        var factura = Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente,
            [Linea(1m, 100m, 21m)], 0m, Reloj, observaciones: "  Pago a 30 días fin de mes.  ").Valor;

        factura.Observaciones.Should().Be("Pago a 30 días fin de mes.");
    }

    [Fact]
    public void Emitir_sin_observaciones_las_deja_nulas()
    {
        var factura = Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente, [Linea(1m, 100m, 21m)], 0m, Reloj).Valor;
        factura.Observaciones.Should().BeNull();
    }

    [Fact]
    public void Emitir_rechaza_observaciones_demasiado_largas()
    {
        var largo = new string('x', Factura.LongitudMaximaObservaciones + 1);
        var resultado = Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente, [Linea(1m, 100m, 21m)], 0m, Reloj, observaciones: largo);

        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("factura.observaciones_largas");
    }

    [Fact]
    public void Emitir_congela_los_datos_del_cliente()
    {
        var factura = Factura.Emitir(Guid.NewGuid(), Numero(), Fecha, Fecha, Cliente, [Linea(1m, 100m, 21m)], 0m, Reloj).Valor;

        factura.ClienteNombre.Should().Be("Cliente SL");
        factura.ClienteNif.Should().Be("B12345674");
        factura.EventosDominio.Should().ContainSingle(e => e is FacturaEmitida);
    }
}
