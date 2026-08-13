using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class ConsultaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Animal = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 1, 1);

    [Fact]
    public void Crear_consulta_valida_emite_evento_y_queda_activa()
    {
        var consulta = Consulta.Crear(Empresa, Animal, Hoy, Reloj, motivo: "Revisión anual", diagnostico: "Sano", pesoKg: 12.5m, veterinario: "Dra. López");

        consulta.EsCorrecto.Should().BeTrue();
        consulta.Valor.Activo.Should().BeTrue();
        consulta.Valor.EmpresaId.Should().Be(Empresa);
        consulta.Valor.AnimalId.Should().Be(Animal);
        consulta.Valor.Fecha.Should().Be(Hoy);
        consulta.Valor.Motivo.Should().Be("Revisión anual");
        consulta.Valor.PesoKg.Should().Be(12.5m);
        consulta.Valor.EventosDominio.Should().ContainSingle(e => e is ConsultaRegistrada);
    }

    [Fact]
    public void Crear_normaliza_las_cadenas_vacias_a_nulo()
    {
        var consulta = Consulta.Crear(Empresa, Animal, Hoy, Reloj, motivo: "   ", diagnostico: " Otitis ").Valor;
        consulta.Motivo.Should().BeNull();
        consulta.Diagnostico.Should().Be("Otitis");
    }

    [Fact]
    public void Crear_rechaza_animal_vacio()
    {
        Consulta.Crear(Empresa, Guid.Empty, Hoy, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_fecha_futura()
    {
        var futura = new DateOnly(2026, 6, 1);
        Consulta.Crear(Empresa, Animal, futura, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_acepta_fecha_de_hoy()
    {
        Consulta.Crear(Empresa, Animal, Hoy, Reloj).EsCorrecto.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_motivo_demasiado_largo()
    {
        var largo = new string('a', Consulta.LongitudMaximaMotivo + 1);
        Consulta.Crear(Empresa, Animal, Hoy, Reloj, motivo: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_diagnostico_demasiado_largo()
    {
        var largo = new string('a', Consulta.LongitudMaximaDiagnostico + 1);
        Consulta.Crear(Empresa, Animal, Hoy, Reloj, diagnostico: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_tratamiento_demasiado_largo()
    {
        var largo = new string('a', Consulta.LongitudMaximaTratamiento + 1);
        Consulta.Crear(Empresa, Animal, Hoy, Reloj, tratamiento: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_veterinario_demasiado_largo()
    {
        var largo = new string('a', Consulta.LongitudMaximaVeterinario + 1);
        Consulta.Crear(Empresa, Animal, Hoy, Reloj, veterinario: largo).EsFallo.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_rechaza_peso_no_positivo(decimal peso)
    {
        Consulta.Crear(Empresa, Animal, Hoy, Reloj, pesoKg: peso).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_acepta_peso_positivo()
    {
        var consulta = Consulta.Crear(Empresa, Animal, Hoy, Reloj, pesoKg: 3.250m).Valor;
        consulta.PesoKg.Should().Be(3.250m);
    }

    [Fact]
    public void Actualizar_cambia_los_datos()
    {
        var consulta = Consulta.Crear(Empresa, Animal, Hoy, Reloj, motivo: "Antiguo").Valor;

        var r = consulta.Actualizar(new DateOnly(2025, 12, 15), Reloj, motivo: "Nuevo", diagnostico: "Gastritis", tratamiento: "Dieta blanda", pesoKg: 10m, veterinario: "Dr. Ruiz");

        r.EsCorrecto.Should().BeTrue();
        consulta.Fecha.Should().Be(new DateOnly(2025, 12, 15));
        consulta.Motivo.Should().Be("Nuevo");
        consulta.Diagnostico.Should().Be("Gastritis");
        consulta.Tratamiento.Should().Be("Dieta blanda");
        consulta.PesoKg.Should().Be(10m);
        consulta.Veterinario.Should().Be("Dr. Ruiz");
    }

    [Fact]
    public void Actualizar_rechaza_datos_invalidos_y_no_muta()
    {
        var consulta = Consulta.Crear(Empresa, Animal, Hoy, Reloj, motivo: "Original").Valor;
        consulta.Actualizar(new DateOnly(2026, 6, 1), Reloj).EsFallo.Should().BeTrue();
        consulta.Fecha.Should().Be(Hoy, "un fallo de validación no debe mutar la consulta");
        consulta.Motivo.Should().Be("Original");
    }

    [Fact]
    public void Anular_marca_inactiva()
    {
        var consulta = Consulta.Crear(Empresa, Animal, Hoy, Reloj).Valor;
        consulta.Anular(Reloj);
        consulta.Activo.Should().BeFalse();
    }
}
