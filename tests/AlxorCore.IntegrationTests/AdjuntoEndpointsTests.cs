using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de los adjuntos (fotos/documentos) de la ficha del animal.</summary>
public sealed class AdjuntoEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public AdjuntoEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record AnimalResp(Guid Id);
    private sealed record AdjuntoResp(Guid Id, Guid AnimalId, string NombreArchivo, string TipoMime, int Tamano, bool EsImagen);

    private static async Task<Guid> CrearAnimalAsync(HttpClient c)
    {
        var cli = await (await c.PostAsJsonAsync("/clientes", new { Nombre = "Dueño" })).Content.ReadFromJsonAsync<ClienteResp>();
        var an = await (await c.PostAsJsonAsync("/animales", new { ClienteId = cli!.Id, Nombre = "Boby", Especie = "Perro", Sexo = "Macho" })).Content.ReadFromJsonAsync<AnimalResp>();
        return an!.Id;
    }

    private static MultipartFormDataContent Fichero(byte[] datos, string nombre, string mime)
    {
        var contenido = new ByteArrayContent(datos);
        contenido.Headers.ContentType = new MediaTypeHeaderValue(mime);
        var form = new MultipartFormDataContent();
        form.Add(contenido, "archivo", nombre);
        return form;
    }

    [Fact]
    public async Task Subir_listar_descargar_y_eliminar_un_adjunto()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var animalId = await CrearAnimalAsync(cliente);
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var subir = await cliente.PostAsync(new Uri($"/animales/{animalId}/adjuntos", UriKind.Relative), Fichero(bytes, "herida.png", "image/png"));
        subir.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await subir.Content.ReadFromJsonAsync<AdjuntoResp>();
        creado!.EsImagen.Should().BeTrue();
        creado.Tamano.Should().Be(8);

        var lista = await cliente.GetFromJsonAsync<List<AdjuntoResp>>($"/animales/{animalId}/adjuntos");
        lista!.Should().ContainSingle(x => x.Id == creado.Id && x.NombreArchivo == "herida.png");

        var descarga = await cliente.GetAsync(new Uri($"/adjuntos/{creado.Id}", UriKind.Relative));
        descarga.StatusCode.Should().Be(HttpStatusCode.OK);
        (await descarga.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);

        var borrar = await cliente.DeleteAsync(new Uri($"/adjuntos/{creado.Id}", UriKind.Relative));
        borrar.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await cliente.GetFromJsonAsync<List<AdjuntoResp>>($"/animales/{animalId}/adjuntos"))!.Should().BeEmpty();
    }

    [Fact]
    public async Task Rechaza_un_tipo_no_permitido()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var animalId = await CrearAnimalAsync(cliente);
        var subir = await cliente.PostAsync(new Uri($"/animales/{animalId}/adjuntos", UriKind.Relative), Fichero(new byte[] { 1, 2, 3 }, "virus.exe", "application/octet-stream"));
        subir.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
