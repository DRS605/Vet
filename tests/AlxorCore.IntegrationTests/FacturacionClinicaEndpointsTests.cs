using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de la nota interna de factura y el listado clínico por factura.</summary>
public sealed class FacturacionClinicaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public FacturacionClinicaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record FacturaClinicaResp(Guid FacturaId, List<string> Especies, List<string> Razas, string? Nota);

    [Fact]
    public async Task Guardar_y_recuperar_la_nota_interna_de_una_factura()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var facturaId = Guid.NewGuid();

        var guardar = await cliente.PutAsJsonAsync($"/facturas-clinica/{facturaId}/nota", new { Texto = "Pendiente de justificante del seguro" });
        guardar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var lista = await cliente.GetFromJsonAsync<List<FacturaClinicaResp>>("/facturas-clinica");
        lista!.Should().Contain(x => x.FacturaId == facturaId && x.Nota == "Pendiente de justificante del seguro");

        // Vaciar la nota la elimina del listado.
        await cliente.PutAsJsonAsync($"/facturas-clinica/{facturaId}/nota", new { Texto = "" });
        var lista2 = await cliente.GetFromJsonAsync<List<FacturaClinicaResp>>("/facturas-clinica");
        lista2!.Should().NotContain(x => x.FacturaId == facturaId);
    }

    [Fact]
    public async Task Actualizar_una_nota_existente()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var facturaId = Guid.NewGuid();
        await cliente.PutAsJsonAsync($"/facturas-clinica/{facturaId}/nota", new { Texto = "Primera" });
        await cliente.PutAsJsonAsync($"/facturas-clinica/{facturaId}/nota", new { Texto = "Segunda" });
        var lista = await cliente.GetFromJsonAsync<List<FacturaClinicaResp>>("/facturas-clinica");
        lista!.Single(x => x.FacturaId == facturaId).Nota.Should().Be("Segunda");
    }

    [Fact]
    public async Task El_listado_clinico_esta_aislado_por_empresa()
    {
        var (a, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var facturaId = Guid.NewGuid();
        await a.PutAsJsonAsync($"/facturas-clinica/{facturaId}/nota", new { Texto = "Solo de A" });

        var (b, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        (await b.GetFromJsonAsync<List<FacturaClinicaResp>>("/facturas-clinica"))!.Should().NotContain(x => x.FacturaId == facturaId);
    }
}
