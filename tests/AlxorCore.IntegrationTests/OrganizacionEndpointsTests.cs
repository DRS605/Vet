using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Organización: alta de empresa, selección y aislamiento multiempresa.</summary>
public sealed class OrganizacionEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public OrganizacionEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record RegistroPeticion(string Email, string Nombre, string Contrasena);

    private sealed record LoginPeticion(string Email, string Contrasena);

    private sealed record LoginRespuesta(string Token);

    private sealed record CrearEmpresaPeticion(string Nif, string RazonSocial);

    private sealed record EmpresaDto(Guid Id, string Nif, string RazonSocial);

    private sealed record EmpresaCompletaDto(
        Guid Id, string Nif, string RazonSocial, string RegimenIva,
        string Calle, string CodigoPostal, string Poblacion, string Provincia);

    private sealed record EmpresaResumen(Guid Id, string Nif, string RazonSocial, string RolCodigo);

    private sealed record SeleccionRespuesta(string Token, Guid EmpresaId, string RolCodigo, IReadOnlyList<string> Permisos);

    private sealed record SerieDto(Guid Id, string TipoDocumento, int Ejercicio, string Prefijo, long SiguienteNumero);

    private static string EmailUnico() => $"u{Guid.NewGuid():N}@ejemplo.com";

    private static int _contadorNif = 10_000_000;

    /// <summary>Genera un DNI válido y único para no chocar con el índice único de NIF.</summary>
    private static string GenerarNif()
    {
        const string letras = "TRWAGMYFPDXBNJZSQVHLCKE";
        var numero = System.Threading.Interlocked.Increment(ref _contadorNif) % 100_000_000;
        return $"{numero:D8}{letras[numero % 23]}";
    }

    /// <summary>Registra e inicia sesión, devolviendo un cliente autenticado (sin empresa activa).</summary>
    private async Task<HttpClient> ClienteAutenticadoAsync()
    {
        var cliente = _fabrica.CreateClient();
        var email = EmailUnico();
        await cliente.PostAsJsonAsync("/auth/registro", new RegistroPeticion(email, "Ana", "contrasena123"));
        var login = await cliente.PostAsJsonAsync("/auth/login", new LoginPeticion(email, "contrasena123"));
        var datos = await login.Content.ReadFromJsonAsync<LoginRespuesta>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", datos!.Token);
        return cliente;
    }

    [Fact]
    public async Task Flujo_crear_seleccionar_y_consultar_empresa()
    {
        var cliente = await ClienteAutenticadoAsync();

        // Crear empresa
        var crear = await cliente.PostAsJsonAsync("/empresas", new CrearEmpresaPeticion(GenerarNif(), "Mi Empresa SL"));
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var empresa = await crear.Content.ReadFromJsonAsync<EmpresaDto>();

        // Listar mis empresas
        var mias = await cliente.GetFromJsonAsync<List<EmpresaResumen>>("/empresas");
        mias.Should().ContainSingle(e => e.Id == empresa!.Id && e.RolCodigo == "propietario");

        // Seleccionar empresa -> token con alcance
        var seleccion = await cliente.PostAsync(new Uri($"/empresas/{empresa!.Id}/seleccionar", UriKind.Relative), content: null);
        seleccion.StatusCode.Should().Be(HttpStatusCode.OK);
        var alcance = await seleccion.Content.ReadFromJsonAsync<SeleccionRespuesta>();
        alcance!.EmpresaId.Should().Be(empresa.Id);
        alcance.Permisos.Should().Contain("factura.emitir");

        // Usar el token con alcance de empresa
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alcance.Token);
        var actual = await cliente.GetFromJsonAsync<EmpresaDto>("/empresas/actual");
        actual!.Id.Should().Be(empresa.Id);
    }

    [Fact]
    public async Task Crear_serie_y_listarla_en_la_empresa_activa()
    {
        var cliente = await ClienteAutenticadoAsync();
        var crear = await cliente.PostAsJsonAsync("/empresas", new CrearEmpresaPeticion(GenerarNif(), "Otra SL"));
        var empresa = await crear.Content.ReadFromJsonAsync<EmpresaDto>();
        var seleccion = await cliente.PostAsync(new Uri($"/empresas/{empresa!.Id}/seleccionar", UriKind.Relative), content: null);
        var alcance = await seleccion.Content.ReadFromJsonAsync<SeleccionRespuesta>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alcance!.Token);

        var crearSerie = await cliente.PostAsJsonAsync("/series", new { TipoDocumento = "Factura", Ejercicio = 2026, Prefijo = "FB" });
        crearSerie.StatusCode.Should().Be(HttpStatusCode.OK);

        var series = await cliente.GetFromJsonAsync<List<SerieDto>>("/series");
        series.Should().ContainSingle(s => s.Prefijo == "FB" && s.Ejercicio == 2026);
    }

    [Fact]
    public async Task Actualizar_empresa_y_leerla()
    {
        var cliente = await ClienteAutenticadoAsync();
        var crear = await cliente.PostAsJsonAsync("/empresas", new CrearEmpresaPeticion(GenerarNif(), "Clínica Vieja SL"));
        var empresa = await crear.Content.ReadFromJsonAsync<EmpresaDto>();
        var seleccion = await cliente.PostAsync(new Uri($"/empresas/{empresa!.Id}/seleccionar", UriKind.Relative), content: null);
        var alcance = await seleccion.Content.ReadFromJsonAsync<SeleccionRespuesta>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alcance!.Token);
        alcance.Permisos.Should().Contain("empresa.ajustes");

        var nuevoNif = GenerarNif();
        var actualizar = await cliente.PutAsJsonAsync("/empresas/actual", new
        {
            Nif = nuevoNif,
            RazonSocial = "Clínica Nueva SL",
            Calle = "Calle Mayor 1",
            CodigoPostal = "28013",
            Poblacion = "Madrid",
            Provincia = "Madrid",
            RegimenIva = "RecargoEquivalencia",
        });
        actualizar.StatusCode.Should().Be(HttpStatusCode.OK);

        var leida = await cliente.GetFromJsonAsync<EmpresaCompletaDto>("/empresas/actual");
        leida!.RazonSocial.Should().Be("Clínica Nueva SL");
        leida.Nif.Should().Be(nuevoNif);
        leida.RegimenIva.Should().Be("RecargoEquivalencia");
        leida.Calle.Should().Be("Calle Mayor 1");
        leida.Poblacion.Should().Be("Madrid");
    }

    [Fact]
    public async Task Un_usuario_no_puede_seleccionar_la_empresa_de_otro()
    {
        // Usuario A crea su empresa
        var clienteA = await ClienteAutenticadoAsync();
        var crearA = await clienteA.PostAsJsonAsync("/empresas", new CrearEmpresaPeticion(GenerarNif(), "Empresa A"));
        var empresaA = await crearA.Content.ReadFromJsonAsync<EmpresaDto>();

        // Usuario B intenta seleccionar la empresa de A
        var clienteB = await ClienteAutenticadoAsync();
        var intento = await clienteB.PostAsync(new Uri($"/empresas/{empresaA!.Id}/seleccionar", UriKind.Relative), content: null);
        intento.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Y no ve ninguna empresa como propia
        var empresasB = await clienteB.GetFromJsonAsync<List<EmpresaResumen>>("/empresas");
        empresasB.Should().NotContain(e => e.Id == empresaA.Id);
    }

    [Fact]
    public async Task Crear_empresa_con_nif_invalido_devuelve_400()
    {
        var cliente = await ClienteAutenticadoAsync();
        var crear = await cliente.PostAsJsonAsync("/empresas", new CrearEmpresaPeticion("NOVALIDO", "X SL"));
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
