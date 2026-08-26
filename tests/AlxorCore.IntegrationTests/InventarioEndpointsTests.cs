using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del inventario / stock.</summary>
public sealed class InventarioEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public InventarioEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ArticuloResp(Guid Id, string Nombre, string Categoria, string? Unidad, decimal Stock, decimal StockMinimo, DateOnly? Caducidad, bool Activo, bool BajoStock);

    [Fact]
    public async Task Crear_ajustar_y_dar_de_baja_un_articulo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var crear = await cliente.PostAsJsonAsync("/inventario", new { Nombre = "Amoxicilina 250mg", Categoria = "Medicamento", Unidad = "comprimidos", Stock = 5m, StockMinimo = 10m });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var art = await crear.Content.ReadFromJsonAsync<ArticuloResp>();
        art!.BajoStock.Should().BeTrue("5 <= 10");

        // Entrada de 20 -> stock 25, ya no está bajo mínimo.
        var ajuste = await cliente.PostAsJsonAsync($"/inventario/{art.Id}/ajustar", new { Delta = 20m });
        ajuste.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ajuste.Content.ReadFromJsonAsync<ArticuloResp>())!.Stock.Should().Be(25m);

        // Salida excesiva -> 400.
        var salida = await cliente.PostAsJsonAsync($"/inventario/{art.Id}/ajustar", new { Delta = -100m });
        salida.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var lista = await cliente.GetFromJsonAsync<List<ArticuloResp>>("/inventario");
        lista!.Single(x => x.Id == art.Id).Stock.Should().Be(25m);

        var baja = await cliente.DeleteAsync(new Uri($"/inventario/{art.Id}", UriKind.Relative));
        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await cliente.GetFromJsonAsync<List<ArticuloResp>>("/inventario"))!.Should().NotContain(x => x.Id == art.Id);
    }

    [Fact]
    public async Task El_inventario_esta_aislado_por_empresa()
    {
        var (a, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await a.PostAsJsonAsync("/inventario", new { Nombre = "Vacuna rabia", Categoria = "Vacuna", Stock = 3m });
        var (b, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        (await b.GetFromJsonAsync<List<ArticuloResp>>("/inventario"))!.Should().BeEmpty();
    }
}
