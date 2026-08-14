using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de los recordatorios del módulo Clínica (generación y envío por correo).</summary>
public sealed class RecordatorioEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public RecordatorioEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);

    private sealed record AnimalResp(Guid Id);

    private sealed record RecordatorioResp(
        Guid Id,
        Guid AnimalId,
        string Tipo,
        string Titulo,
        DateOnly FechaObjetivo,
        string? Notas,
        string? ReferenciaTipo,
        Guid? ReferenciaId,
        string Estado,
        DateTimeOffset? FechaEnvio);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente, string nombre, string? email = null)
    {
        var creado = await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = nombre, Email = email })).Content.ReadFromJsonAsync<ClienteResp>();
        return creado!.Id;
    }

    private static async Task<Guid> CrearAnimalAsync(HttpClient cliente, Guid clienteId, string nombre)
    {
        var creado = await (await cliente.PostAsJsonAsync("/animales", new { ClienteId = clienteId, Nombre = nombre, Especie = "Perro", Sexo = "Macho" })).Content.ReadFromJsonAsync<AnimalResp>();
        return creado!.Id;
    }

    [Fact]
    public async Task Crear_recordatorio_manual()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario SL");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Toby");

        var crear = await cliente.PostAsJsonAsync("/recordatorios", new
        {
            AnimalId = animalId,
            Tipo = "Otro",
            Titulo = "Llamar para revisión anual de Toby",
            FechaObjetivo = new DateOnly(2026, 6, 1),
            Notas = "Preferible por la mañana",
        });

        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await crear.Content.ReadFromJsonAsync<RecordatorioResp>();
        creado!.AnimalId.Should().Be(animalId);
        creado.Titulo.Should().Be("Llamar para revisión anual de Toby");
        creado.Estado.Should().Be("Pendiente");
        creado.FechaEnvio.Should().BeNull();
    }

    [Fact]
    public async Task Crear_recordatorio_con_animal_inexistente_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var crear = await cliente.PostAsJsonAsync("/recordatorios", new
        {
            AnimalId = Guid.NewGuid(),
            Tipo = "Otro",
            Titulo = "Fantasma",
            FechaObjetivo = new DateOnly(2026, 6, 1),
        });
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Generar_crea_recordatorios_desde_vencimientos_y_no_duplica()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Dueño de Nala", "nala@ejemplo.com");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Nala");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        // Vacunación con próxima dosis dentro de la ventana de 30 días.
        await cliente.PostAsJsonAsync($"/animales/{animalId}/vacunas", new
        {
            FechaAplicacion = hoy.AddDays(-1),
            Nombre = "Polivalente",
            ProximaDosis = hoy.AddDays(10),
        });

        var primera = await cliente.PostAsync(new Uri("/recordatorios/generar?dias=30", UriKind.Relative), content: null);
        primera.StatusCode.Should().Be(HttpStatusCode.OK);
        (await primera.Content.ReadFromJsonAsync<int>()).Should().Be(1);

        // Segunda llamada: el vencimiento ya tiene recordatorio, así que no se duplica.
        var segunda = await cliente.PostAsync(new Uri("/recordatorios/generar?dias=30", UriKind.Relative), content: null);
        (await segunda.Content.ReadFromJsonAsync<int>()).Should().Be(0);

        var listado = await cliente.GetFromJsonAsync<List<RecordatorioResp>>("/recordatorios");
        listado!.Should().ContainSingle(r => r.AnimalId == animalId && r.Tipo == "Vacuna");
        listado!.Single(r => r.AnimalId == animalId).Titulo.Should().Contain("Nala");
    }

    [Fact]
    public async Task Enviar_recordatorio_marca_enviado_y_el_correo_llega_al_cliente()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var email = $"prop-{Guid.NewGuid():N}@ejemplo.com";
        var clienteId = await CrearClienteAsync(cliente, "Propietario con correo", email);
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Luna");

        var creado = await (await cliente.PostAsJsonAsync("/recordatorios", new
        {
            AnimalId = animalId,
            Tipo = "Vacuna",
            Titulo = "Vacuna de la rabia de Luna",
            FechaObjetivo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5),
        })).Content.ReadFromJsonAsync<RecordatorioResp>();

        var enviar = await cliente.PostAsync(new Uri($"/recordatorios/{creado!.Id}/enviar", UriKind.Relative), content: null);
        enviar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var obtenido = await cliente.GetFromJsonAsync<RecordatorioResp>($"/recordatorios/{creado.Id}");
        obtenido!.Estado.Should().Be("Enviado");
        obtenido.FechaEnvio.Should().NotBeNull();

        var mensajes = _fabrica.Correo.ParaDestinatario(email);
        mensajes.Should().ContainSingle();
        mensajes[0].Asunto.Should().Be("Vacuna de la rabia de Luna");
        mensajes[0].Cuerpo.Should().Contain("Luna");
    }

    [Fact]
    public async Task Enviar_recordatorio_sin_email_del_cliente_devuelve_error_controlado()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario sin correo");
        var animalId = await CrearAnimalAsync(cliente, clienteId, "Rex");

        var creado = await (await cliente.PostAsJsonAsync("/recordatorios", new
        {
            AnimalId = animalId,
            Tipo = "Revision",
            Titulo = "Revisión de Rex",
            FechaObjetivo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3),
        })).Content.ReadFromJsonAsync<RecordatorioResp>();

        var enviar = await cliente.PostAsync(new Uri($"/recordatorios/{creado!.Id}/enviar", UriKind.Relative), content: null);
        enviar.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Sigue pendiente: un fallo de envío no lo marca como enviado.
        var obtenido = await cliente.GetFromJsonAsync<RecordatorioResp>($"/recordatorios/{creado.Id}");
        obtenido!.Estado.Should().Be("Pendiente");
    }

    [Fact]
    public async Task Enviar_pendientes_envia_los_que_tienen_email_y_cuenta_los_fallidos()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var emailOk = $"ok-{Guid.NewGuid():N}@ejemplo.com";
        var conCorreo = await CrearClienteAsync(cliente, "Con correo", emailOk);
        var sinCorreo = await CrearClienteAsync(cliente, "Sin correo");
        var animalOk = await CrearAnimalAsync(cliente, conCorreo, "Kira");
        var animalSin = await CrearAnimalAsync(cliente, sinCorreo, "Tom");

        var manana = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        await cliente.PostAsJsonAsync("/recordatorios", new { AnimalId = animalOk, Tipo = "Vacuna", Titulo = "Vacuna de Kira", FechaObjetivo = manana });
        await cliente.PostAsJsonAsync("/recordatorios", new { AnimalId = animalSin, Tipo = "Vacuna", Titulo = "Vacuna de Tom", FechaObjetivo = manana });

        var resumen = await (await cliente.PostAsync(new Uri("/recordatorios/enviar-pendientes?dias=30", UriKind.Relative), content: null))
            .Content.ReadFromJsonAsync<ResumenResp>();

        resumen!.Enviados.Should().Be(1);
        resumen.Fallidos.Should().ContainSingle(f => f.Codigo == "recordatorio.sin_email");
        _fabrica.Correo.ParaDestinatario(emailOk).Should().ContainSingle();
    }

    [Fact]
    public async Task Los_recordatorios_estan_aislados_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(empresaA, "Cliente de A");
        var animalA = await CrearAnimalAsync(empresaA, clienteA, "Animal de A");
        await empresaA.PostAsJsonAsync("/recordatorios", new { AnimalId = animalA, Tipo = "Otro", Titulo = "Privado de A", FechaObjetivo = new DateOnly(2026, 6, 1) });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var listadoB = await empresaB.GetFromJsonAsync<List<RecordatorioResp>>("/recordatorios");
        listadoB!.Should().BeEmpty("cada empresa solo ve sus propios recordatorios");
    }

    private sealed record FalloResp(Guid RecordatorioId, string Codigo, string Mensaje);

    private sealed record ResumenResp(int Enviados, List<FalloResp> Fallidos);
}
