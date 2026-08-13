using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de las vacunas del módulo Clínica (pautas y vacunaciones).</summary>
public sealed class VacunaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public VacunaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id, string Nombre);

    private sealed record AnimalResp(Guid Id, string Nombre);

    private sealed record PautaResp(
        Guid Id,
        string Especie,
        string Nombre,
        string Caracter,
        int? EdadInicioSemanas,
        int? PeriodicidadRefuerzoMeses,
        bool Activo);

    private sealed record VacunacionResp(
        Guid Id,
        Guid AnimalId,
        Guid? PautaVacunalId,
        string Nombre,
        DateOnly FechaAplicacion,
        string? Lote,
        DateOnly? ProximaDosis,
        string? Veterinario,
        string? Notas,
        bool Activo);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente, string nombre)
    {
        var creado = await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = nombre })).Content.ReadFromJsonAsync<ClienteResp>();
        return creado!.Id;
    }

    private static async Task<Guid> CrearAnimalAsync(HttpClient cliente, Guid clienteId, string nombre, string especie = "Perro")
    {
        var creado = await (await cliente.PostAsJsonAsync("/animales", new { ClienteId = clienteId, Nombre = nombre, Especie = especie, Sexo = "Macho" })).Content.ReadFromJsonAsync<AnimalResp>();
        return creado!.Id;
    }

    [Fact]
    public async Task Crear_pauta_y_listarla_por_especie()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var crear = await cliente.PostAsJsonAsync("/vacunas/pautas", new
        {
            Especie = "Perro",
            Nombre = "Polivalente (DHPPi/L)",
            Caracter = "Recomendada",
            EdadInicioSemanas = 6,
            PeriodicidadRefuerzoMeses = 12,
        });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creada = await crear.Content.ReadFromJsonAsync<PautaResp>();
        creada!.Nombre.Should().Be("Polivalente (DHPPi/L)");
        creada.PeriodicidadRefuerzoMeses.Should().Be(12);

        await cliente.PostAsJsonAsync("/vacunas/pautas", new { Especie = "Gato", Nombre = "Trivalente felina", Caracter = "Recomendada" });

        var deLperros = await cliente.GetFromJsonAsync<List<PautaResp>>("/vacunas/pautas?especie=Perro");
        deLperros!.Should().ContainSingle(p => p.Nombre == "Polivalente (DHPPi/L)");
        deLperros.Should().OnlyContain(p => p.Especie == "Perro");

        var todas = await cliente.GetFromJsonAsync<List<PautaResp>>("/vacunas/pautas");
        todas!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Crear_pauta_duplicada_devuelve_409()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await cliente.PostAsJsonAsync("/vacunas/pautas", new { Especie = "Perro", Nombre = "Rabia", Caracter = "Legal" });

        var repetida = await cliente.PostAsJsonAsync("/vacunas/pautas", new { Especie = "Perro", Nombre = "Rabia", Caracter = "Legal" });
        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Registrar_vacunacion_ligada_a_pauta_copia_nombre_y_autocalcula_proxima_dosis()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Toby");

        var pauta = await (await cliente.PostAsJsonAsync("/vacunas/pautas", new
        {
            Especie = "Perro",
            Nombre = "Rabia",
            Caracter = "Legal",
            PeriodicidadRefuerzoMeses = 12,
        })).Content.ReadFromJsonAsync<PautaResp>();

        var fecha = new DateOnly(2026, 1, 10);
        var crear = await cliente.PostAsJsonAsync($"/animales/{animalId}/vacunas", new
        {
            FechaAplicacion = fecha,
            PautaVacunalId = pauta!.Id,
            Lote = "L-2026-001",
        });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creada = await crear.Content.ReadFromJsonAsync<VacunacionResp>();
        creada!.AnimalId.Should().Be(animalId);
        creada.PautaVacunalId.Should().Be(pauta.Id);
        creada.Nombre.Should().Be("Rabia", "el nombre se copia de la pauta si no se indica");
        creada.ProximaDosis.Should().Be(fecha.AddMonths(12), "la próxima dosis se autocalcula desde la periodicidad de la pauta");
    }

    [Fact]
    public async Task Registrar_vacunacion_con_pauta_de_otra_especie_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Micha", especie: "Gato");

        var pautaPerro = await (await cliente.PostAsJsonAsync("/vacunas/pautas", new { Especie = "Perro", Nombre = "Polivalente canina", Caracter = "Recomendada" })).Content.ReadFromJsonAsync<PautaResp>();

        var crear = await cliente.PostAsJsonAsync($"/animales/{animalId}/vacunas", new
        {
            FechaAplicacion = new DateOnly(2026, 1, 10),
            PautaVacunalId = pautaPerro!.Id,
        });
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Listar_vacunas_de_un_animal_se_ordena_de_la_mas_reciente_a_la_mas_antigua()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Rex");

        await cliente.PostAsJsonAsync($"/animales/{animalId}/vacunas", new { FechaAplicacion = new DateOnly(2026, 1, 5), Nombre = "Primera" });
        await cliente.PostAsJsonAsync($"/animales/{animalId}/vacunas", new { FechaAplicacion = new DateOnly(2026, 3, 1), Nombre = "Tercera" });
        await cliente.PostAsJsonAsync($"/animales/{animalId}/vacunas", new { FechaAplicacion = new DateOnly(2026, 2, 1), Nombre = "Segunda" });

        var historial = await cliente.GetFromJsonAsync<List<VacunacionResp>>($"/animales/{animalId}/vacunas");
        historial!.Should().HaveCount(3);
        historial!.Select(v => v.Nombre).Should().ContainInOrder("Tercera", "Segunda", "Primera");
    }

    [Fact]
    public async Task Anular_una_vacunacion_la_saca_del_historial()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Fido");
        var creada = await (await cliente.PostAsJsonAsync($"/animales/{animalId}/vacunas", new { FechaAplicacion = new DateOnly(2026, 1, 10), Nombre = "A anular" })).Content.ReadFromJsonAsync<VacunacionResp>();

        var baja = await cliente.DeleteAsync(new Uri($"/vacunas/{creada!.Id}", UriKind.Relative));
        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var historial = await cliente.GetFromJsonAsync<List<VacunacionResp>>($"/animales/{animalId}/vacunas");
        historial!.Should().NotContain(v => v.Id == creada.Id);
    }

    [Fact]
    public async Task Proximas_vacunas_devuelve_las_de_la_ventana()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Rex");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Dentro de la ventana de 30 días.
        await cliente.PostAsJsonAsync($"/animales/{animalId}/vacunas", new { FechaAplicacion = hoy.AddDays(-1), Nombre = "Pronto", ProximaDosis = hoy.AddDays(10) });
        // Fuera de la ventana.
        await cliente.PostAsJsonAsync($"/animales/{animalId}/vacunas", new { FechaAplicacion = hoy.AddDays(-1), Nombre = "Lejos", ProximaDosis = hoy.AddDays(200) });

        var proximas = await cliente.GetFromJsonAsync<List<VacunacionResp>>("/vacunas/proximas?dias=30");
        proximas!.Should().ContainSingle(v => v.Nombre == "Pronto");
        proximas.Should().NotContain(v => v.Nombre == "Lejos");
    }

    [Fact]
    public async Task Las_vacunas_estan_aisladas_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(empresaA, "Cliente de A");
        var animalA = await CrearAnimalAsync(empresaA, clienteA, "Animal de A");
        await empresaA.PostAsJsonAsync("/vacunas/pautas", new { Especie = "Perro", Nombre = "Privada de A", Caracter = "Recomendada" });
        await empresaA.PostAsJsonAsync($"/animales/{animalA}/vacunas", new { FechaAplicacion = new DateOnly(2026, 1, 10), Nombre = "Vacuna de A" });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        // La empresa B no ve las pautas de A.
        var pautasB = await empresaB.GetFromJsonAsync<List<PautaResp>>("/vacunas/pautas");
        pautasB!.Should().BeEmpty("cada empresa solo ve sus propias pautas");

        // Ni puede registrar vacunas sobre un animal que no es suyo.
        var crearEnB = await empresaB.PostAsJsonAsync($"/animales/{animalA}/vacunas", new { FechaAplicacion = new DateOnly(2026, 1, 10), Nombre = "Intrusa" });
        crearEnB.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Ni ver su historial.
        var historialEnB = await empresaB.GetFromJsonAsync<List<VacunacionResp>>($"/animales/{animalA}/vacunas");
        historialEnB!.Should().BeEmpty("cada empresa solo ve las vacunas de sus propios animales");
    }
}
