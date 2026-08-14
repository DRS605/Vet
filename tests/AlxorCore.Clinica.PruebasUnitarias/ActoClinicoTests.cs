using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class ActoClinicoTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Animal = Guid.NewGuid();
    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly DateOnly Fecha = new(2026, 1, 1);

    private static ActoClinico Nuevo() =>
        ActoClinico.Crear(Empresa, Animal, Cliente, Fecha, "Consulta + vacuna polivalente", 40m, Reloj).Valor;

    [Fact]
    public void Crear_acto_valido_emite_evento_y_queda_pendiente()
    {
        var acto = ActoClinico.Crear(
            Empresa, Animal, Cliente, Fecha, "Consulta general", 30m, Reloj,
            porcentajeIva: 10m, referenciaTipo: "consulta", referenciaId: Guid.NewGuid());

        acto.EsCorrecto.Should().BeTrue();
        acto.Valor.EmpresaId.Should().Be(Empresa);
        acto.Valor.AnimalId.Should().Be(Animal);
        acto.Valor.ClienteId.Should().Be(Cliente);
        acto.Valor.Fecha.Should().Be(Fecha);
        acto.Valor.Concepto.Should().Be("Consulta general");
        acto.Valor.Importe.Should().Be(30m);
        acto.Valor.PorcentajeIva.Should().Be(10m);
        acto.Valor.ReferenciaTipo.Should().Be("consulta");
        acto.Valor.Estado.Should().Be(EstadoActo.Pendiente);
        acto.Valor.FacturaId.Should().BeNull();
        acto.Valor.CobradoTicketEn.Should().BeNull();
        acto.Valor.EventosDominio.Should().ContainSingle(e => e is ActoClinicoRegistrado);
    }

    [Fact]
    public void Crear_aplica_el_iva_por_defecto_del_21()
    {
        var acto = Nuevo();
        acto.PorcentajeIva.Should().Be(21m);
        acto.CodigoIva().Should().Be("IVA21");
    }

    [Fact]
    public void Crear_normaliza_concepto_y_referencia()
    {
        var acto = ActoClinico.Crear(Empresa, Animal, Cliente, Fecha, "  Radiografía  ", 25m, Reloj, referenciaTipo: "  ").Valor;
        acto.Concepto.Should().Be("Radiografía");
        acto.ReferenciaTipo.Should().BeNull();
    }

    [Fact]
    public void Crear_rechaza_animal_vacio()
    {
        var acto = ActoClinico.Crear(Empresa, Guid.Empty, Cliente, Fecha, "Consulta", 10m, Reloj);
        acto.EsFallo.Should().BeTrue();
        acto.Error.Codigo.Should().Be("acto.animal_obligatorio");
    }

    [Fact]
    public void Crear_rechaza_cliente_vacio()
    {
        var acto = ActoClinico.Crear(Empresa, Animal, Guid.Empty, Fecha, "Consulta", 10m, Reloj);
        acto.EsFallo.Should().BeTrue();
        acto.Error.Codigo.Should().Be("acto.cliente_obligatorio");
    }

    [Fact]
    public void Crear_rechaza_concepto_vacio()
    {
        var acto = ActoClinico.Crear(Empresa, Animal, Cliente, Fecha, "   ", 10m, Reloj);
        acto.EsFallo.Should().BeTrue();
        acto.Error.Codigo.Should().Be("acto.concepto_vacio");
    }

    [Fact]
    public void Crear_rechaza_concepto_demasiado_largo()
    {
        var largo = new string('a', ActoClinico.LongitudMaximaConcepto + 1);
        var acto = ActoClinico.Crear(Empresa, Animal, Cliente, Fecha, largo, 10m, Reloj);
        acto.EsFallo.Should().BeTrue();
        acto.Error.Codigo.Should().Be("acto.concepto_largo");
    }

    [Fact]
    public void Crear_rechaza_importe_negativo()
    {
        var acto = ActoClinico.Crear(Empresa, Animal, Cliente, Fecha, "Consulta", -1m, Reloj);
        acto.EsFallo.Should().BeTrue();
        acto.Error.Codigo.Should().Be("acto.importe_negativo");
    }

    [Fact]
    public void Crear_admite_importe_cero()
    {
        ActoClinico.Crear(Empresa, Animal, Cliente, Fecha, "Cortesía", 0m, Reloj).EsCorrecto.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(21)]
    public void Crear_admite_los_ivas_validos(int iva)
    {
        ActoClinico.Crear(Empresa, Animal, Cliente, Fecha, "Consulta", 10m, Reloj, porcentajeIva: iva).EsCorrecto.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_iva_invalido()
    {
        var acto = ActoClinico.Crear(Empresa, Animal, Cliente, Fecha, "Consulta", 10m, Reloj, porcentajeIva: 7m);
        acto.EsFallo.Should().BeTrue();
        acto.Error.Codigo.Should().Be("acto.iva_invalido");
    }

    [Fact]
    public void MarcarTicket_avanza_a_ticket_y_fija_el_momento()
    {
        var acto = Nuevo();
        acto.MarcarTicket(Reloj).EsCorrecto.Should().BeTrue();
        acto.Estado.Should().Be(EstadoActo.Ticket);
        acto.CobradoTicketEn.Should().Be(Reloj.AhoraUtc);
    }

    [Fact]
    public void MarcarFacturado_fija_estado_y_factura()
    {
        var acto = Nuevo();
        var facturaId = Guid.NewGuid();

        acto.MarcarFacturado(facturaId, Reloj).EsCorrecto.Should().BeTrue();
        acto.Estado.Should().Be(EstadoActo.Facturado);
        acto.FacturaId.Should().Be(facturaId);
    }

    [Fact]
    public void MarcarFacturado_rechaza_factura_vacia()
    {
        var acto = Nuevo();
        var resultado = acto.MarcarFacturado(Guid.Empty, Reloj);
        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("acto.factura_obligatoria");
        acto.Estado.Should().Be(EstadoActo.Pendiente);
    }

    [Fact]
    public void Anular_desde_pendiente()
    {
        var acto = Nuevo();
        acto.Anular(Reloj).EsCorrecto.Should().BeTrue();
        acto.Estado.Should().Be(EstadoActo.Anulado);
    }

    [Fact]
    public void Actualizar_cambia_los_datos_solo_en_pendiente()
    {
        var acto = Nuevo();
        var resultado = acto.Actualizar(new DateOnly(2026, 2, 1), "Consulta + analítica", 55m, 10m, Reloj);

        resultado.EsCorrecto.Should().BeTrue();
        acto.Fecha.Should().Be(new DateOnly(2026, 2, 1));
        acto.Concepto.Should().Be("Consulta + analítica");
        acto.Importe.Should().Be(55m);
        acto.PorcentajeIva.Should().Be(10m);
    }

    [Fact]
    public void Actualizar_un_facturado_es_transicion_invalida()
    {
        var acto = Nuevo();
        acto.MarcarFacturado(Guid.NewGuid(), Reloj);

        var resultado = acto.Actualizar(Fecha, "Otro", 10m, null, Reloj);
        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("acto.transicion_invalida");
    }

    [Fact]
    public void Facturar_un_ya_facturado_es_transicion_invalida()
    {
        var acto = Nuevo();
        acto.MarcarFacturado(Guid.NewGuid(), Reloj);

        var resultado = acto.MarcarFacturado(Guid.NewGuid(), Reloj);
        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("acto.transicion_invalida");
    }

    [Fact]
    public void Ticket_sobre_un_anulado_es_transicion_invalida()
    {
        var acto = Nuevo();
        acto.Anular(Reloj);

        var resultado = acto.MarcarTicket(Reloj);
        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("acto.transicion_invalida");
    }

    [Fact]
    public void Facturar_un_cobrado_con_ticket_es_transicion_invalida()
    {
        var acto = Nuevo();
        acto.MarcarTicket(Reloj);

        acto.MarcarFacturado(Guid.NewGuid(), Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Anular_un_ticket_es_transicion_invalida()
    {
        var acto = Nuevo();
        acto.MarcarTicket(Reloj);

        acto.Anular(Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void CodigoIva_traduce_el_porcentaje_al_codigo_del_catalogo()
    {
        ActoClinico.Crear(Empresa, Animal, Cliente, Fecha, "A", 10m, Reloj, porcentajeIva: 4m).Valor.CodigoIva().Should().Be("IVA4");
        ActoClinico.Crear(Empresa, Animal, Cliente, Fecha, "B", 10m, Reloj, porcentajeIva: 0m).Valor.CodigoIva().Should().Be("IVA0");
    }
}
