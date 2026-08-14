using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class CitaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Animal = Guid.NewGuid();
    private static readonly DateTimeOffset Inicio = new(2026, 2, 1, 10, 30, 0, TimeSpan.Zero);

    private static Cita Nueva() =>
        Cita.Crear(Empresa, Animal, Inicio, Reloj).Valor;

    [Fact]
    public void Crear_cita_valida_emite_evento_y_queda_solicitada()
    {
        var cita = Cita.Crear(
            Empresa, Animal, Inicio, Reloj,
            duracionMinutos: 45, tipo: TipoCita.Vacuna, motivo: "Polivalente", veterinario: "Dra. López", notas: "Traer cartilla");

        cita.EsCorrecto.Should().BeTrue();
        cita.Valor.EmpresaId.Should().Be(Empresa);
        cita.Valor.AnimalId.Should().Be(Animal);
        cita.Valor.Inicio.Should().Be(Inicio);
        cita.Valor.DuracionMinutos.Should().Be(45);
        cita.Valor.Tipo.Should().Be(TipoCita.Vacuna);
        cita.Valor.Motivo.Should().Be("Polivalente");
        cita.Valor.Veterinario.Should().Be("Dra. López");
        cita.Valor.Estado.Should().Be(EstadoCita.Solicitada);
        cita.Valor.EventosDominio.Should().ContainSingle(e => e is CitaCreada);
    }

    [Fact]
    public void Crear_aplica_la_duracion_por_defecto_y_el_tipo_consulta()
    {
        var cita = Nueva();
        cita.DuracionMinutos.Should().Be(Cita.DuracionPorDefectoMinutos);
        cita.Tipo.Should().Be(TipoCita.Consulta);
    }

    [Fact]
    public void Crear_normaliza_las_cadenas_vacias_a_nulo()
    {
        var cita = Cita.Crear(Empresa, Animal, Inicio, Reloj, motivo: "  Revisión  ", veterinario: "   ", notas: "  ").Valor;
        cita.Motivo.Should().Be("Revisión");
        cita.Veterinario.Should().BeNull();
        cita.Notas.Should().BeNull();
    }

    [Fact]
    public void Crear_rechaza_animal_vacio()
    {
        var cita = Cita.Crear(Empresa, Guid.Empty, Inicio, Reloj);
        cita.EsFallo.Should().BeTrue();
        cita.Error.Codigo.Should().Be("cita.animal_obligatorio");
    }

    [Fact]
    public void Crear_rechaza_duracion_no_positiva()
    {
        Cita.Crear(Empresa, Animal, Inicio, Reloj, duracionMinutos: 0).EsFallo.Should().BeTrue();
        var negativa = Cita.Crear(Empresa, Animal, Inicio, Reloj, duracionMinutos: -10);
        negativa.EsFallo.Should().BeTrue();
        negativa.Error.Codigo.Should().Be("cita.duracion_invalida");
    }

    [Fact]
    public void Crear_rechaza_tipo_invalido()
    {
        Cita.Crear(Empresa, Animal, Inicio, Reloj, tipo: (TipoCita)999).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_motivo_demasiado_largo()
    {
        var largo = new string('a', Cita.LongitudMaximaMotivo + 1);
        Cita.Crear(Empresa, Animal, Inicio, Reloj, motivo: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_veterinario_demasiado_largo()
    {
        var largo = new string('a', Cita.LongitudMaximaVeterinario + 1);
        Cita.Crear(Empresa, Animal, Inicio, Reloj, veterinario: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_notas_demasiado_largas()
    {
        var largo = new string('a', Cita.LongitudMaximaNotas + 1);
        Cita.Crear(Empresa, Animal, Inicio, Reloj, notas: largo).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Confirmar_avanza_de_solicitada_a_confirmada()
    {
        var cita = Nueva();
        cita.Confirmar(Reloj).EsCorrecto.Should().BeTrue();
        cita.Estado.Should().Be(EstadoCita.Confirmada);
    }

    [Fact]
    public void Confirmar_falla_si_ya_esta_confirmada()
    {
        var cita = Nueva();
        cita.Confirmar(Reloj);

        var segunda = cita.Confirmar(Reloj);
        segunda.EsFallo.Should().BeTrue();
        segunda.Error.Codigo.Should().Be("cita.transicion_invalida");
    }

    [Fact]
    public void Atender_avanza_a_atendida_desde_confirmada()
    {
        var cita = Nueva();
        cita.Confirmar(Reloj);

        cita.Atender(Reloj).EsCorrecto.Should().BeTrue();
        cita.Estado.Should().Be(EstadoCita.Atendida);
    }

    [Fact]
    public void MarcarNoPresentado_desde_confirmada()
    {
        var cita = Nueva();
        cita.Confirmar(Reloj);

        cita.MarcarNoPresentado(Reloj).EsCorrecto.Should().BeTrue();
        cita.Estado.Should().Be(EstadoCita.NoPresentado);
    }

    [Fact]
    public void Cancelar_desde_solicitada()
    {
        var cita = Nueva();
        cita.Cancelar(Reloj).EsCorrecto.Should().BeTrue();
        cita.Estado.Should().Be(EstadoCita.Cancelada);
    }

    [Fact]
    public void Reprogramar_cambia_inicio_y_duracion_desde_confirmada()
    {
        var cita = Nueva();
        cita.Confirmar(Reloj);
        var nuevo = new DateTimeOffset(2026, 2, 5, 9, 0, 0, TimeSpan.Zero);

        cita.Reprogramar(nuevo, 60, Reloj).EsCorrecto.Should().BeTrue();
        cita.Inicio.Should().Be(nuevo);
        cita.DuracionMinutos.Should().Be(60);
        cita.Estado.Should().Be(EstadoCita.Confirmada);
    }

    [Fact]
    public void Reprogramar_sin_duracion_mantiene_la_actual()
    {
        var cita = Cita.Crear(Empresa, Animal, Inicio, Reloj, duracionMinutos: 20).Valor;
        var nuevo = new DateTimeOffset(2026, 2, 5, 9, 0, 0, TimeSpan.Zero);

        cita.Reprogramar(nuevo, null, Reloj).EsCorrecto.Should().BeTrue();
        cita.DuracionMinutos.Should().Be(20);
    }

    [Fact]
    public void Reprogramar_rechaza_duracion_no_positiva()
    {
        var cita = Nueva();
        var nuevo = new DateTimeOffset(2026, 2, 5, 9, 0, 0, TimeSpan.Zero);
        cita.Reprogramar(nuevo, 0, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Atender_desde_cancelada_es_transicion_invalida()
    {
        var cita = Nueva();
        cita.Cancelar(Reloj);

        var resultado = cita.Atender(Reloj);
        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("cita.transicion_invalida");
    }

    [Fact]
    public void Reprogramar_una_atendida_es_transicion_invalida()
    {
        var cita = Nueva();
        cita.Confirmar(Reloj);
        cita.Atender(Reloj);

        var resultado = cita.Reprogramar(Inicio.AddDays(1), null, Reloj);
        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("cita.transicion_invalida");
    }

    [Fact]
    public void Cancelar_una_atendida_es_transicion_invalida()
    {
        var cita = Nueva();
        cita.Confirmar(Reloj);
        cita.Atender(Reloj);

        cita.Cancelar(Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void No_se_puede_confirmar_una_cancelada()
    {
        var cita = Nueva();
        cita.Cancelar(Reloj);

        cita.Confirmar(Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void MarcarNoPresentado_de_una_no_presentado_es_transicion_invalida()
    {
        var cita = Nueva();
        cita.MarcarNoPresentado(Reloj);

        cita.MarcarNoPresentado(Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_cambia_los_datos_sin_alterar_el_estado()
    {
        var cita = Nueva();
        cita.Confirmar(Reloj);
        var nuevoInicio = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

        var resultado = cita.Actualizar(nuevoInicio, 90, TipoCita.Cirugia, Reloj, motivo: "Preoperatorio", veterinario: "Dr. Gil", notas: "Ayuno");

        resultado.EsCorrecto.Should().BeTrue();
        cita.Inicio.Should().Be(nuevoInicio);
        cita.DuracionMinutos.Should().Be(90);
        cita.Tipo.Should().Be(TipoCita.Cirugia);
        cita.Motivo.Should().Be("Preoperatorio");
        cita.Estado.Should().Be(EstadoCita.Confirmada);
    }

    [Fact]
    public void Actualizar_rechaza_duracion_no_positiva()
    {
        var cita = Nueva();
        cita.Actualizar(Inicio, 0, TipoCita.Consulta, Reloj).EsFallo.Should().BeTrue();
    }
}
