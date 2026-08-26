using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Terceros (clientes).</summary>
public sealed class TercerosEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public TercerosEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteDto(Guid Id, string Nombre, string? NifFiscal, decimal PorcentajeIrpfDefecto, bool Activo, bool RecargoEquivalencia);

    [Fact]
    public async Task Exportar_datos_incluye_los_clientes_de_la_empresa()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente Exportable SL", NifFiscal = "B12345674" });

        var resp = await cliente.GetAsync(new Uri("/cuenta/exportar", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var json = await resp.Content.ReadAsStringAsync();
        json.Should().Contain("Cliente Exportable SL");
        json.Should().Contain("\"empresa\"");
        json.Should().Contain("\"facturas\"");
    }

    [Fact]
    public async Task Eliminar_la_cuenta_borra_sus_datos_y_respeta_a_las_demas_empresas()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await empresaA.PostAsJsonAsync("/clientes", new { Nombre = "Cliente de A" });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await empresaB.PostAsJsonAsync("/clientes", new { Nombre = "Cliente de B" });

        var borrar = await empresaA.DeleteAsync(new Uri("/cuenta", UriKind.Relative));
        borrar.StatusCode.Should().Be(HttpStatusCode.OK);

        // La empresa A ya no tiene datos; la B sigue intacta.
        var clientesA = await empresaA.GetFromJsonAsync<List<ClienteDto>>("/clientes");
        clientesA.Should().BeEmpty();
        var clientesB = await empresaB.GetFromJsonAsync<List<ClienteDto>>("/clientes");
        clientesB.Should().ContainSingle(c => c.Nombre == "Cliente de B");
    }

    [Fact]
    public async Task Cliente_guarda_el_indicador_de_recargo_de_equivalencia()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var creado = await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Minorista SL", RecargoEquivalencia = true })).Content.ReadFromJsonAsync<ClienteDto>();
        creado!.RecargoEquivalencia.Should().BeTrue();

        var obtenido = await cliente.GetFromJsonAsync<ClienteDto>($"/clientes/{creado.Id}");
        obtenido!.RecargoEquivalencia.Should().BeTrue();
    }

    [Fact]
    public async Task Crear_listar_obtener_y_actualizar_cliente()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var crear = await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Acme SL", NifFiscal = "B12345674", PorcentajeIrpfDefecto = 15m });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await crear.Content.ReadFromJsonAsync<ClienteDto>();
        creado!.Nombre.Should().Be("Acme SL");

        var lista = await cliente.GetFromJsonAsync<List<ClienteDto>>("/clientes");
        lista.Should().ContainSingle(c => c.Id == creado.Id);

        var obtenido = await cliente.GetFromJsonAsync<ClienteDto>($"/clientes/{creado.Id}");
        obtenido!.PorcentajeIrpfDefecto.Should().Be(15m);

        var actualizar = await cliente.PutAsJsonAsync($"/clientes/{creado.Id}", new { Nombre = "Acme Renombrada SL", PorcentajeIrpfDefecto = 7m });
        actualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualizado = await actualizar.Content.ReadFromJsonAsync<ClienteDto>();
        actualizado!.Nombre.Should().Be("Acme Renombrada SL");
        actualizado.PorcentajeIrpfDefecto.Should().Be(7m);
    }

    [Fact]
    public async Task Dar_de_baja_cliente_lo_oculta_del_listado_pero_conserva_su_ficha()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var crear = await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente a dar de baja SL" });
        var creado = await crear.Content.ReadFromJsonAsync<ClienteDto>();

        var baja = await cliente.DeleteAsync(new Uri($"/clientes/{creado!.Id}", UriKind.Relative));
        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Ya no aparece en el listado (baja lógica, no borrado físico)…
        var lista = await cliente.GetFromJsonAsync<List<ClienteDto>>("/clientes");
        lista.Should().NotContain(c => c.Id == creado.Id);

        // …pero su ficha sigue existiendo, marcada como inactiva.
        var obtenido = await cliente.GetFromJsonAsync<ClienteDto>($"/clientes/{creado.Id}");
        obtenido!.Activo.Should().BeFalse();
    }

    [Fact]
    public async Task Dar_de_baja_cliente_inexistente_devuelve_404()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var baja = await cliente.DeleteAsync(new Uri($"/clientes/{Guid.NewGuid()}", UriKind.Relative));
        baja.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Crear_cliente_sin_nombre_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var crear = await cliente.PostAsJsonAsync("/clientes", new { Nombre = "", PorcentajeIrpfDefecto = 0m });
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Los_clientes_estan_aislados_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await empresaA.PostAsJsonAsync("/clientes", new { Nombre = "Cliente de A", PorcentajeIrpfDefecto = 0m });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var listaB = await empresaB.GetFromJsonAsync<List<ClienteDto>>("/clientes");

        listaB.Should().BeEmpty("cada empresa solo ve sus propios clientes");
    }
}
