using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class CirugiaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Animal = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 1, 1);

    [Fact]
    public void Crear_cirugia_valida_emite_evento_y_queda_activa()
    {
        var cirugia = Cirugia.Crear(
            Empresa, Animal, Hoy, "Esterilización (OVH)", Reloj,
            descripcion: "Sin incidencias", cirujano: "Dra. López", anestesia: "Isoflurano",
            proximaRevision: new DateOnly(2026, 1, 11));

        cirugia.EsCorrecto.Should().BeTrue();
        cirugia.Valor.Activo.Should().BeTrue();
        cirugia.Valor.EmpresaId.Should().Be(Empresa);
        cirugia.Valor.AnimalId.Should().Be(Animal);
        cirugia.Valor.Fecha.Should().Be(Hoy);
        cirugia.Valor.Nombre.Should().Be("Esterilización (OVH)");
        cirugia.Valor.Cirujano.Should().Be("Dra. López");
        cirugia.Valor.ProximaRevision.Should().Be(new DateOnly(2026, 1, 11));
        cirugia.Valor.EventosDominio.Should().ContainSingle(e => e is CirugiaRegistrada);
    }

    [Fact]
    public void Crear_normaliza_las_cadenas_vacias_a_nulo()
    {
        var cirugia = Cirugia.Crear(Empresa, Animal, Hoy, "  Castración  ", Reloj, descripcion: "   ", cirujano: " Dr. Ruiz ").Valor;
        cirugia.Nombre.Should().Be("Castración");
        cirugia.Descripcion.Should().BeNull();
        cirugia.Cirujano.Should().Be("Dr. Ruiz");
    }

    [Fact]
    public void Crear_rechaza_animal_vacio()
    {
        Cirugia.Crear(Empresa, Guid.Empty, Hoy, "OVH", Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_fecha_futura()
    {
        var futura = new DateOnly(2026, 6, 1);
        Cirugia.Crear(Empresa, Animal, futura, "OVH", Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_acepta_fecha_de_hoy()
    {
        Cirugia.Crear(Empresa, Animal, Hoy, "OVH", Reloj).EsCorrecto.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_nombre_vacio()
    {
        Cirugia.Crear(Empresa, Animal, Hoy, "   ", Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_nombre_demasiado_largo()
    {
        var largo = new string('a', Cirugia.LongitudMaximaNombre + 1);
        Cirugia.Crear(Empresa, Animal, Hoy, largo, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_descripcion_demasiado_larga()
    {
        var largo = new string('a', Cirugia.LongitudMaximaDescripcion + 1);
        Cirugia.Crear(Empresa, Animal, Hoy, "OVH", Reloj, descripcion: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_cirujano_demasiado_largo()
    {
        var largo = new string('a', Cirugia.LongitudMaximaCirujano + 1);
        Cirugia.Crear(Empresa, Animal, Hoy, "OVH", Reloj, cirujano: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_anestesia_demasiado_larga()
    {
        var largo = new string('a', Cirugia.LongitudMaximaAnestesia + 1);
        Cirugia.Crear(Empresa, Animal, Hoy, "OVH", Reloj, anestesia: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_complicaciones_demasiado_largas()
    {
        var largo = new string('a', Cirugia.LongitudMaximaComplicaciones + 1);
        Cirugia.Crear(Empresa, Animal, Hoy, "OVH", Reloj, complicaciones: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_revision_anterior_a_la_fecha()
    {
        Cirugia.Crear(Empresa, Animal, Hoy, "OVH", Reloj, proximaRevision: new DateOnly(2025, 12, 31)).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_acepta_revision_igual_a_la_fecha()
    {
        Cirugia.Crear(Empresa, Animal, Hoy, "OVH", Reloj, proximaRevision: Hoy).EsCorrecto.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_cambia_los_datos()
    {
        var cirugia = Cirugia.Crear(Empresa, Animal, Hoy, "Antiguo", Reloj).Valor;

        var r = cirugia.Actualizar(
            new DateOnly(2025, 12, 15), "Nuevo", Reloj,
            descripcion: "Reintervención", cirujano: "Dr. Ruiz", anestesia: "Propofol",
            complicaciones: "Ninguna", proximaRevision: new DateOnly(2025, 12, 25));

        r.EsCorrecto.Should().BeTrue();
        cirugia.Fecha.Should().Be(new DateOnly(2025, 12, 15));
        cirugia.Nombre.Should().Be("Nuevo");
        cirugia.Descripcion.Should().Be("Reintervención");
        cirugia.Cirujano.Should().Be("Dr. Ruiz");
        cirugia.Anestesia.Should().Be("Propofol");
        cirugia.Complicaciones.Should().Be("Ninguna");
        cirugia.ProximaRevision.Should().Be(new DateOnly(2025, 12, 25));
    }

    [Fact]
    public void Actualizar_rechaza_datos_invalidos_y_no_muta()
    {
        var cirugia = Cirugia.Crear(Empresa, Animal, Hoy, "Original", Reloj).Valor;
        cirugia.Actualizar(new DateOnly(2026, 6, 1), "Original", Reloj).EsFallo.Should().BeTrue();
        cirugia.Fecha.Should().Be(Hoy, "un fallo de validación no debe mutar la cirugía");
        cirugia.Nombre.Should().Be("Original");
    }

    [Fact]
    public void Anular_marca_inactiva()
    {
        var cirugia = Cirugia.Crear(Empresa, Animal, Hoy, "OVH", Reloj).Valor;
        cirugia.Anular(Reloj);
        cirugia.Activo.Should().BeFalse();
    }
}
