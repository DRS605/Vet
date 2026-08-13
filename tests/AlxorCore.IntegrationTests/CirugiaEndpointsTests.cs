using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de las cirugías del módulo Clínica.</summary>
public sealed class CirugiaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public CirugiaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id, string Nombre);

    private sealed record AnimalResp(Guid Id, string Nombre);

    private sealed record CirugiaResp(
        Guid Id,
        Guid AnimalId,
        DateOnly Fecha,
        string Nombre,
        string? Descripcion,
        string? Cirujano,
        string? Anestesia,
        string? Complicaciones,
        DateOnly? ProximaRevision,
        bool Activo);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente, string nombre)
    {
        var creado = await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = nombre })).Content.ReadFromJsonAsync<ClienteResp>();
        return creado!.Id;
    }

    private static async Task<Guid> CrearAnimalAsync(HttpClient cliente, Guid clienteId, string nombre)
    {
        var creado = await (await cliente.PostAsJsonAsync("/animales", new { ClienteId = clienteId, Nombre = nombre, Especie = "Perro", Sexo = "Macho" })).Content.ReadFromJsonAsync<AnimalResp>();
        return creado!.Id;
    }

    [Fact]
    public async Task Registrar_obtener_y_actualizar_cirugia()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario SL");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Toby");

        var crear = await cliente.PostAsJsonAsync($"/animales/{animalId}/cirugias", new
        {
            Fecha = new DateOnly(2026, 1, 10),
            Nombre = "Esterilización (OVH)",
            Descripcion = "Sin incidencias",
            Cirujano = "Dra. López",
            Anestesia = "Isoflurano",
            ProximaRevision = new DateOnly(2026, 1, 20),
        });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creada = await crear.Content.ReadFromJsonAsync<CirugiaResp>();
        creada!.AnimalId.Should().Be(animalId);
        creada.Nombre.Should().Be("Esterilización (OVH)");
        creada.Cirujano.Should().Be("Dra. López");
        creada.ProximaRevision.Should().Be(new DateOnly(2026, 1, 20));

        var obtenida = await cliente.GetFromJsonAsync<CirugiaResp>($"/cirugias/{creada.Id}");
        obtenida!.Anestesia.Should().Be("Isoflurano");

        var actualizar = await cliente.PutAsJsonAsync($"/cirugias/{creada.Id}", new
        {
            Fecha = new DateOnly(2026, 1, 12),
            Nombre = "Esterilización (OVH) revisada",
            Complicaciones = "Ninguna",
        });
        actualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualizada = await actualizar.Content.ReadFromJsonAsync<CirugiaResp>();
        actualizada!.Nombre.Should().Be("Esterilización (OVH) revisada");
        actualizada.Complicaciones.Should().Be("Ninguna");
        actualizada.Fecha.Should().Be(new DateOnly(2026, 1, 12));
    }

    [Fact]
    public async Task Historial_de_cirugias_se_ordena_de_la_mas_reciente_a_la_mas_antigua()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Rex");

        await cliente.PostAsJsonAsync($"/animales/{animalId}/cirugias", new { Fecha = new DateOnly(2026, 1, 5), Nombre = "Primera" });
        await cliente.PostAsJsonAsync($"/animales/{animalId}/cirugias", new { Fecha = new DateOnly(2026, 3, 1), Nombre = "Tercera" });
        await cliente.PostAsJsonAsync($"/animales/{animalId}/cirugias", new { Fecha = new DateOnly(2026, 2, 1), Nombre = "Segunda" });

        var historial = await cliente.GetFromJsonAsync<List<CirugiaResp>>($"/animales/{animalId}/cirugias");
        historial!.Should().HaveCount(3);
        historial!.Select(c => c.Nombre).Should().ContainInOrder("Tercera", "Segunda", "Primera");
    }

    [Fact]
    public async Task Registrar_cirugia_con_animal_inexistente_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var crear = await cliente.PostAsJsonAsync($"/animales/{Guid.NewGuid()}/cirugias", new { Fecha = new DateOnly(2026, 1, 10), Nombre = "Fantasma" });
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Proximas_revisiones_devuelve_las_de_la_ventana()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Rex");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Dentro de la ventana de 30 días.
        await cliente.PostAsJsonAsync($"/animales/{animalId}/cirugias", new { Fecha = hoy.AddDays(-1), Nombre = "Pronto", ProximaRevision = hoy.AddDays(10) });
        // Fuera de la ventana.
        await cliente.PostAsJsonAsync($"/animales/{animalId}/cirugias", new { Fecha = hoy.AddDays(-1), Nombre = "Lejos", ProximaRevision = hoy.AddDays(200) });

        var proximas = await cliente.GetFromJsonAsync<List<CirugiaResp>>("/cirugias/proximas-revisiones?dias=30");
        proximas!.Should().ContainSingle(c => c.Nombre == "Pronto");
        proximas.Should().NotContain(c => c.Nombre == "Lejos");
    }

    [Fact]
    public async Task Anular_una_cirugia_la_saca_del_historial()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Fido");
        var creada = await (await cliente.PostAsJsonAsync($"/animales/{animalId}/cirugias", new { Fecha = new DateOnly(2026, 1, 10), Nombre = "A anular" })).Content.ReadFromJsonAsync<CirugiaResp>();

        var baja = await cliente.DeleteAsync(new Uri($"/cirugias/{creada!.Id}", UriKind.Relative));
        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var historial = await cliente.GetFromJsonAsync<List<CirugiaResp>>($"/animales/{animalId}/cirugias");
        historial!.Should().NotContain(c => c.Id == creada.Id);
    }

    [Fact]
    public async Task Las_cirugias_estan_aisladas_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(empresaA, "Cliente de A");
        var animalA = await CrearAnimalAsync(empresaA, clienteA, "Animal de A");
        await empresaA.PostAsJsonAsync($"/animales/{animalA}/cirugias", new { Fecha = new DateOnly(2026, 1, 10), Nombre = "Privada de A" });

        // Otra empresa no puede registrar cirugías sobre un animal que no es suyo.
        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var crearEnB = await empresaB.PostAsJsonAsync($"/animales/{animalA}/cirugias", new { Fecha = new DateOnly(2026, 1, 10), Nombre = "Intrusa" });
        crearEnB.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Ni ver su historial.
        var historialEnB = await empresaB.GetFromJsonAsync<List<CirugiaResp>>($"/animales/{animalA}/cirugias");
        historialEnB!.Should().BeEmpty("cada empresa solo ve las cirugías de sus propios animales");
    }
}
