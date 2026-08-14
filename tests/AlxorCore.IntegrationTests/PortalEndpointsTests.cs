using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>
/// Pruebas de integración de la Cartilla Viva (portal del dueño por token). Cubren: generación del
/// acceso (autenticado), lectura pública de la cartilla del cliente correcto, confirmación de cita por
/// token, token inválido/revocado ⇒ 404, aislamiento multiempresa y que un citaId ajeno no se pueda
/// confirmar con el token.
/// </summary>
public sealed class PortalEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public PortalEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id, string Nombre);

    private sealed record AnimalResp(Guid Id, string Nombre);

    private sealed record CitaResp(Guid Id, Guid AnimalId, DateTimeOffset Inicio, string Estado);

    private sealed record AccesoResp(Guid ClienteId, string Token, bool Activo, string Enlace);

    private sealed record CartillaVacunaResp(string Nombre, string Estado, DateOnly? ProximaDosis);

    private sealed record CartillaCitaResp(
        Guid CitaId, Guid AnimalId, string Animal, DateTimeOffset Inicio, string Estado, string EstadoTexto, string Tipo, string? Motivo);

    private sealed record CartillaHitoResp(int Orden, string Titulo, string Detalle, string Estado);

    private sealed record CartillaAnimalResp(
        Guid Id, string Nombre, string Especie, string EspecieTexto, int? EdadMeses, string? EdadTexto, bool EsCachorro,
        List<CartillaVacunaResp> Vacunas, CartillaCitaResp? ProximaCita, List<CartillaHitoResp> Hitos);

    private sealed record CartillaResp(
        string NombreClinica, string NombreCliente, List<CartillaAnimalResp> Animales, List<CartillaCitaResp> ProximasCitas);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente, string nombre, string? email = null)
    {
        var creado = await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = nombre, Email = email })).Content.ReadFromJsonAsync<ClienteResp>();
        return creado!.Id;
    }

    private static async Task<Guid> CrearAnimalAsync(HttpClient cliente, Guid clienteId, string nombre, string especie = "Perro", string? fechaNacimiento = null, bool esterilizado = false)
    {
        var creado = await (await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId,
            Nombre = nombre,
            Especie = especie,
            Sexo = "Macho",
            FechaNacimiento = fechaNacimiento,
            Esterilizado = esterilizado,
        })).Content.ReadFromJsonAsync<AnimalResp>();
        return creado!.Id;
    }

    private static async Task<CitaResp> CrearCitaAsync(HttpClient cliente, Guid animalId, DateTimeOffset inicio)
    {
        var resp = await cliente.PostAsJsonAsync("/citas", new { AnimalId = animalId, Inicio = inicio, Tipo = "Consulta", Motivo = "Revisión" });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<CitaResp>())!;
    }

    private static async Task<AccesoResp> GenerarPortalAsync(HttpClient cliente, Guid clienteId)
    {
        var resp = await cliente.PostAsync(new Uri($"/clientes/{clienteId}/portal", UriKind.Relative), content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<AccesoResp>())!;
    }

    [Fact]
    public async Task Generar_acceso_devuelve_token_y_enlace()
    {
        var (clinica, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(clinica, "Laura Giménez");

        var acceso = await GenerarPortalAsync(clinica, clienteId);

        acceso.ClienteId.Should().Be(clienteId);
        acceso.Activo.Should().BeTrue();
        acceso.Token.Should().NotBeNullOrWhiteSpace();
        acceso.Token.Length.Should().BeGreaterThanOrEqualTo(32);
        acceso.Enlace.Should().Be($"/cartilla.html?token={acceso.Token}");
    }

    [Fact]
    public async Task Regenerar_revoca_el_token_anterior()
    {
        var (clinica, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(clinica, "Marcos Ruiz");
        var animalId = await CrearAnimalAsync(clinica, clienteId, "Rex");
        await CrearCitaAsync(clinica, animalId, DateTimeOffset.UtcNow.AddDays(3));

        var primero = await GenerarPortalAsync(clinica, clienteId);
        var segundo = await GenerarPortalAsync(clinica, clienteId);

        segundo.Token.Should().NotBe(primero.Token);

        var publico = _fabrica.CreateClient();
        // El token viejo ya no resuelve nada.
        (await publico.GetAsync(new Uri($"/portal/{primero.Token}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        // El nuevo sí.
        (await publico.GetAsync(new Uri($"/portal/{segundo.Token}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cartilla_publica_muestra_los_datos_del_cliente_correcto()
    {
        var (clinica, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(clinica, "Laura Giménez Ortí", "laura@example.com");
        var animalId = await CrearAnimalAsync(clinica, clienteId, "Nala");
        var cita = await CrearCitaAsync(clinica, animalId, DateTimeOffset.UtcNow.AddDays(5));
        var acceso = await GenerarPortalAsync(clinica, clienteId);

        var publico = _fabrica.CreateClient();
        var cartilla = await publico.GetFromJsonAsync<CartillaResp>($"/portal/{acceso.Token}");

        cartilla!.NombreCliente.Should().Be("Laura");
        cartilla.NombreClinica.Should().Be("Empresa de Pruebas SL");
        cartilla.Animales.Should().ContainSingle(a => a.Nombre == "Nala");
        cartilla.ProximasCitas.Should().ContainSingle(c => c.CitaId == cita.Id);
        cartilla.Animales.Single().ProximaCita!.CitaId.Should().Be(cita.Id);
    }

    [Fact]
    public async Task Cachorro_incluye_los_hitos_del_plan_de_crecimiento()
    {
        var (clinica, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(clinica, "Ana Torres");
        var haceCuatroMeses = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-4).ToString("yyyy-MM-dd");
        await CrearAnimalAsync(clinica, clienteId, "Coco", "Perro", haceCuatroMeses);
        var acceso = await GenerarPortalAsync(clinica, clienteId);

        var publico = _fabrica.CreateClient();
        var cartilla = await publico.GetFromJsonAsync<CartillaResp>($"/portal/{acceso.Token}");

        var coco = cartilla!.Animales.Single(a => a.Nombre == "Coco");
        coco.EsCachorro.Should().BeTrue();
        coco.Hitos.Should().HaveCount(6);
        coco.Hitos.Should().Contain(h => h.Estado == "Actual");
    }

    [Fact]
    public async Task Confirmar_cita_por_token_cambia_el_estado()
    {
        var (clinica, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(clinica, "Javier Peña");
        var animalId = await CrearAnimalAsync(clinica, clienteId, "Rocky");
        var cita = await CrearCitaAsync(clinica, animalId, DateTimeOffset.UtcNow.AddDays(2));
        cita.Estado.Should().Be("Solicitada");
        var acceso = await GenerarPortalAsync(clinica, clienteId);

        var publico = _fabrica.CreateClient();
        var confirmar = await publico.PostAsync(new Uri($"/portal/{acceso.Token}/citas/{cita.Id}/confirmar", UriKind.Relative), content: null);
        confirmar.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmada = await confirmar.Content.ReadFromJsonAsync<CartillaCitaResp>();
        confirmada!.Estado.Should().Be("Confirmada");

        // La clínica ve la cita confirmada.
        var vista = await clinica.GetFromJsonAsync<CitaResp>($"/citas/{cita.Id}");
        vista!.Estado.Should().Be("Confirmada");
    }

    [Fact]
    public async Task Token_invalido_devuelve_404()
    {
        var publico = _fabrica.CreateClient();
        var resp = await publico.GetAsync(new Uri("/portal/token-que-no-existe-pero-suficientemente-largo", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Token_revocado_devuelve_404()
    {
        var (clinica, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(clinica, "Cliente Revocado");
        await CrearAnimalAsync(clinica, clienteId, "Kira");
        var acceso = await GenerarPortalAsync(clinica, clienteId);

        var revocar = await clinica.DeleteAsync(new Uri($"/clientes/{clienteId}/portal", UriKind.Relative));
        revocar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var publico = _fabrica.CreateClient();
        (await publico.GetAsync(new Uri($"/portal/{acceso.Token}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task El_portal_esta_aislado_por_empresa()
    {
        // Empresa A con un cliente, animal y token.
        var (clinicaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(clinicaA, "Dueño de A");
        var animalA = await CrearAnimalAsync(clinicaA, clienteA, "Animal de A");
        var citaA = await CrearCitaAsync(clinicaA, animalA, DateTimeOffset.UtcNow.AddDays(3));
        var accesoA = await GenerarPortalAsync(clinicaA, clienteA);

        // Empresa B con su propio cliente/animal/cita.
        var (clinicaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteB = await CrearClienteAsync(clinicaB, "Dueño de B");
        var animalB = await CrearAnimalAsync(clinicaB, clienteB, "Animal de B");
        var citaB = await CrearCitaAsync(clinicaB, animalB, DateTimeOffset.UtcNow.AddDays(3));

        var publico = _fabrica.CreateClient();

        // El token de A solo ve datos de A.
        var cartillaA = await publico.GetFromJsonAsync<CartillaResp>($"/portal/{accesoA.Token}");
        cartillaA!.NombreCliente.Should().Be("Dueño");
        cartillaA.Animales.Should().ContainSingle(a => a.Nombre == "Animal de A");
        cartillaA.Animales.Should().NotContain(a => a.Nombre == "Animal de B");

        // Con el token de A NO se puede confirmar una cita de la empresa B: 404 (no se filtra info).
        var intentoCruzado = await publico.PostAsync(new Uri($"/portal/{accesoA.Token}/citas/{citaB.Id}/confirmar", UriKind.Relative), content: null);
        intentoCruzado.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // La cita de B sigue solicitada.
        var vistaB = await clinicaB.GetFromJsonAsync<CitaResp>($"/citas/{citaB.Id}");
        vistaB!.Estado.Should().Be("Solicitada");

        // Y el token de A sí confirma la suya.
        var propia = await publico.PostAsync(new Uri($"/portal/{accesoA.Token}/citas/{citaA.Id}/confirmar", UriKind.Relative), content: null);
        propia.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task No_se_puede_confirmar_con_el_token_una_cita_de_otro_cliente_de_la_misma_empresa()
    {
        var (clinica, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var cliente1 = await CrearClienteAsync(clinica, "Cliente Uno");
        var animal1 = await CrearAnimalAsync(clinica, cliente1, "Perro Uno");
        var cita1 = await CrearCitaAsync(clinica, animal1, DateTimeOffset.UtcNow.AddDays(3));

        var cliente2 = await CrearClienteAsync(clinica, "Cliente Dos");
        await CrearAnimalAsync(clinica, cliente2, "Perro Dos");
        var acceso2 = await GenerarPortalAsync(clinica, cliente2);

        var publico = _fabrica.CreateClient();

        // El token del cliente 2 no puede confirmar la cita del cliente 1 (mismo empresa, otro dueño).
        var intento = await publico.PostAsync(new Uri($"/portal/{acceso2.Token}/citas/{cita1.Id}/confirmar", UriKind.Relative), content: null);
        intento.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Y su cartilla no lista al animal del cliente 1.
        var cartilla2 = await publico.GetFromJsonAsync<CartillaResp>($"/portal/{acceso2.Token}");
        cartilla2!.Animales.Should().NotContain(a => a.Nombre == "Perro Uno");
    }
}
