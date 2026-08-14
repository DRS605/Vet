using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class RecordatorioTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Animal = Guid.NewGuid();
    private static readonly DateOnly Objetivo = new(2026, 2, 1);

    private static Recordatorio Nuevo() =>
        Recordatorio.Crear(Empresa, Animal, TipoRecordatorio.Vacuna, "Vacuna polivalente de Nala", Objetivo, Reloj).Valor;

    [Fact]
    public void Crear_recordatorio_valido_emite_evento_y_queda_pendiente()
    {
        var recordatorio = Recordatorio.Crear(
            Empresa, Animal, TipoRecordatorio.Vacuna, "Vacuna polivalente de Nala", Objetivo, Reloj,
            notas: "Traer cartilla", referenciaTipo: "vacunacion", referenciaId: Guid.NewGuid());

        recordatorio.EsCorrecto.Should().BeTrue();
        recordatorio.Valor.EmpresaId.Should().Be(Empresa);
        recordatorio.Valor.AnimalId.Should().Be(Animal);
        recordatorio.Valor.Tipo.Should().Be(TipoRecordatorio.Vacuna);
        recordatorio.Valor.Titulo.Should().Be("Vacuna polivalente de Nala");
        recordatorio.Valor.FechaObjetivo.Should().Be(Objetivo);
        recordatorio.Valor.Estado.Should().Be(EstadoRecordatorio.Pendiente);
        recordatorio.Valor.FechaEnvio.Should().BeNull();
        recordatorio.Valor.EventosDominio.Should().ContainSingle(e => e is RecordatorioCreado);
    }

    [Fact]
    public void Crear_normaliza_las_cadenas_vacias_a_nulo()
    {
        var recordatorio = Recordatorio.Crear(Empresa, Animal, TipoRecordatorio.Otro, "  Aviso  ", Objetivo, Reloj, notas: "   ").Valor;
        recordatorio.Titulo.Should().Be("Aviso");
        recordatorio.Notas.Should().BeNull();
    }

    [Fact]
    public void Crear_rechaza_animal_vacio()
    {
        Recordatorio.Crear(Empresa, Guid.Empty, TipoRecordatorio.Vacuna, "Vacuna", Objetivo, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_titulo_vacio()
    {
        Recordatorio.Crear(Empresa, Animal, TipoRecordatorio.Vacuna, "   ", Objetivo, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_titulo_demasiado_largo()
    {
        var largo = new string('a', Recordatorio.LongitudMaximaTitulo + 1);
        Recordatorio.Crear(Empresa, Animal, TipoRecordatorio.Vacuna, largo, Objetivo, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_notas_demasiado_largas()
    {
        var largo = new string('a', Recordatorio.LongitudMaximaNotas + 1);
        Recordatorio.Crear(Empresa, Animal, TipoRecordatorio.Vacuna, "Vacuna", Objetivo, Reloj, notas: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_tipo_invalido()
    {
        Recordatorio.Crear(Empresa, Animal, (TipoRecordatorio)999, "Vacuna", Objetivo, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void MarcarEnviado_fija_estado_y_fecha_de_envio()
    {
        var recordatorio = Nuevo();

        var resultado = recordatorio.MarcarEnviado(Reloj);

        resultado.EsCorrecto.Should().BeTrue();
        recordatorio.Estado.Should().Be(EstadoRecordatorio.Enviado);
        recordatorio.FechaEnvio.Should().Be(Reloj.AhoraUtc);
    }

    [Fact]
    public void MarcarEnviado_falla_si_no_esta_pendiente()
    {
        var recordatorio = Nuevo();
        recordatorio.MarcarEnviado(Reloj);

        var segundo = recordatorio.MarcarEnviado(Reloj);

        segundo.EsFallo.Should().BeTrue();
        segundo.Error.Codigo.Should().Be("recordatorio.no_enviable");
    }

    [Fact]
    public void Completar_marca_completado_desde_pendiente()
    {
        var recordatorio = Nuevo();

        recordatorio.MarcarCompletado(Reloj).EsCorrecto.Should().BeTrue();
        recordatorio.Estado.Should().Be(EstadoRecordatorio.Completado);
    }

    [Fact]
    public void Completar_marca_completado_desde_enviado()
    {
        var recordatorio = Nuevo();
        recordatorio.MarcarEnviado(Reloj);

        recordatorio.MarcarCompletado(Reloj).EsCorrecto.Should().BeTrue();
        recordatorio.Estado.Should().Be(EstadoRecordatorio.Completado);
    }

    [Fact]
    public void Completar_falla_si_esta_cancelado()
    {
        var recordatorio = Nuevo();
        recordatorio.Cancelar(Reloj);

        recordatorio.MarcarCompletado(Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Cancelar_marca_cancelado()
    {
        var recordatorio = Nuevo();

        recordatorio.Cancelar(Reloj).EsCorrecto.Should().BeTrue();
        recordatorio.Estado.Should().Be(EstadoRecordatorio.Cancelado);
    }

    [Fact]
    public void Cancelar_falla_si_ya_esta_completado()
    {
        var recordatorio = Nuevo();
        recordatorio.MarcarCompletado(Reloj);

        recordatorio.Cancelar(Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_cambia_titulo_fecha_y_notas()
    {
        var recordatorio = Nuevo();

        var resultado = recordatorio.Actualizar("Nuevo título", new DateOnly(2026, 3, 1), "Nueva nota", Reloj);

        resultado.EsCorrecto.Should().BeTrue();
        recordatorio.Titulo.Should().Be("Nuevo título");
        recordatorio.FechaObjetivo.Should().Be(new DateOnly(2026, 3, 1));
        recordatorio.Notas.Should().Be("Nueva nota");
    }

    [Fact]
    public void Actualizar_rechaza_titulo_vacio()
    {
        var recordatorio = Nuevo();
        recordatorio.Actualizar("   ", Objetivo, null, Reloj).EsFallo.Should().BeTrue();
    }
}
