using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de la importación CSV de clientes y productos.</summary>
public sealed class ImportacionCsvEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public ImportacionCsvEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ErrorFila(int Fila, string Mensaje);
    private sealed record ResultadoImportacion(int Total, int Correctas, int Importadas, bool Previsualizacion, IReadOnlyList<ErrorFila> Errores);
    private sealed record ClienteResp(Guid Id, string Nombre);
    private sealed record ClienteConTelResp(Guid Id, string Nombre, string? Telefono);

    [Fact]
    public async Task Importa_el_telefono_del_cliente()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var csv = "nombre;telefono;email\nMaría López;+34 600 123 456;maria@x.es";
        var r = await (await cliente.PostAsJsonAsync("/clientes/importar", new { Contenido = csv, Previsualizar = false }))
            .Content.ReadFromJsonAsync<ResultadoImportacion>();
        r!.Importadas.Should().Be(1);

        var lista = await cliente.GetFromJsonAsync<List<ClienteConTelResp>>("/clientes");
        lista!.Single().Telefono.Should().Be("+34 600 123 456");
    }
    private sealed record ProductoResp(Guid Id, string Nombre, string? Referencia);

    [Fact]
    public async Task Previsualizar_no_crea_clientes_pero_valida()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var csv = "nombre;nif;email\nCafé Central SL;B12345674;hola@central.es\n;sin-nombre@x.es;\nLaura Giménez;;laura@x.es";

        var r = await (await cliente.PostAsJsonAsync("/clientes/importar", new { Contenido = csv, Previsualizar = true }))
            .Content.ReadFromJsonAsync<ResultadoImportacion>();

        r!.Total.Should().Be(3);
        r.Correctas.Should().Be(2);
        r.Importadas.Should().Be(0); // previsualización
        r.Errores.Should().ContainSingle(e => e.Fila == 3); // la fila sin nombre

        // No se ha creado nada.
        var lista = await cliente.GetFromJsonAsync<List<ClienteResp>>("/clientes");
        lista.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirmar_crea_los_clientes_validos()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var csv = "nombre,nif,email\nCafé Central SL,B12345674,hola@central.es\nLaura Giménez,,laura@x.es";

        var r = await (await cliente.PostAsJsonAsync("/clientes/importar", new { Contenido = csv, Previsualizar = false }))
            .Content.ReadFromJsonAsync<ResultadoImportacion>();

        r!.Importadas.Should().Be(2);

        var lista = await cliente.GetFromJsonAsync<List<ClienteResp>>("/clientes");
        lista.Should().HaveCount(2);
        lista.Should().Contain(c => c.Nombre == "Café Central SL");
    }

    [Fact]
    public async Task Importa_productos_con_codigo_y_precio_en_formato_espanol()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        // Precio con coma decimal y símbolo €; IVA como número; columna "codigo".
        var csv = "nombre;codigo;precio;iva\nCamiseta;8412345000019;14,90 €;21\nTaza;8412345000026;7,50;10";

        var r = await (await cliente.PostAsJsonAsync("/productos/importar", new { Contenido = csv, Previsualizar = false }))
            .Content.ReadFromJsonAsync<ResultadoImportacion>();

        r!.Importadas.Should().Be(2);

        var lista = await cliente.GetFromJsonAsync<List<ProductoResp>>("/productos");
        lista.Should().Contain(p => p.Referencia == "8412345000019");
    }
}
