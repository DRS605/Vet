using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class VacunacionTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Animal = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 1, 1);

    [Fact]
    public void Crear_vacunacion_valida_emite_evento_y_queda_activa()
    {
        var pautaId = Guid.NewGuid();
        var vacunacion = Vacunacion.Crear(Empresa, Animal, "Rabia", Hoy, Reloj, pautaVacunalId: pautaId, lote: "L-2026-001", proximaDosis: new DateOnly(2027, 1, 1), veterinario: "Dra. López", notas: "Sin reacción");

        vacunacion.EsCorrecto.Should().BeTrue();
        vacunacion.Valor.Activo.Should().BeTrue();
        vacunacion.Valor.EmpresaId.Should().Be(Empresa);
        vacunacion.Valor.AnimalId.Should().Be(Animal);
        vacunacion.Valor.PautaVacunalId.Should().Be(pautaId);
        vacunacion.Valor.Nombre.Should().Be("Rabia");
        vacunacion.Valor.FechaAplicacion.Should().Be(Hoy);
        vacunacion.Valor.Lote.Should().Be("L-2026-001");
        vacunacion.Valor.ProximaDosis.Should().Be(new DateOnly(2027, 1, 1));
        vacunacion.Valor.EventosDominio.Should().ContainSingle(e => e is VacunacionRegistrada);
    }

    [Fact]
    public void Crear_normaliza_las_cadenas_vacias_a_nulo()
    {
        var vacunacion = Vacunacion.Crear(Empresa, Animal, "Rabia", Hoy, Reloj, lote: "   ", veterinario: " Dr. Ruiz ").Valor;
        vacunacion.Lote.Should().BeNull();
        vacunacion.Veterinario.Should().Be("Dr. Ruiz");
    }

    [Fact]
    public void Crear_es_adhoc_sin_pauta()
    {
        var vacunacion = Vacunacion.Crear(Empresa, Animal, "Vacuna ad-hoc", Hoy, Reloj).Valor;
        vacunacion.PautaVacunalId.Should().BeNull();
    }

    [Fact]
    public void Crear_rechaza_animal_vacio()
    {
        Vacunacion.Crear(Empresa, Guid.Empty, "Rabia", Hoy, Reloj).EsFallo.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Crear_rechaza_nombre_vacio(string? nombre)
    {
        Vacunacion.Crear(Empresa, Animal, nombre, Hoy, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_nombre_demasiado_largo()
    {
        var largo = new string('a', Vacunacion.LongitudMaximaNombre + 1);
        Vacunacion.Crear(Empresa, Animal, largo, Hoy, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_fecha_futura()
    {
        var futura = new DateOnly(2026, 6, 1);
        Vacunacion.Crear(Empresa, Animal, "Rabia", futura, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_acepta_fecha_de_hoy()
    {
        Vacunacion.Crear(Empresa, Animal, "Rabia", Hoy, Reloj).EsCorrecto.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_lote_demasiado_largo()
    {
        var largo = new string('a', Vacunacion.LongitudMaximaLote + 1);
        Vacunacion.Crear(Empresa, Animal, "Rabia", Hoy, Reloj, lote: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_veterinario_demasiado_largo()
    {
        var largo = new string('a', Vacunacion.LongitudMaximaVeterinario + 1);
        Vacunacion.Crear(Empresa, Animal, "Rabia", Hoy, Reloj, veterinario: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_notas_demasiado_largas()
    {
        var largo = new string('a', Vacunacion.LongitudMaximaNotas + 1);
        Vacunacion.Crear(Empresa, Animal, "Rabia", Hoy, Reloj, notas: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_cambia_los_datos()
    {
        var vacunacion = Vacunacion.Crear(Empresa, Animal, "Antigua", Hoy, Reloj).Valor;
        var nuevaPauta = Guid.NewGuid();

        var r = vacunacion.Actualizar("Nueva", new DateOnly(2025, 12, 15), Reloj, pautaVacunalId: nuevaPauta, lote: "L-99", proximaDosis: new DateOnly(2026, 12, 15), veterinario: "Dr. Ruiz", notas: "Actualizada");

        r.EsCorrecto.Should().BeTrue();
        vacunacion.Nombre.Should().Be("Nueva");
        vacunacion.FechaAplicacion.Should().Be(new DateOnly(2025, 12, 15));
        vacunacion.PautaVacunalId.Should().Be(nuevaPauta);
        vacunacion.Lote.Should().Be("L-99");
        vacunacion.ProximaDosis.Should().Be(new DateOnly(2026, 12, 15));
        vacunacion.Veterinario.Should().Be("Dr. Ruiz");
        vacunacion.Notas.Should().Be("Actualizada");
    }

    [Fact]
    public void Actualizar_rechaza_datos_invalidos_y_no_muta()
    {
        var vacunacion = Vacunacion.Crear(Empresa, Animal, "Original", Hoy, Reloj).Valor;
        vacunacion.Actualizar("Nueva", new DateOnly(2026, 6, 1), Reloj).EsFallo.Should().BeTrue();
        vacunacion.Nombre.Should().Be("Original", "un fallo de validación no debe mutar la vacunación");
        vacunacion.FechaAplicacion.Should().Be(Hoy);
    }

    [Fact]
    public void Anular_marca_inactiva()
    {
        var vacunacion = Vacunacion.Crear(Empresa, Animal, "Rabia", Hoy, Reloj).Valor;
        vacunacion.Anular(Reloj);
        vacunacion.Activo.Should().BeFalse();
    }
}
