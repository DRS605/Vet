using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Clinica.PruebasUnitarias;

public class CampoPersonalizadoTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    private static CampoPersonalizado Crear(TipoCampo tipo, string etiqueta = "Campo", string? opciones = null, bool obligatorio = false) =>
        CampoPersonalizado.Crear(Empresa, EntidadPersonalizable.Animal, etiqueta, tipo, opciones, obligatorio, 0, Reloj).Valor;

    [Fact]
    public void Crear_campo_valido_emite_evento_y_queda_activo()
    {
        var campo = CampoPersonalizado.Crear(Empresa, EntidadPersonalizable.Cliente, "Alergias", TipoCampo.TextoLargo, null, false, 3, Reloj);

        campo.EsCorrecto.Should().BeTrue();
        campo.Valor.Activo.Should().BeTrue();
        campo.Valor.EmpresaId.Should().Be(Empresa);
        campo.Valor.Entidad.Should().Be(EntidadPersonalizable.Cliente);
        campo.Valor.Etiqueta.Should().Be("Alergias");
        campo.Valor.Orden.Should().Be(3);
        campo.Valor.EventosDominio.Should().ContainSingle(e => e is CampoPersonalizadoCreado);
    }

    [Theory]
    [InlineData("Nº de chip", "n_de_chip")]
    [InlineData("  Alergias graves  ", "alergias_graves")]
    [InlineData("Peso (kg)", "peso_kg")]
    [InlineData("Cámara/Jaula", "camara_jaula")]
    public void La_clave_se_normaliza_desde_la_etiqueta(string etiqueta, string claveEsperada)
    {
        Crear(TipoCampo.Texto, etiqueta).Clave.Should().Be(claveEsperada);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Crear_rechaza_etiqueta_vacia(string? etiqueta)
    {
        CampoPersonalizado.Crear(Empresa, EntidadPersonalizable.Animal, etiqueta, TipoCampo.Texto, null, false, 0, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Una_etiqueta_solo_de_simbolos_es_invalida()
    {
        CampoPersonalizado.Crear(Empresa, EntidadPersonalizable.Animal, "!!!", TipoCampo.Texto, null, false, 0, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Una_lista_sin_opciones_es_invalida()
    {
        CampoPersonalizado.Crear(Empresa, EntidadPersonalizable.Animal, "Temperamento", TipoCampo.Lista, "   ", false, 0, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Las_opciones_solo_se_guardan_para_las_listas()
    {
        Crear(TipoCampo.Texto, "Nota", "a\nb").Opciones.Should().BeNull();
        Crear(TipoCampo.Lista, "Tipo", "Tranquilo\n Nervioso \n\n").OpcionesLista.Should().BeEquivalentTo("Tranquilo", "Nervioso");
    }

    [Fact]
    public void Actualizar_recalcula_la_clave_y_cambia_el_tipo()
    {
        var campo = Crear(TipoCampo.Texto, "Chip");
        campo.Actualizar("Microchip mascota", TipoCampo.Numero, null, true, 5, Reloj).EsCorrecto.Should().BeTrue();
        campo.Etiqueta.Should().Be("Microchip mascota");
        campo.Clave.Should().Be("microchip_mascota");
        campo.Tipo.Should().Be(TipoCampo.Numero);
        campo.Obligatorio.Should().BeTrue();
        campo.Orden.Should().Be(5);
    }

    [Fact]
    public void Desactivar_hace_baja_logica()
    {
        var campo = Crear(TipoCampo.Texto);
        campo.Desactivar(Reloj);
        campo.Activo.Should().BeFalse();
    }

    [Fact]
    public void Valor_obligatorio_vacio_falla_y_opcional_vacio_es_nulo()
    {
        Crear(TipoCampo.Texto, obligatorio: true).NormalizarValor("  ").EsFallo.Should().BeTrue();
        var opcional = Crear(TipoCampo.Texto, obligatorio: false).NormalizarValor(null);
        opcional.EsCorrecto.Should().BeTrue();
        opcional.Valor.Should().BeNull();
    }

    [Theory]
    [InlineData("12,50", "12.50")]
    [InlineData("12.50", "12.50")]
    [InlineData("8", "8")]
    public void Numero_acepta_coma_o_punto_y_normaliza_a_invariante(string entrada, string esperado)
    {
        var r = Crear(TipoCampo.Numero).NormalizarValor(entrada);
        r.EsCorrecto.Should().BeTrue();
        r.Valor.Should().Be(esperado);
    }

    [Fact]
    public void Numero_rechaza_texto()
    {
        Crear(TipoCampo.Numero).NormalizarValor("abc").EsFallo.Should().BeTrue();
    }

    [Theory]
    [InlineData("2026-08-26", "2026-08-26")]
    [InlineData("26/08/2026", "2026-08-26")]
    public void Fecha_normaliza_a_iso(string entrada, string esperado)
    {
        var r = Crear(TipoCampo.Fecha).NormalizarValor(entrada);
        r.EsCorrecto.Should().BeTrue();
        r.Valor.Should().Be(esperado);
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData("sí", "true")]
    [InlineData("1", "true")]
    [InlineData("false", "false")]
    [InlineData("no", "false")]
    public void Booleano_normaliza_a_true_o_false(string entrada, string esperado)
    {
        Crear(TipoCampo.Booleano).NormalizarValor(entrada).Valor.Should().Be(esperado);
    }

    [Fact]
    public void Lista_solo_admite_una_opcion_valida_sin_distinguir_mayusculas()
    {
        var campo = Crear(TipoCampo.Lista, "Temperamento", "Tranquilo\nNervioso");
        campo.NormalizarValor("tranquilo").Valor.Should().Be("Tranquilo");
        campo.NormalizarValor("Agresivo").EsFallo.Should().BeTrue();
    }
}
