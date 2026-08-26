using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class EspecieTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Fact]
    public void Crear_especie_valida_emite_evento_y_queda_activa()
    {
        var especie = Especie.Crear(Empresa, "Tortuga", 24, Reloj);

        especie.EsCorrecto.Should().BeTrue();
        especie.Valor.Activo.Should().BeTrue();
        especie.Valor.EmpresaId.Should().Be(Empresa);
        especie.Valor.Nombre.Should().Be("Tortuga");
        especie.Valor.MesesCachorro.Should().Be(24);
        especie.Valor.EventosDominio.Should().ContainSingle(e => e is EspecieCreada);
    }

    [Fact]
    public void Crear_recorta_el_nombre()
    {
        var especie = Especie.Crear(Empresa, "  Perro  ", 12, Reloj).Valor;
        especie.Nombre.Should().Be("Perro");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Crear_rechaza_nombre_vacio(string? nombre)
    {
        Especie.Crear(Empresa, nombre, 12, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_nombre_demasiado_largo()
    {
        var largo = new string('a', Especie.LongitudMaximaNombre + 1);
        Especie.Crear(Empresa, largo, 12, Reloj).EsFallo.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_rechaza_meses_cachorro_no_positivos(int meses)
    {
        Especie.Crear(Empresa, "Perro", meses, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_cambia_nombre_y_umbral()
    {
        var especie = Especie.Crear(Empresa, "Antigua", 12, Reloj).Valor;

        var r = especie.Actualizar("Nueva", 6, Reloj);

        r.EsCorrecto.Should().BeTrue();
        especie.Nombre.Should().Be("Nueva");
        especie.MesesCachorro.Should().Be(6);
    }

    [Fact]
    public void Actualizar_rechaza_datos_invalidos_y_no_muta()
    {
        var especie = Especie.Crear(Empresa, "Perro", 12, Reloj).Valor;
        especie.Actualizar("", 12, Reloj).EsFallo.Should().BeTrue();
        especie.Nombre.Should().Be("Perro", "un fallo de validación no debe mutar la especie");
    }

    [Fact]
    public void Desactivar_marca_inactiva()
    {
        var especie = Especie.Crear(Empresa, "Perro", 12, Reloj).Valor;
        especie.Desactivar(Reloj);
        especie.Activo.Should().BeFalse();
    }
}
