using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Clínica (animales).</summary>
public sealed class ClinicaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public ClinicaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id, string Nombre);

    private sealed record AnimalResp(
        Guid Id,
        Guid ClienteId,
        string Nombre,
        string Especie,
        string? Raza,
        string Sexo,
        DateOnly? FechaNacimiento,
        string? Microchip,
        bool Esterilizado,
        decimal? PesoKg,
        bool Activo,
        int? EdadMeses,
        bool EsCachorro);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente, string nombre)
    {
        var creado = await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = nombre })).Content.ReadFromJsonAsync<ClienteResp>();
        return creado!.Id;
    }

    [Fact]
    public async Task Crear_obtener_y_actualizar_animal()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario SL");

        var crear = await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId,
            Nombre = "Toby",
            Especie = "Perro",
            Sexo = "Macho",
            Raza = "Beagle",
            Microchip = "941 000 123",
        });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await crear.Content.ReadFromJsonAsync<AnimalResp>();
        creado!.Nombre.Should().Be("Toby");
        creado.Especie.Should().Be("Perro");
        creado.Microchip.Should().Be("941000123");

        var obtenido = await cliente.GetFromJsonAsync<AnimalResp>($"/animales/{creado.Id}");
        obtenido!.ClienteId.Should().Be(clienteId);

        var actualizar = await cliente.PutAsJsonAsync($"/animales/{creado.Id}", new
        {
            Nombre = "Toby II",
            Especie = "Gato",
            Sexo = "Hembra",
            Esterilizado = true,
        });
        actualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualizado = await actualizar.Content.ReadFromJsonAsync<AnimalResp>();
        actualizado!.Nombre.Should().Be("Toby II");
        actualizado.Especie.Should().Be("Gato");
        actualizado.Esterilizado.Should().BeTrue();
    }

    [Fact]
    public async Task Crear_animal_con_cliente_inexistente_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var crear = await cliente.PostAsJsonAsync("/animales", new { ClienteId = Guid.NewGuid(), Nombre = "Fantasma", Especie = "Perro", Sexo = "Macho" });
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Listar_animales_de_un_cliente()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(cliente, "Dueño A");
        var clienteB = await CrearClienteAsync(cliente, "Dueño B");

        await cliente.PostAsJsonAsync("/animales", new { ClienteId = clienteA, Nombre = "Rex", Especie = "Perro", Sexo = "Macho" });
        await cliente.PostAsJsonAsync("/animales", new { ClienteId = clienteA, Nombre = "Micha", Especie = "Gato", Sexo = "Hembra" });
        await cliente.PostAsJsonAsync("/animales", new { ClienteId = clienteB, Nombre = "Piolín", Especie = "Ave", Sexo = "Desconocido" });

        var deA = await cliente.GetFromJsonAsync<List<AnimalResp>>($"/clientes/{clienteA}/animales");
        deA!.Should().HaveCount(2);
        deA.Should().OnlyContain(a => a.ClienteId == clienteA);

        var deB = await cliente.GetFromJsonAsync<List<AnimalResp>>($"/clientes/{clienteB}/animales");
        deB!.Should().ContainSingle(a => a.Nombre == "Piolín");
    }

    [Fact]
    public async Task Dar_de_baja_un_animal_lo_saca_del_listado()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario");
        var creado = await (await cliente.PostAsJsonAsync("/animales", new { ClienteId = clienteId, Nombre = "Fido", Especie = "Perro", Sexo = "Macho" })).Content.ReadFromJsonAsync<AnimalResp>();

        var baja = await cliente.DeleteAsync(new Uri($"/animales/{creado!.Id}", UriKind.Relative));
        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var lista = await cliente.GetFromJsonAsync<List<AnimalResp>>("/animales");
        lista!.Should().NotContain(a => a.Id == creado.Id);
    }

    [Fact]
    public async Task Cachorro_se_calcula_a_partir_de_la_fecha_de_nacimiento()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var cachorro = await (await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId,
            Nombre = "Cachorro",
            Especie = "Perro",
            Sexo = "Macho",
            FechaNacimiento = hoy.AddMonths(-3),
        })).Content.ReadFromJsonAsync<AnimalResp>();
        cachorro!.EsCachorro.Should().BeTrue();
        cachorro.EdadMeses.Should().BeGreaterThanOrEqualTo(2);

        var adulto = await (await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId,
            Nombre = "Adulto",
            Especie = "Perro",
            Sexo = "Macho",
            FechaNacimiento = hoy.AddYears(-3),
        })).Content.ReadFromJsonAsync<AnimalResp>();
        adulto!.EsCachorro.Should().BeFalse();
    }

    [Fact]
    public async Task Los_animales_estan_aislados_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(empresaA, "Cliente de A");
        await empresaA.PostAsJsonAsync("/animales", new { ClienteId = clienteA, Nombre = "Animal de A", Especie = "Perro", Sexo = "Macho" });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var listaB = await empresaB.GetFromJsonAsync<List<AnimalResp>>("/animales");

        listaB!.Should().BeEmpty("cada empresa solo ve sus propios animales");
    }
}
