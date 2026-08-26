using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de la agenda (citas) del módulo Clínica: estados y KPI de confirmación.</summary>
public sealed class CitaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public CitaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id, string Nombre);

    private sealed record AnimalResp(Guid Id, string Nombre);

    private sealed record CitaResp(
        Guid Id,
        Guid AnimalId,
        DateTimeOffset Inicio,
        int DuracionMinutos,
        string Tipo,
        string? Motivo,
        string? Veterinario,
        string Estado,
        string? Notas);

    private sealed record ResumenResp(
        int Total,
        int Solicitadas,
        int Confirmadas,
        int Atendidas,
        int Canceladas,
        int NoPresentado,
        int PorcentajeConfirmacion);

    private sealed record PuntoResp(int Anio, int Mes, int Citadas, int Confirmadas);

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

    private static async Task<CitaResp> CrearCitaAsync(HttpClient cliente, Guid animalId, DateTimeOffset inicio, string? veterinario = null, string tipo = "Consulta")
    {
        var resp = await cliente.PostAsJsonAsync("/citas", new
        {
            AnimalId = animalId,
            Inicio = inicio,
            Tipo = tipo,
            Veterinario = veterinario,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<CitaResp>())!;
    }

    [Fact]
    public async Task Crear_cita_queda_solicitada()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario SL");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Toby");

        var creada = await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 2, 10, 9, 30, 0, TimeSpan.Zero), "Dra. López");

        creada.AnimalId.Should().Be(animalId);
        creada.Estado.Should().Be("Solicitada");
        creada.DuracionMinutos.Should().Be(30);
        creada.Veterinario.Should().Be("Dra. López");
    }

    [Fact]
    public async Task Crear_cita_con_animal_inexistente_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var crear = await cliente.PostAsJsonAsync("/citas", new
        {
            AnimalId = Guid.NewGuid(),
            Inicio = new DateTimeOffset(2026, 2, 10, 9, 30, 0, TimeSpan.Zero),
        });
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Agenda_devuelve_el_rango_ordenado_por_inicio_y_filtra_por_estado()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Rex");

        // Se crean fuera de orden para comprobar el orden ascendente por inicio.
        var tarde = await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero));
        var pronto = await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 2, 5, 8, 0, 0, TimeSpan.Zero));
        var media = await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 2, 12, 10, 0, 0, TimeSpan.Zero));
        // Fuera de la ventana.
        await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero));

        var agenda = await cliente.GetFromJsonAsync<List<CitaResp>>(
            "/agenda?desde=2026-02-01T00:00:00Z&hasta=2026-02-28T23:59:59Z");
        agenda!.Select(c => c.Id).Should().ContainInOrder(pronto.Id, media.Id, tarde.Id);
        agenda.Should().HaveCount(3);

        // Filtro por estado: se confirma una y se pide solo las confirmadas.
        await cliente.PostAsync(new Uri($"/citas/{media.Id}/confirmar", UriKind.Relative), content: null);
        var confirmadas = await cliente.GetFromJsonAsync<List<CitaResp>>(
            "/agenda?desde=2026-02-01T00:00:00Z&hasta=2026-02-28T23:59:59Z&estado=Confirmada");
        confirmadas!.Should().ContainSingle(c => c.Id == media.Id);
    }

    [Fact]
    public async Task Confirmar_una_cita_cambia_su_estado()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Luna");
        var cita = await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 2, 15, 11, 0, 0, TimeSpan.Zero));

        var confirmar = await cliente.PostAsync(new Uri($"/citas/{cita.Id}/confirmar", UriKind.Relative), content: null);
        confirmar.StatusCode.Should().Be(HttpStatusCode.OK);

        var obtenida = await cliente.GetFromJsonAsync<CitaResp>($"/citas/{cita.Id}");
        obtenida!.Estado.Should().Be("Confirmada");
    }

    [Fact]
    public async Task Kpi_calcula_el_porcentaje_de_confirmacion()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Nala");

        // 4 citas: 1 confirmada + 1 atendida (cuentan como confirmadas) y 2 solicitadas → 50%.
        var c1 = await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
        var c2 = await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero));
        await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero));
        await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 3, 5, 9, 0, 0, TimeSpan.Zero));

        await cliente.PostAsync(new Uri($"/citas/{c1.Id}/confirmar", UriKind.Relative), content: null);
        await cliente.PostAsync(new Uri($"/citas/{c2.Id}/confirmar", UriKind.Relative), content: null);
        await cliente.PostAsync(new Uri($"/citas/{c2.Id}/atender", UriKind.Relative), content: null);

        var kpi = await cliente.GetFromJsonAsync<ResumenResp>(
            "/citas/kpi?desde=2026-03-01T00:00:00Z&hasta=2026-03-31T23:59:59Z");

        kpi!.Total.Should().Be(4);
        kpi.Confirmadas.Should().Be(1);
        kpi.Atendidas.Should().Be(1);
        kpi.Solicitadas.Should().Be(2);
        kpi.PorcentajeConfirmacion.Should().Be(50);
    }

    [Fact]
    public async Task Kpi_confirmacion_mensual_devuelve_la_serie_de_los_ultimos_meses()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Kira");

        var ahora = DateTimeOffset.UtcNow;
        var esteMes = new DateTimeOffset(ahora.Year, ahora.Month, 15, 10, 0, 0, TimeSpan.Zero);
        var c = await CrearCitaAsync(cliente, animalId, esteMes);
        await cliente.PostAsync(new Uri($"/citas/{c.Id}/confirmar", UriKind.Relative), content: null);

        var serie = await cliente.GetFromJsonAsync<List<PuntoResp>>("/citas/kpi/confirmacion-mensual?meses=6");
        serie!.Should().HaveCount(6);
        var actual = serie![^1];
        actual.Anio.Should().Be(ahora.Year);
        actual.Mes.Should().Be(ahora.Month);
        actual.Citadas.Should().Be(1);
        actual.Confirmadas.Should().Be(1);
    }

    [Fact]
    public async Task Agenda_y_kpi_admiten_rango_con_offset_local_no_utc()
    {
        // Regresión: en un PC español (UTC+2) la SPA envía el rango con offset +02:00. Npgsql exige
        // offset 0 al mapear DateTimeOffset a «timestamp with time zone», así que sin normalizar a UTC
        // lanzaba ArgumentException y el panel daba Error 500. Este test usa offset +02:00 EXPLÍCITO
        // (no la zona del proceso), por lo que detecta el fallo aunque el CI corra en UTC.
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario Madrid");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Chispa");

        // Cita a las 09:00 UTC del 17/08/2026, dentro de la ventana local de ese día en España.
        var cita = await CrearCitaAsync(cliente, animalId, new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero));
        await cliente.PostAsync(new Uri($"/citas/{cita.Id}/confirmar", UriKind.Relative), content: null);

        var offset = TimeSpan.FromHours(2);
        var desde = new DateTimeOffset(2026, 8, 17, 0, 0, 0, offset);   // 16/08 22:00 UTC
        var hasta = new DateTimeOffset(2026, 8, 17, 23, 59, 59, offset); // 17/08 21:59:59 UTC

        static string Q(DateTimeOffset v) => Uri.EscapeDataString(v.ToString("yyyy-MM-ddTHH:mm:sszzz"));

        // Agenda: no debe lanzar (500) y debe devolver la cita del rango.
        var respAgenda = await cliente.GetAsync(new Uri($"/agenda?desde={Q(desde)}&hasta={Q(hasta)}", UriKind.Relative));
        respAgenda.StatusCode.Should().Be(HttpStatusCode.OK, "el rango con offset local debe normalizarse a UTC, no dar 500");
        var agenda = await respAgenda.Content.ReadFromJsonAsync<List<CitaResp>>();
        agenda!.Should().ContainSingle(c => c.Id == cita.Id);

        // KPI/resumen: mismo rango con offset +02:00.
        var respKpi = await cliente.GetAsync(new Uri($"/citas/kpi?desde={Q(desde)}&hasta={Q(hasta)}", UriKind.Relative));
        respKpi.StatusCode.Should().Be(HttpStatusCode.OK, "el KPI con offset local debe normalizarse a UTC, no dar 500");
        var kpi = await respKpi.Content.ReadFromJsonAsync<ResumenResp>();
        kpi!.Total.Should().Be(1);
        kpi.Confirmadas.Should().Be(1);
        kpi.PorcentajeConfirmacion.Should().Be(100);
    }

    [Fact]
    public async Task La_agenda_esta_aislada_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(empresaA, "Cliente de A");
        var animalA = await CrearAnimalAsync(empresaA, clienteA, "Animal de A");
        await CrearCitaAsync(empresaA, animalA, new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero));

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        // B no ve la agenda de A.
        var agendaB = await empresaB.GetFromJsonAsync<List<CitaResp>>(
            "/agenda?desde=2026-02-01T00:00:00Z&hasta=2026-02-28T23:59:59Z");
        agendaB!.Should().BeEmpty("cada empresa solo ve sus propias citas");

        // Ni puede crear una cita sobre un animal que no es suyo.
        var crearEnB = await empresaB.PostAsJsonAsync("/citas", new
        {
            AnimalId = animalA,
            Inicio = new DateTimeOffset(2026, 2, 11, 9, 0, 0, TimeSpan.Zero),
        });
        crearEnB.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
