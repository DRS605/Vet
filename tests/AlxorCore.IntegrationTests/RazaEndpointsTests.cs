using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del maestro de razas y del emoji de especie.</summary>
public sealed class RazaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public RazaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record RazaResp(Guid Id, string Especie, string Nombre, bool Activo);
    private sealed record EspecieResp(Guid Id, string Nombre, int MesesCachorro, bool Activo, string? Emoji);

    [Fact]
    public async Task Especies_por_defecto_traen_emoji()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var especies = await cliente.GetFromJsonAsync<List<EspecieResp>>("/especies");
        especies!.Should().Contain(e => e.Nombre == "Perro" && e.Emoji == "🐕");
        especies!.Should().Contain(e => e.Nombre == "Gato" && e.Emoji == "🐈");
    }

    [Fact]
    public async Task Crear_especie_con_emoji_y_editarlo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var creada = await (await cliente.PostAsJsonAsync("/especies", new { Nombre = "Serpiente", MesesCachorro = 12, Emoji = "🐍" })).Content.ReadFromJsonAsync<EspecieResp>();
        creada!.Emoji.Should().Be("🐍");
        var editada = await (await cliente.PutAsJsonAsync($"/especies/{creada.Id}", new { Nombre = "Serpiente", MesesCachorro = 12, Emoji = "🐉" })).Content.ReadFromJsonAsync<EspecieResp>();
        editada!.Emoji.Should().Be("🐉");
    }

    [Fact]
    public async Task Crear_editar_listar_y_dar_de_baja_razas_por_especie()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var vacio = await cliente.GetFromJsonAsync<List<RazaResp>>("/razas?especie=Perro");
        vacio!.Should().BeEmpty();

        var lab = await (await cliente.PostAsJsonAsync("/razas", new { Especie = "Perro", Nombre = "Labrador" })).Content.ReadFromJsonAsync<RazaResp>();
        lab!.Especie.Should().Be("Perro");
        await cliente.PostAsJsonAsync("/razas", new { Especie = "Perro", Nombre = "Bulldog" });
        await cliente.PostAsJsonAsync("/razas", new { Especie = "Gato", Nombre = "Siamés" });

        var perros = await cliente.GetFromJsonAsync<List<RazaResp>>("/razas?especie=Perro");
        perros!.Select(r => r.Nombre).Should().BeEquivalentTo(new[] { "Labrador", "Bulldog" });

        var editar = await cliente.PutAsJsonAsync($"/razas/{lab.Id}", new { Especie = "Perro", Nombre = "Golden Retriever" });
        editar.StatusCode.Should().Be(HttpStatusCode.OK);

        var baja = await cliente.DeleteAsync(new Uri($"/razas/{lab.Id}", UriKind.Relative));
        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await cliente.GetFromJsonAsync<List<RazaResp>>("/razas?especie=Perro"))!.Should().NotContain(r => r.Id == lab.Id);
        (await cliente.GetFromJsonAsync<List<RazaResp>>("/razas?especie=Perro&incluirInactivas=true"))!.Should().Contain(r => r.Id == lab.Id && !r.Activo);

        // Todas las razas de la empresa (sin filtro).
        (await cliente.GetFromJsonAsync<List<RazaResp>>("/razas"))!.Should().Contain(r => r.Nombre == "Siamés");
    }

    [Fact]
    public async Task Raza_duplicada_por_especie_devuelve_409()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await cliente.PostAsJsonAsync("/razas", new { Especie = "Perro", Nombre = "Beagle" });
        var rep = await cliente.PostAsJsonAsync("/razas", new { Especie = "Perro", Nombre = "Beagle" });
        rep.StatusCode.Should().Be(HttpStatusCode.Conflict);
        // La misma raza en otra especie no choca.
        (await cliente.PostAsJsonAsync("/razas", new { Especie = "Gato", Nombre = "Beagle" })).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Las_razas_estan_aisladas_por_empresa()
    {
        var (a, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await a.PostAsJsonAsync("/razas", new { Especie = "Perro", Nombre = "Pastor Alemán" });
        var (b, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        (await b.GetFromJsonAsync<List<RazaResp>>("/razas"))!.Should().BeEmpty();
    }
}
