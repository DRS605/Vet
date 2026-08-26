using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del maestro de especies (nuevo agregado editable por empresa).</summary>
public sealed class EspecieEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public EspecieEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record EspecieResp(Guid Id, string Nombre, int MesesCachorro, bool Activo);
    private sealed record ClienteResp(Guid Id, string Nombre);
    private sealed record AnimalResp(Guid Id, string Nombre, string Especie, int? EdadMeses, bool EsCachorro);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente, string nombre)
    {
        var creado = await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = nombre })).Content.ReadFromJsonAsync<ClienteResp>();
        return creado!.Id;
    }

    [Fact]
    public async Task Empresa_nueva_arranca_con_las_siete_especies_por_defecto()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var especies = await cliente.GetFromJsonAsync<List<EspecieResp>>("/especies");
        especies!.Select(e => e.Nombre).Should().BeEquivalentTo(
            new[] { "Perro", "Gato", "Conejo", "Ave", "Huron", "Reptil", "Otro" });
        especies.Should().Contain(e => e.Nombre == "Conejo" && e.MesesCachorro == 6);
        especies.Should().Contain(e => e.Nombre == "Perro" && e.MesesCachorro == 12);
    }

    [Fact]
    public async Task Crear_editar_y_dar_de_baja_una_especie()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var crear = await cliente.PostAsJsonAsync("/especies", new { Nombre = "Tortuga", MesesCachorro = 24 });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creada = await crear.Content.ReadFromJsonAsync<EspecieResp>();
        creada!.Nombre.Should().Be("Tortuga");
        creada.MesesCachorro.Should().Be(24);

        var editar = await cliente.PutAsJsonAsync($"/especies/{creada.Id}", new { Nombre = "Tortuga de tierra", MesesCachorro = 36 });
        editar.StatusCode.Should().Be(HttpStatusCode.OK);
        var editada = await editar.Content.ReadFromJsonAsync<EspecieResp>();
        editada!.Nombre.Should().Be("Tortuga de tierra");
        editada.MesesCachorro.Should().Be(36);

        var baja = await cliente.DeleteAsync(new Uri($"/especies/{creada.Id}", UriKind.Relative));
        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var activas = await cliente.GetFromJsonAsync<List<EspecieResp>>("/especies");
        activas!.Should().NotContain(e => e.Id == creada.Id);

        var todas = await cliente.GetFromJsonAsync<List<EspecieResp>>("/especies?incluirInactivas=true");
        todas!.Should().Contain(e => e.Id == creada.Id && !e.Activo);
    }

    [Fact]
    public async Task Crear_especie_duplicada_devuelve_409()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var repetida = await cliente.PostAsJsonAsync("/especies", new { Nombre = "Perro", MesesCachorro = 12 });
        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Crear_animal_con_especie_inexistente_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario");

        var crear = await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId, Nombre = "Nessie", Especie = "Dragón", Sexo = "Macho",
        });
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Crear_animal_con_especie_dada_de_baja_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario");

        var especie = await (await cliente.PostAsJsonAsync("/especies", new { Nombre = "Pez", MesesCachorro = 6 })).Content.ReadFromJsonAsync<EspecieResp>();
        await cliente.DeleteAsync(new Uri($"/especies/{especie!.Id}", UriKind.Relative));

        var crear = await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId, Nombre = "Burbuja", Especie = "Pez", Sexo = "Desconocido",
        });
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cachorro_usa_el_umbral_del_maestro()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Conejo por defecto: umbral 6 meses. Con 5 meses es cachorro; con 8, ya no.
        var cachorro = await (await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId, Nombre = "Bugs", Especie = "Conejo", Sexo = "Macho",
            FechaNacimiento = hoy.AddMonths(-5),
        })).Content.ReadFromJsonAsync<AnimalResp>();
        cachorro!.EsCachorro.Should().BeTrue("un conejo de 5 meses está por debajo del umbral 6 del maestro");

        var adulto = await (await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId, Nombre = "Roger", Especie = "Conejo", Sexo = "Macho",
            FechaNacimiento = hoy.AddMonths(-8),
        })).Content.ReadFromJsonAsync<AnimalResp>();
        adulto!.EsCachorro.Should().BeFalse("un conejo de 8 meses supera el umbral 6 del maestro");
    }

    [Fact]
    public async Task Editar_el_umbral_del_maestro_cambia_el_calculo_de_cachorro()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Propietario");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var gatos = (await cliente.GetFromJsonAsync<List<EspecieResp>>("/especies"))!;
        var gato = gatos.Single(e => e.Nombre == "Gato");
        // Bajamos el umbral de Gato a 3 meses.
        await cliente.PutAsJsonAsync($"/especies/{gato.Id}", new { Nombre = "Gato", MesesCachorro = 3 });

        var animal = await (await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId, Nombre = "Micha", Especie = "Gato", Sexo = "Hembra",
            FechaNacimiento = hoy.AddMonths(-5),
        })).Content.ReadFromJsonAsync<AnimalResp>();
        animal!.EsCachorro.Should().BeFalse("con el umbral bajado a 3, un gato de 5 meses ya no es cachorro");
    }

    [Fact]
    public async Task Las_especies_estan_aisladas_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await empresaA.PostAsJsonAsync("/especies", new { Nombre = "Tortuga", MesesCachorro = 24 });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var especiesB = await empresaB.GetFromJsonAsync<List<EspecieResp>>("/especies");
        especiesB!.Should().NotContain(e => e.Nombre == "Tortuga", "la especie propia de A no la ve B");
        especiesB!.Should().HaveCount(7, "B solo tiene sus 7 especies por defecto");
    }
}
