using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>
/// Pruebas de integración del puente de facturación de actos clínicos: registrar actos, cobrarlos con
/// ticket y facturar varios del mismo cliente emitiendo <b>una factura real</b> del módulo Facturación
/// (numeración correlativa + IVA + VeriFactu), reutilizando el montaje de facturación existente.
/// </summary>
public sealed class ActoFacturacionEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public ActoFacturacionEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record AnimalResp(Guid Id, Guid ClienteId, string Nombre);
    private sealed record ActoResp(
        Guid Id, Guid AnimalId, Guid ClienteId, decimal Importe, decimal PorcentajeIva, string Estado, Guid? FacturaId);
    private sealed record FacturaResp(Guid Id, string NumeroCompleto, decimal BaseImponible, decimal CuotaIva, decimal Total, string Estado);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente, string nombre = "Propietario SL")
    {
        var creado = await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = nombre, NifFiscal = "B12345674" })).Content.ReadFromJsonAsync<ClienteResp>();
        return creado!.Id;
    }

    private static async Task<AnimalResp> CrearAnimalAsync(HttpClient cliente, Guid clienteId, string nombre = "Toby")
    {
        var creado = await (await cliente.PostAsJsonAsync("/animales", new { ClienteId = clienteId, Nombre = nombre, Especie = "Perro", Sexo = "Macho" })).Content.ReadFromJsonAsync<AnimalResp>();
        return creado!;
    }

    private static async Task<ActoResp> RegistrarActoAsync(HttpClient cliente, Guid animalId, string concepto, decimal importe, decimal iva = 21m)
    {
        var creado = await cliente.PostAsJsonAsync($"/animales/{animalId}/actos", new { Concepto = concepto, Importe = importe, PorcentajeIva = iva });
        creado.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await creado.Content.ReadFromJsonAsync<ActoResp>())!;
    }

    [Fact]
    public async Task Registrar_acto_sobre_un_animal_queda_pendiente_con_el_cliente_del_animal()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var animal = await CrearAnimalAsync(cliente, clienteId);

        var acto = await RegistrarActoAsync(cliente, animal.Id, "Consulta + vacuna", 40m);

        acto.AnimalId.Should().Be(animal.Id);
        acto.ClienteId.Should().Be(clienteId);
        acto.Importe.Should().Be(40m);
        acto.Estado.Should().Be("Pendiente");
        acto.FacturaId.Should().BeNull();
    }

    [Fact]
    public async Task Cobrar_un_acto_con_ticket_lo_marca()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var animal = await CrearAnimalAsync(cliente, clienteId);
        var acto = await RegistrarActoAsync(cliente, animal.Id, "Corte de uñas", 12m);

        var ticket = await cliente.PostAsync(new Uri($"/actos/{acto.Id}/ticket", UriKind.Relative), content: null);
        ticket.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualizado = await ticket.Content.ReadFromJsonAsync<ActoResp>();
        actualizado!.Estado.Should().Be("Ticket");

        var pendientes = await cliente.GetFromJsonAsync<List<ActoResp>>("/actos?estado=Pendiente");
        pendientes!.Should().NotContain(a => a.Id == acto.Id);
    }

    [Fact]
    public async Task Facturar_varios_actos_del_mismo_cliente_emite_una_unica_factura_real()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var animal = await CrearAnimalAsync(cliente, clienteId);

        var acto1 = await RegistrarActoAsync(cliente, animal.Id, "Consulta", 100m, iva: 21m);
        var acto2 = await RegistrarActoAsync(cliente, animal.Id, "Pienso terapéutico", 50m, iva: 10m);

        var facturar = await cliente.PostAsJsonAsync("/actos/facturar", new { ActoIds = new[] { acto1.Id, acto2.Id } });
        facturar.StatusCode.Should().Be(HttpStatusCode.Created);
        var factura = await facturar.Content.ReadFromJsonAsync<FacturaResp>();

        // Una sola factura del módulo Facturación, con numeración correlativa y total = suma con IVA.
        factura!.NumeroCompleto.Should().EndWith("/000001");
        factura.BaseImponible.Should().Be(150m);          // 100 + 50
        factura.CuotaIva.Should().Be(26m);                // 21 (21% de 100) + 5 (10% de 50)
        factura.Total.Should().Be(176m);                  // 150 + 26

        // La factura existe de verdad en Facturación y es consultable por su endpoint.
        var enFacturacion = await cliente.GetFromJsonAsync<FacturaResp>($"/facturas/{factura.Id}");
        enFacturacion!.Total.Should().Be(176m);

        // Los dos actos quedan Facturado con ese FacturaId.
        foreach (var id in new[] { acto1.Id, acto2.Id })
        {
            var acto = await cliente.GetFromJsonAsync<ActoResp>($"/actos/{id}");
            acto!.Estado.Should().Be("Facturado");
            acto.FacturaId.Should().Be(factura.Id);
        }
    }

    [Fact]
    public async Task Facturar_actos_de_clientes_distintos_devuelve_error_y_no_toca_los_actos()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(cliente, "Cliente A");
        var clienteB = await CrearClienteAsync(cliente, "Cliente B");
        var animalA = await CrearAnimalAsync(cliente, clienteA, "Rex");
        var animalB = await CrearAnimalAsync(cliente, clienteB, "Micha");

        var actoA = await RegistrarActoAsync(cliente, animalA.Id, "Consulta A", 30m);
        var actoB = await RegistrarActoAsync(cliente, animalB.Id, "Consulta B", 40m);

        var facturar = await cliente.PostAsJsonAsync("/actos/facturar", new { ActoIds = new[] { actoA.Id, actoB.Id } });
        facturar.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Ningún acto ha quedado facturado.
        (await cliente.GetFromJsonAsync<ActoResp>($"/actos/{actoA.Id}"))!.Estado.Should().Be("Pendiente");
        (await cliente.GetFromJsonAsync<ActoResp>($"/actos/{actoB.Id}"))!.Estado.Should().Be("Pendiente");
    }

    [Fact]
    public async Task Los_actos_estan_aislados_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(empresaA, "Cliente de A");
        var animalA = await CrearAnimalAsync(empresaA, clienteA);
        var actoA = await RegistrarActoAsync(empresaA, animalA.Id, "Consulta de A", 25m);

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        // Empresa B no ve los actos de A...
        var listaB = await empresaB.GetFromJsonAsync<List<ActoResp>>("/actos?estado=Pendiente");
        listaB!.Should().BeEmpty();

        // ...ni puede facturarlos (no existen en su ámbito).
        var facturar = await empresaB.PostAsJsonAsync("/actos/facturar", new { ActoIds = new[] { actoA.Id } });
        facturar.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
