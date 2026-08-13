using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del historial clínico (consultas) del módulo Clínica.</summary>
public sealed class ConsultaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public ConsultaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id, string Nombre);

    private sealed record AnimalResp(Guid Id, string Nombre);

    private sealed record ConsultaResp(
        Guid Id,
        Guid AnimalId,
        DateOnly Fecha,
        string? Motivo,
        string? Diagnostico,
        string? Tratamiento,
        decimal? PesoKg,
        string? Veterinario,
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
    public async Task Registrar_obtener_y_actualizar_consulta()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario SL");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Toby");

        var crear = await cliente.PostAsJsonAsync($"/animales/{animalId}/consultas", new
        {
            Fecha = new DateOnly(2026, 1, 10),
            Motivo = "Revisión anual",
            Diagnostico = "Sano",
            PesoKg = 12.5m,
            Veterinario = "Dra. López",
        });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creada = await crear.Content.ReadFromJsonAsync<ConsultaResp>();
        creada!.AnimalId.Should().Be(animalId);
        creada.Motivo.Should().Be("Revisión anual");
        creada.PesoKg.Should().Be(12.5m);

        var obtenida = await cliente.GetFromJsonAsync<ConsultaResp>($"/consultas/{creada.Id}");
        obtenida!.Diagnostico.Should().Be("Sano");

        var actualizar = await cliente.PutAsJsonAsync($"/consultas/{creada.Id}", new
        {
            Fecha = new DateOnly(2026, 1, 12),
            Motivo = "Seguimiento",
            Tratamiento = "Antibiótico 7 días",
        });
        actualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualizada = await actualizar.Content.ReadFromJsonAsync<ConsultaResp>();
        actualizada!.Motivo.Should().Be("Seguimiento");
        actualizada.Tratamiento.Should().Be("Antibiótico 7 días");
        actualizada.Fecha.Should().Be(new DateOnly(2026, 1, 12));
    }

    [Fact]
    public async Task Historial_se_ordena_de_la_mas_reciente_a_la_mas_antigua()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Rex");

        await cliente.PostAsJsonAsync($"/animales/{animalId}/consultas", new { Fecha = new DateOnly(2026, 1, 5), Motivo = "Primera" });
        await cliente.PostAsJsonAsync($"/animales/{animalId}/consultas", new { Fecha = new DateOnly(2026, 3, 1), Motivo = "Tercera" });
        await cliente.PostAsJsonAsync($"/animales/{animalId}/consultas", new { Fecha = new DateOnly(2026, 2, 1), Motivo = "Segunda" });

        var historial = await cliente.GetFromJsonAsync<List<ConsultaResp>>($"/animales/{animalId}/consultas");
        historial!.Should().HaveCount(3);
        historial!.Select(c => c.Motivo).Should().ContainInOrder("Tercera", "Segunda", "Primera");
    }

    [Fact]
    public async Task Registrar_consulta_con_animal_inexistente_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var crear = await cliente.PostAsJsonAsync($"/animales/{Guid.NewGuid()}/consultas", new { Fecha = new DateOnly(2026, 1, 10), Motivo = "Fantasma" });
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anular_una_consulta_la_saca_del_historial()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Fido");
        var creada = await (await cliente.PostAsJsonAsync($"/animales/{animalId}/consultas", new { Fecha = new DateOnly(2026, 1, 10), Motivo = "A anular" })).Content.ReadFromJsonAsync<ConsultaResp>();

        var baja = await cliente.DeleteAsync(new Uri($"/consultas/{creada!.Id}", UriKind.Relative));
        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var historial = await cliente.GetFromJsonAsync<List<ConsultaResp>>($"/animales/{animalId}/consultas");
        historial!.Should().NotContain(c => c.Id == creada.Id);
    }

    [Fact]
    public async Task Las_consultas_estan_aisladas_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(empresaA, "Cliente de A");
        var animalA = await CrearAnimalAsync(empresaA, clienteA, "Animal de A");
        await empresaA.PostAsJsonAsync($"/animales/{animalA}/consultas", new { Fecha = new DateOnly(2026, 1, 10), Motivo = "Privada de A" });

        // Otra empresa no puede registrar consultas sobre un animal que no es suyo.
        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var crearEnB = await empresaB.PostAsJsonAsync($"/animales/{animalA}/consultas", new { Fecha = new DateOnly(2026, 1, 10), Motivo = "Intrusa" });
        crearEnB.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Ni ver su historial.
        var historialEnB = await empresaB.GetFromJsonAsync<List<ConsultaResp>>($"/animales/{animalA}/consultas");
        historialEnB!.Should().BeEmpty("cada empresa solo ve las consultas de sus propios animales");
    }
}
