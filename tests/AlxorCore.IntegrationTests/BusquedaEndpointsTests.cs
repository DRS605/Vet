using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del buscador global (/buscar): clientes y animales de la empresa activa.</summary>
public sealed class BusquedaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public BusquedaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id, string Nombre);

    private sealed record AnimalResp(Guid Id, Guid ClienteId, string Nombre);

    private sealed record ResultadoBusquedaResp(string Tipo, Guid Id, Guid? ClienteId, string Etiqueta, string? Subetiqueta);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente, string nombre, string? nif = null)
    {
        var creado = await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = nombre, NifFiscal = nif })).Content.ReadFromJsonAsync<ClienteResp>();
        return creado!.Id;
    }

    private static async Task<Guid> CrearAnimalAsync(HttpClient cliente, Guid clienteId, string nombre, string? microchip = null)
    {
        var creado = await (await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId,
            Nombre = nombre,
            Especie = "Perro",
            Sexo = "Macho",
            Microchip = microchip,
        })).Content.ReadFromJsonAsync<AnimalResp>();
        return creado!.Id;
    }

    [Fact]
    public async Task Encuentra_cliente_por_nombre()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Laura Martínez");

        var resultados = await cliente.GetFromJsonAsync<List<ResultadoBusquedaResp>>("/buscar?q=Laura");

        resultados!.Should().ContainSingle(r => r.Tipo == "cliente" && r.Id == clienteId && r.Etiqueta == "Laura Martínez");
    }

    [Fact]
    public async Task Encuentra_cliente_por_nif()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var nif = Ayudas.GenerarNif();
        var clienteId = await CrearClienteAsync(cliente, "Pedro Gómez", nif);

        var resultados = await cliente.GetFromJsonAsync<List<ResultadoBusquedaResp>>($"/buscar?q={nif}");

        resultados!.Should().Contain(r => r.Tipo == "cliente" && r.Id == clienteId);
    }

    [Fact]
    public async Task Encuentra_animal_por_nombre()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueña de Nala");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Nala");

        var resultados = await cliente.GetFromJsonAsync<List<ResultadoBusquedaResp>>("/buscar?q=Nala");

        var animal = resultados!.Should().ContainSingle(r => r.Tipo == "animal" && r.Id == animalId).Subject;
        animal.ClienteId.Should().Be(clienteId);
    }

    [Fact]
    public async Task Encuentra_animal_por_microchip()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño con chip");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Rocky", "941000000123456");

        var resultados = await cliente.GetFromJsonAsync<List<ResultadoBusquedaResp>>("/buscar?q=941000000123456");

        resultados!.Should().Contain(r => r.Tipo == "animal" && r.Id == animalId);
    }

    [Fact]
    public async Task Termino_demasiado_corto_no_devuelve_resultados()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Aurora");
        await CrearAnimalAsync(cliente, clienteId, "Ada");

        var resultados = await cliente.GetFromJsonAsync<List<ResultadoBusquedaResp>>("/buscar?q=A");

        resultados!.Should().BeEmpty();
    }

    [Fact]
    public async Task La_busqueda_esta_aislada_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(empresaA, "Zenón Único");
        await CrearAnimalAsync(empresaA, clienteA, "Zeus Único");

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var resultados = await empresaB.GetFromJsonAsync<List<ResultadoBusquedaResp>>("/buscar?q=Único");

        resultados!.Should().BeEmpty("cada empresa solo busca en sus propios datos");
    }
}
