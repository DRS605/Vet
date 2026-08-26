using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class RazaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Fact]
    public void Crear_raza_valida_emite_evento_y_queda_activa()
    {
        var raza = Raza.Crear(Empresa, "Perro", "Labrador", Reloj);
        raza.EsCorrecto.Should().BeTrue();
        raza.Valor.Activo.Should().BeTrue();
        raza.Valor.Especie.Should().Be("Perro");
        raza.Valor.Nombre.Should().Be("Labrador");
        raza.Valor.EventosDominio.Should().ContainSingle(e => e is RazaCreada);
    }

    [Fact]
    public void Crear_recorta_especie_y_nombre()
    {
        var raza = Raza.Crear(Empresa, "  Gato ", "  Siamés ", Reloj).Valor;
        raza.Especie.Should().Be("Gato");
        raza.Nombre.Should().Be("Siamés");
    }

    [Theory]
    [InlineData("", "Labrador")]
    [InlineData("Perro", "")]
    [InlineData(null, "Labrador")]
    [InlineData("Perro", null)]
    public void Crear_rechaza_vacios(string? especie, string? nombre)
    {
        Raza.Crear(Empresa, especie, nombre, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_cambia_el_nombre_pero_no_la_especie()
    {
        var raza = Raza.Crear(Empresa, "Perro", "Labrador", Reloj).Valor;
        raza.Actualizar("Golden Retriever", Reloj).EsCorrecto.Should().BeTrue();
        raza.Nombre.Should().Be("Golden Retriever");
        raza.Especie.Should().Be("Perro");
    }

    [Fact]
    public void Desactivar_hace_baja_logica()
    {
        var raza = Raza.Crear(Empresa, "Perro", "Labrador", Reloj).Valor;
        raza.Desactivar(Reloj);
        raza.Activo.Should().BeFalse();
    }
}
