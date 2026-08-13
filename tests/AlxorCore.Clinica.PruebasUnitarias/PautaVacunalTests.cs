using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class PautaVacunalTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Fact]
    public void Crear_pauta_valida_emite_evento_y_queda_activa()
    {
        var pauta = PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, "Polivalente (DHPPi/L)", CaracterVacuna.Recomendada, Reloj, edadInicioSemanas: 6, periodicidadRefuerzoMeses: 12);

        pauta.EsCorrecto.Should().BeTrue();
        pauta.Valor.Activo.Should().BeTrue();
        pauta.Valor.EmpresaId.Should().Be(Empresa);
        pauta.Valor.Especie.Should().Be(EspecieAnimal.Perro);
        pauta.Valor.Nombre.Should().Be("Polivalente (DHPPi/L)");
        pauta.Valor.Caracter.Should().Be(CaracterVacuna.Recomendada);
        pauta.Valor.EdadInicioSemanas.Should().Be(6);
        pauta.Valor.PeriodicidadRefuerzoMeses.Should().Be(12);
        pauta.Valor.EventosDominio.Should().ContainSingle(e => e is PautaVacunalCreada);
    }

    [Fact]
    public void Crear_recorta_el_nombre()
    {
        var pauta = PautaVacunal.Crear(Empresa, EspecieAnimal.Gato, "  Rabia  ", CaracterVacuna.Legal, Reloj).Valor;
        pauta.Nombre.Should().Be("Rabia");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Crear_rechaza_nombre_vacio(string? nombre)
    {
        PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, nombre, CaracterVacuna.Recomendada, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_nombre_demasiado_largo()
    {
        var largo = new string('a', PautaVacunal.LongitudMaximaNombre + 1);
        PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, largo, CaracterVacuna.Recomendada, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_especie_invalida()
    {
        PautaVacunal.Crear(Empresa, (EspecieAnimal)999, "Vacuna", CaracterVacuna.Recomendada, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_caracter_invalido()
    {
        PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, "Vacuna", (CaracterVacuna)999, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_edad_inicio_negativa()
    {
        PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, "Vacuna", CaracterVacuna.Recomendada, Reloj, edadInicioSemanas: -1).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_acepta_edad_inicio_cero()
    {
        PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, "Vacuna", CaracterVacuna.Recomendada, Reloj, edadInicioSemanas: 0).EsCorrecto.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_rechaza_periodicidad_no_positiva(int periodicidad)
    {
        PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, "Vacuna", CaracterVacuna.Recomendada, Reloj, periodicidadRefuerzoMeses: periodicidad).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_acepta_sin_periodicidad_ni_edad()
    {
        var pauta = PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, "Dosis única", CaracterVacuna.Opcional, Reloj).Valor;
        pauta.PeriodicidadRefuerzoMeses.Should().BeNull();
        pauta.EdadInicioSemanas.Should().BeNull();
    }

    [Fact]
    public void Actualizar_cambia_los_datos()
    {
        var pauta = PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, "Antigua", CaracterVacuna.Recomendada, Reloj).Valor;

        var r = pauta.Actualizar(EspecieAnimal.Gato, "Nueva", CaracterVacuna.Legal, Reloj, edadInicioSemanas: 8, periodicidadRefuerzoMeses: 24);

        r.EsCorrecto.Should().BeTrue();
        pauta.Especie.Should().Be(EspecieAnimal.Gato);
        pauta.Nombre.Should().Be("Nueva");
        pauta.Caracter.Should().Be(CaracterVacuna.Legal);
        pauta.EdadInicioSemanas.Should().Be(8);
        pauta.PeriodicidadRefuerzoMeses.Should().Be(24);
    }

    [Fact]
    public void Actualizar_rechaza_datos_invalidos_y_no_muta()
    {
        var pauta = PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, "Original", CaracterVacuna.Recomendada, Reloj).Valor;
        pauta.Actualizar(EspecieAnimal.Perro, "", CaracterVacuna.Recomendada, Reloj).EsFallo.Should().BeTrue();
        pauta.Nombre.Should().Be("Original", "un fallo de validación no debe mutar la pauta");
    }

    [Fact]
    public void Desactivar_marca_inactiva()
    {
        var pauta = PautaVacunal.Crear(Empresa, EspecieAnimal.Perro, "Vacuna", CaracterVacuna.Recomendada, Reloj).Valor;
        pauta.Desactivar(Reloj);
        pauta.Activo.Should().BeFalse();
    }

    [Fact]
    public void CalcularProximaDosis_suma_los_meses_de_periodicidad()
    {
        var fecha = new DateOnly(2026, 1, 15);
        PautaVacunal.CalcularProximaDosis(fecha, 12).Should().Be(new DateOnly(2027, 1, 15));
        PautaVacunal.CalcularProximaDosis(fecha, 3).Should().Be(new DateOnly(2026, 4, 15));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalcularProximaDosis_es_nula_sin_periodicidad_positiva(int? periodicidad)
    {
        PautaVacunal.CalcularProximaDosis(new DateOnly(2026, 1, 15), periodicidad).Should().BeNull();
    }
}
