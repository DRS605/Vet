using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de los campos personalizados de clientes y animales.</summary>
public sealed class CampoPersonalizadoEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public CampoPersonalizadoEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record CampoResp(Guid Id, string Entidad, string Etiqueta, string Clave, string Tipo, List<string> Opciones, bool Obligatorio, int Orden, bool Activo);
    private sealed record ValorResp(Guid CampoId, string Etiqueta, string Clave, string Tipo, List<string> Opciones, bool Obligatorio, int Orden, string? Valor);
    private sealed record ClienteResp(Guid Id, string Nombre);
    private sealed record AnimalResp(Guid Id, string Nombre);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente, string nombre)
    {
        var creado = await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = nombre })).Content.ReadFromJsonAsync<ClienteResp>();
        return creado!.Id;
    }

    private static async Task<Guid> CrearAnimalAsync(HttpClient cliente, Guid clienteId)
    {
        var creado = await (await cliente.PostAsJsonAsync("/animales", new
        {
            ClienteId = clienteId, Nombre = "Toby", Especie = "Perro", Sexo = "Macho",
        })).Content.ReadFromJsonAsync<AnimalResp>();
        return creado!.Id;
    }

    [Fact]
    public async Task Crear_editar_y_dar_de_baja_un_campo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var crear = await cliente.PostAsJsonAsync("/campos-personalizados", new
        {
            Entidad = "Animal", Etiqueta = "Nº de chip aseguradora", Tipo = "Texto", Orden = 1,
        });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await crear.Content.ReadFromJsonAsync<CampoResp>();
        creado!.Clave.Should().Be("n_de_chip_aseguradora");
        creado.Entidad.Should().Be("Animal");

        var editar = await cliente.PutAsJsonAsync($"/campos-personalizados/{creado.Id}", new
        {
            Entidad = "Animal", Etiqueta = "Chip", Tipo = "Numero", Obligatorio = true, Orden = 2,
        });
        editar.StatusCode.Should().Be(HttpStatusCode.OK);
        var editado = await editar.Content.ReadFromJsonAsync<CampoResp>();
        editado!.Tipo.Should().Be("Numero");
        editado.Obligatorio.Should().BeTrue();

        var baja = await cliente.DeleteAsync(new Uri($"/campos-personalizados/{creado.Id}", UriKind.Relative));
        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var activos = await cliente.GetFromJsonAsync<List<CampoResp>>("/campos-personalizados?entidad=Animal");
        activos!.Should().NotContain(c => c.Id == creado.Id);
        var todos = await cliente.GetFromJsonAsync<List<CampoResp>>("/campos-personalizados?entidad=Animal&incluirInactivos=true");
        todos!.Should().Contain(c => c.Id == creado.Id && !c.Activo);
    }

    [Fact]
    public async Task Crear_campo_duplicado_devuelve_409()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await cliente.PostAsJsonAsync("/campos-personalizados", new { Entidad = "Cliente", Etiqueta = "DNI escaneado", Tipo = "Texto" });
        // Misma clave normalizada aunque cambie mayúsculas/acentos.
        var repetido = await cliente.PostAsJsonAsync("/campos-personalizados", new { Entidad = "Cliente", Etiqueta = "  dni  escaneado ", Tipo = "Texto" });
        repetido.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task El_mismo_nombre_en_distinta_entidad_no_choca()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        (await cliente.PostAsJsonAsync("/campos-personalizados", new { Entidad = "Cliente", Etiqueta = "Observaciones", Tipo = "TextoLargo" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await cliente.PostAsJsonAsync("/campos-personalizados", new { Entidad = "Animal", Etiqueta = "Observaciones", Tipo = "TextoLargo" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Guardar_y_recuperar_valores_de_una_ficha()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Ana");
        var animalId = await CrearAnimalAsync(cliente, clienteId);

        var chip = await (await cliente.PostAsJsonAsync("/campos-personalizados", new { Entidad = "Animal", Etiqueta = "Chip", Tipo = "Numero", Orden = 1 })).Content.ReadFromJsonAsync<CampoResp>();
        var temperamento = await (await cliente.PostAsJsonAsync("/campos-personalizados", new { Entidad = "Animal", Etiqueta = "Temperamento", Tipo = "Lista", Opciones = "Tranquilo\nNervioso", Orden = 2 })).Content.ReadFromJsonAsync<CampoResp>();

        // Al abrir la ficha, los campos aparecen sin valor.
        var vacios = await cliente.GetFromJsonAsync<List<ValorResp>>($"/campos-personalizados/valores/Animal/{animalId}");
        vacios!.Should().HaveCount(2);
        vacios.Should().OnlyContain(v => v.Valor == null);

        var guardar = await cliente.PutAsJsonAsync($"/campos-personalizados/valores/Animal/{animalId}", new[]
        {
            new { CampoId = chip!.Id, Valor = "12,5" },
            new { CampoId = temperamento!.Id, Valor = "tranquilo" },
        });
        guardar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var valores = (await cliente.GetFromJsonAsync<List<ValorResp>>($"/campos-personalizados/valores/Animal/{animalId}"))!;
        valores.Single(v => v.CampoId == chip.Id).Valor.Should().Be("12.5");
        valores.Single(v => v.CampoId == temperamento.Id).Valor.Should().Be("Tranquilo");

        // El formulario reenvía todos los campos: al vaciar el chip se borra su valor; el temperamento
        // se mantiene porque se vuelve a enviar con su valor.
        await cliente.PutAsJsonAsync($"/campos-personalizados/valores/Animal/{animalId}", new[]
        {
            new { CampoId = chip.Id, Valor = "" },
            new { CampoId = temperamento.Id, Valor = "Tranquilo" },
        });
        var tras = (await cliente.GetFromJsonAsync<List<ValorResp>>($"/campos-personalizados/valores/Animal/{animalId}"))!;
        tras.Single(v => v.CampoId == chip.Id).Valor.Should().BeNull();
        tras.Single(v => v.CampoId == temperamento.Id).Valor.Should().Be("Tranquilo");
    }

    [Fact]
    public async Task Un_campo_obligatorio_vacio_impide_guardar()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, "Ana");

        await cliente.PostAsJsonAsync("/campos-personalizados", new { Entidad = "Cliente", Etiqueta = "Consentimiento RGPD", Tipo = "Texto", Obligatorio = true });

        var guardar = await cliente.PutAsJsonAsync($"/campos-personalizados/valores/Cliente/{clienteId}", Array.Empty<object>());
        guardar.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Los_campos_estan_aislados_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await empresaA.PostAsJsonAsync("/campos-personalizados", new { Entidad = "Cliente", Etiqueta = "Solo A", Tipo = "Texto" });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var camposB = await empresaB.GetFromJsonAsync<List<CampoResp>>("/campos-personalizados?entidad=Cliente");
        camposB!.Should().BeEmpty("los campos de A no los ve B");
    }

    [Fact]
    public async Task Entidad_invalida_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var r = await cliente.GetAsync(new Uri("/campos-personalizados?entidad=Factura", UriKind.Relative));
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
