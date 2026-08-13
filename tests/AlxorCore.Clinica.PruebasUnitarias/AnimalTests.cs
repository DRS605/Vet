using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

/// <summary>Reloj fijo para pruebas deterministas (1 de enero de 2026).</summary>
public sealed class RelojFijo : IReloj
{
    public RelojFijo()
    {
    }

    public RelojFijo(DateTimeOffset ahora) => AhoraUtc = ahora;

    public DateTimeOffset AhoraUtc { get; init; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

public class AnimalTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 1, 1);

    [Fact]
    public void Crear_animal_valido_emite_evento_y_queda_activo()
    {
        var animal = Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj, raza: "Beagle");

        animal.EsCorrecto.Should().BeTrue();
        animal.Valor.Activo.Should().BeTrue();
        animal.Valor.EmpresaId.Should().Be(Empresa);
        animal.Valor.ClienteId.Should().Be(Cliente);
        animal.Valor.Nombre.Should().Be("Toby");
        animal.Valor.Especie.Should().Be(EspecieAnimal.Perro);
        animal.Valor.EventosDominio.Should().ContainSingle(e => e is AnimalCreado);
    }

    [Fact]
    public void Crear_normaliza_el_microchip()
    {
        var animal = Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj, microchip: " 941 000 abc ").Valor;
        animal.Microchip.Should().Be("941000ABC");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Crear_rechaza_nombre_vacio(string? nombre)
    {
        Animal.Crear(Empresa, Cliente, nombre, EspecieAnimal.Perro, SexoAnimal.Macho, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_nombre_demasiado_largo()
    {
        var largo = new string('a', Animal.LongitudMaximaNombre + 1);
        Animal.Crear(Empresa, Cliente, largo, EspecieAnimal.Perro, SexoAnimal.Macho, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_cliente_vacio()
    {
        Animal.Crear(Empresa, Guid.Empty, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj).EsFallo.Should().BeTrue();
    }

    [Theory]
    [InlineData(EspecieAnimal.Gato, SexoAnimal.Hembra)]
    [InlineData(EspecieAnimal.Conejo, SexoAnimal.Desconocido)]
    [InlineData(EspecieAnimal.Reptil, SexoAnimal.Macho)]
    public void Crear_acepta_especies_y_sexos_validos(EspecieAnimal especie, SexoAnimal sexo)
    {
        Animal.Crear(Empresa, Cliente, "Mascota", especie, sexo, Reloj).EsCorrecto.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_especie_invalida()
    {
        Animal.Crear(Empresa, Cliente, "Mascota", (EspecieAnimal)999, SexoAnimal.Macho, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_sexo_invalido()
    {
        Animal.Crear(Empresa, Cliente, "Mascota", EspecieAnimal.Perro, (SexoAnimal)999, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_fecha_nacimiento_futura()
    {
        var futura = new DateOnly(2026, 6, 1);
        Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj, fechaNacimiento: futura).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_acepta_fecha_nacimiento_de_hoy()
    {
        Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj, fechaNacimiento: Hoy).EsCorrecto.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_rechaza_peso_no_positivo(decimal peso)
    {
        Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj, pesoKg: peso).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_acepta_peso_positivo()
    {
        var animal = Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj, pesoKg: 12.345m).Valor;
        animal.PesoKg.Should().Be(12.345m);
    }

    [Fact]
    public void EdadMeses_es_nula_sin_fecha_de_nacimiento()
    {
        var animal = Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj).Valor;
        animal.EdadMeses(Hoy).Should().BeNull();
    }

    [Fact]
    public void EdadMeses_cuenta_meses_completos()
    {
        // Nacido hace 18 meses exactos.
        var animal = Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj, fechaNacimiento: new DateOnly(2024, 7, 1)).Valor;
        animal.EdadMeses(Hoy).Should().Be(18);
    }

    [Fact]
    public void EsCachorro_falso_sin_fecha_de_nacimiento()
    {
        var animal = Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj).Valor;
        animal.EsCachorro(Hoy).Should().BeFalse();
    }

    [Fact]
    public void EsCachorro_verdadero_para_perro_por_debajo_del_umbral()
    {
        // Perro de 11 meses: umbral 12 → cachorro.
        var animal = Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj, fechaNacimiento: new DateOnly(2025, 2, 1)).Valor;
        animal.EdadMeses(Hoy).Should().Be(11);
        animal.EsCachorro(Hoy).Should().BeTrue();
    }

    [Fact]
    public void EsCachorro_falso_justo_en_el_umbral_para_perro()
    {
        // Perro de exactamente 12 meses: umbral 12 → ya no cachorro (edad < umbral es estricto).
        var animal = Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj, fechaNacimiento: new DateOnly(2025, 1, 1)).Valor;
        animal.EdadMeses(Hoy).Should().Be(12);
        animal.EsCachorro(Hoy).Should().BeFalse();
    }

    [Fact]
    public void EsCachorro_conejo_umbral_seis_meses()
    {
        // Conejo de 5 meses: umbral 6 → cachorro.
        var cachorro = Animal.Crear(Empresa, Cliente, "Bugs", EspecieAnimal.Conejo, SexoAnimal.Macho, Reloj, fechaNacimiento: new DateOnly(2025, 8, 1)).Valor;
        cachorro.EdadMeses(Hoy).Should().Be(5);
        cachorro.EsCachorro(Hoy).Should().BeTrue();

        // Conejo de 6 meses: umbral 6 → ya no cachorro.
        var adulto = Animal.Crear(Empresa, Cliente, "Roger", EspecieAnimal.Conejo, SexoAnimal.Macho, Reloj, fechaNacimiento: new DateOnly(2025, 7, 1)).Valor;
        adulto.EdadMeses(Hoy).Should().Be(6);
        adulto.EsCachorro(Hoy).Should().BeFalse();
    }

    [Fact]
    public void Actualizar_cambia_los_datos()
    {
        var animal = Animal.Crear(Empresa, Cliente, "Antiguo", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj).Valor;

        var r = animal.Actualizar("Nuevo", EspecieAnimal.Gato, SexoAnimal.Hembra, Reloj, raza: "Siamés", esterilizado: true, pesoKg: 4.2m);

        r.EsCorrecto.Should().BeTrue();
        animal.Nombre.Should().Be("Nuevo");
        animal.Especie.Should().Be(EspecieAnimal.Gato);
        animal.Sexo.Should().Be(SexoAnimal.Hembra);
        animal.Raza.Should().Be("Siamés");
        animal.Esterilizado.Should().BeTrue();
        animal.PesoKg.Should().Be(4.2m);
    }

    [Fact]
    public void Actualizar_rechaza_datos_invalidos()
    {
        var animal = Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj).Valor;
        animal.Actualizar("", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj).EsFallo.Should().BeTrue();
        animal.Nombre.Should().Be("Toby", "un fallo de validación no debe mutar el animal");
    }

    [Fact]
    public void Desactivar_marca_inactivo()
    {
        var animal = Animal.Crear(Empresa, Cliente, "Toby", EspecieAnimal.Perro, SexoAnimal.Macho, Reloj).Valor;
        animal.Desactivar(Reloj);
        animal.Activo.Should().BeFalse();
    }
}
