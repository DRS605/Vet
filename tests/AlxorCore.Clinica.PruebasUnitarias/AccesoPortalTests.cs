using AlxorCore.Clinica.Aplicacion;
using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class AccesoPortalTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Cliente = Guid.NewGuid();
    private const string TokenValido = "abcdefghijklmnopqrstuvwxyz0123456789-_AB"; // 40 chars, URL-safe

    [Fact]
    public void Crear_acceso_valido_queda_activo_sin_revocar()
    {
        var acceso = AccesoPortal.Crear(Empresa, Cliente, TokenValido, Reloj);

        acceso.EsCorrecto.Should().BeTrue();
        acceso.Valor.EmpresaId.Should().Be(Empresa);
        acceso.Valor.ClienteId.Should().Be(Cliente);
        acceso.Valor.Token.Should().Be(TokenValido);
        acceso.Valor.Activo.Should().BeTrue();
        acceso.Valor.RevocadoEn.Should().BeNull();
        acceso.Valor.CreadoEn.Should().Be(Reloj.AhoraUtc);
    }

    [Fact]
    public void Crear_recorta_el_token()
    {
        var acceso = AccesoPortal.Crear(Empresa, Cliente, "  " + TokenValido + "  ", Reloj);
        acceso.Valor.Token.Should().Be(TokenValido);
    }

    [Fact]
    public void Crear_rechaza_cliente_vacio()
    {
        var acceso = AccesoPortal.Crear(Empresa, Guid.Empty, TokenValido, Reloj);
        acceso.EsFallo.Should().BeTrue();
        acceso.Error.Codigo.Should().Be("acceso_portal.cliente_obligatorio");
    }

    [Fact]
    public void Crear_rechaza_token_vacio()
    {
        var acceso = AccesoPortal.Crear(Empresa, Cliente, "   ", Reloj);
        acceso.EsFallo.Should().BeTrue();
        acceso.Error.Codigo.Should().Be("acceso_portal.token_vacio");
    }

    [Fact]
    public void Crear_rechaza_token_sin_entropia_suficiente()
    {
        var corto = new string('a', AccesoPortal.LongitudMinimaToken - 1);
        var acceso = AccesoPortal.Crear(Empresa, Cliente, corto, Reloj);
        acceso.EsFallo.Should().BeTrue();
        acceso.Error.Codigo.Should().Be("acceso_portal.token_corto");
    }

    [Fact]
    public void Crear_rechaza_token_demasiado_largo()
    {
        var largo = new string('a', AccesoPortal.LongitudMaximaToken + 1);
        var acceso = AccesoPortal.Crear(Empresa, Cliente, largo, Reloj);
        acceso.EsFallo.Should().BeTrue();
        acceso.Error.Codigo.Should().Be("acceso_portal.token_largo");
    }

    [Fact]
    public void Revocar_desactiva_y_marca_la_fecha()
    {
        var revocadoEn = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var acceso = AccesoPortal.Crear(Empresa, Cliente, TokenValido, Reloj).Valor;

        acceso.Revocar(new RelojFijo(revocadoEn));

        acceso.Activo.Should().BeFalse();
        acceso.RevocadoEn.Should().Be(revocadoEn);
    }

    [Fact]
    public void Revocar_es_idempotente_y_conserva_la_primera_fecha()
    {
        var primera = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var acceso = AccesoPortal.Crear(Empresa, Cliente, TokenValido, Reloj).Valor;

        acceso.Revocar(new RelojFijo(primera));
        acceso.Revocar(new RelojFijo(primera.AddDays(5)));

        acceso.Activo.Should().BeFalse();
        acceso.RevocadoEn.Should().Be(primera);
    }
}

public class PlanCrecimientoTests
{
    [Fact]
    public void Un_adulto_no_tiene_hitos()
    {
        var hitos = PlanCrecimiento.Derivar(esCachorro: false, edadMeses: 24, esterilizado: true, numeroVacunaciones: 5);
        hitos.Should().BeEmpty();
    }

    [Fact]
    public void Un_cachorro_recien_llegado_tiene_seis_hitos_con_el_primero_como_actual()
    {
        var hitos = PlanCrecimiento.Derivar(esCachorro: true, edadMeses: 1, esterilizado: false, numeroVacunaciones: 0);

        hitos.Should().HaveCount(6);
        hitos[0].Estado.Should().Be(EstadoHito.Actual);
        hitos.Skip(1).Should().OnlyContain(h => h.Estado == EstadoHito.Pendiente);
    }

    [Fact]
    public void Los_hitos_cumplidos_se_marcan_hechos_y_el_siguiente_es_el_actual()
    {
        // 2 vacunaciones y >= 2 meses: hitos 1 (1 vacuna), 2 (desparasitación por edad) y 3 (2 dosis) hechos.
        var hitos = PlanCrecimiento.Derivar(esCachorro: true, edadMeses: 3, esterilizado: false, numeroVacunaciones: 2);

        hitos[0].Estado.Should().Be(EstadoHito.Hecho);
        hitos[1].Estado.Should().Be(EstadoHito.Hecho);
        hitos[2].Estado.Should().Be(EstadoHito.Hecho);
        hitos[3].Estado.Should().Be(EstadoHito.Actual);
        hitos[4].Estado.Should().Be(EstadoHito.Pendiente);
        hitos[5].Estado.Should().Be(EstadoHito.Pendiente);
    }
}
