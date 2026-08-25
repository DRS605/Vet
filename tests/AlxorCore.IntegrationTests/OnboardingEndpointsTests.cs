using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>
/// Pruebas del endpoint público <c>/estado-instalacion</c>. Va en su propia clase (y por tanto su
/// propia fábrica, que trunca las tablas al iniciar) para poder afirmar de forma determinista que la
/// instalación pasa de «no inicializada» a «inicializada» al crear la primera empresa.
/// </summary>
public sealed class EstadoInstalacionEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public EstadoInstalacionEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record EstadoResp(bool Inicializada);
    private sealed record RegistroPeticion(string Email, string Nombre, string Contrasena);
    private sealed record LoginPeticion(string Email, string Contrasena);
    private sealed record LoginRespuesta(string Token);

    [Fact]
    public async Task Estado_pasa_de_no_inicializada_a_inicializada_al_crear_la_primera_empresa()
    {
        var anonimo = _fabrica.CreateClient();

        // Con la base recién truncada no hay ninguna empresa: la instalación NO está inicializada.
        var antes = await anonimo.GetFromJsonAsync<EstadoResp>("/estado-instalacion");
        antes!.Inicializada.Should().BeFalse("aún no existe ninguna empresa");

        // No requiere autenticación (endpoint público para que la SPA elija asistente o login).
        var sinToken = await anonimo.GetAsync(new Uri("/estado-instalacion", UriKind.Relative));
        sinToken.StatusCode.Should().Be(HttpStatusCode.OK);

        // Se da de alta la primera empresa siguiendo el flujo real (registrar → login → crear empresa).
        var email = Ayudas.EmailUnico();
        await anonimo.PostAsJsonAsync("/auth/registro", new RegistroPeticion(email, "Admin", "contrasena123"));
        var login = await anonimo.PostAsJsonAsync("/auth/login", new LoginPeticion(email, "contrasena123"));
        var datos = await login.Content.ReadFromJsonAsync<LoginRespuesta>();
        var autenticado = _fabrica.CreateClient();
        autenticado.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", datos!.Token);
        await autenticado.PostAsJsonAsync("/empresas", new { Nif = Ayudas.GenerarNif(), RazonSocial = "Clínica San Roque" });

        // Ahora sí: existe una empresa, la instalación está inicializada.
        var despues = await anonimo.GetFromJsonAsync<EstadoResp>("/estado-instalacion");
        despues!.Inicializada.Should().BeTrue("ya existe al menos una empresa");
    }
}

/// <summary>Pruebas del endpoint del asistente <c>POST /onboarding/pautas-recomendadas</c>.</summary>
public sealed class OnboardingPautasEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public OnboardingPautasEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record CargaResp(int Creadas, int YaExistentes);

    private sealed record PautaResp(
        Guid Id, string Especie, string Nombre, string Caracter, int? EdadInicioSemanas, int? PeriodicidadRefuerzoMeses, bool Activo);

    [Fact]
    public async Task Carga_el_cuadro_recomendado_completo_y_es_idempotente()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var primera = await cliente.PostAsJsonAsync("/onboarding/pautas-recomendadas", new { });
        primera.StatusCode.Should().Be(HttpStatusCode.OK);
        var carga1 = await primera.Content.ReadFromJsonAsync<CargaResp>();
        carga1!.Creadas.Should().Be(11, "el cuadro por defecto trae 11 pautas (perro, gato, conejo y hurón)");
        carga1.YaExistentes.Should().Be(0);

        var pautas = await cliente.GetFromJsonAsync<List<PautaResp>>("/vacunas/pautas");
        pautas!.Should().HaveCount(11);
        pautas.Should().Contain(p => p.Especie == "Perro" && p.Nombre == "Rabia" && p.Caracter == "Legal");
        pautas.Should().Contain(p => p.Especie == "Conejo" && p.Nombre == "Mixomatosis" && p.PeriodicidadRefuerzoMeses == 6);

        // Segunda carga: idempotente, no duplica ninguna.
        var segunda = await cliente.PostAsJsonAsync("/onboarding/pautas-recomendadas", new { });
        var carga2 = await segunda.Content.ReadFromJsonAsync<CargaResp>();
        carga2!.Creadas.Should().Be(0);
        carga2.YaExistentes.Should().Be(11);

        var pautasTras = await cliente.GetFromJsonAsync<List<PautaResp>>("/vacunas/pautas");
        pautasTras!.Should().HaveCount(11, "la segunda carga no crea duplicados");
    }

    [Fact]
    public async Task Puede_limitarse_a_un_subconjunto_de_especies()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var resp = await cliente.PostAsJsonAsync("/onboarding/pautas-recomendadas", new { Especies = new[] { "Perro", "Gato" } });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var carga = await resp.Content.ReadFromJsonAsync<CargaResp>();
        carga!.Creadas.Should().Be(7, "4 pautas de perro + 3 de gato");

        var pautas = await cliente.GetFromJsonAsync<List<PautaResp>>("/vacunas/pautas");
        pautas!.Should().OnlyContain(p => p.Especie == "Perro" || p.Especie == "Gato");
        pautas.Should().NotContain(p => p.Especie == "Conejo");
    }

    [Fact]
    public async Task Respeta_las_pautas_ya_creadas_a_mano()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        // Ya existe una pauta con el mismo nombre que una del cuadro por defecto.
        await cliente.PostAsJsonAsync("/vacunas/pautas", new { Especie = "Perro", Nombre = "Rabia", Caracter = "Legal" });

        var resp = await cliente.PostAsJsonAsync("/onboarding/pautas-recomendadas", new { Especies = new[] { "Perro" } });
        var carga = await resp.Content.ReadFromJsonAsync<CargaResp>();
        carga!.Creadas.Should().Be(3, "de las 4 de perro, «Rabia» ya existía");
        carga.YaExistentes.Should().Be(1);

        var pautas = await cliente.GetFromJsonAsync<List<PautaResp>>("/vacunas/pautas?especie=Perro");
        pautas!.Should().ContainSingle(p => p.Nombre == "Rabia", "no se duplica la pauta ya creada");
    }

    [Fact]
    public async Task La_carga_esta_aislada_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await empresaA.PostAsJsonAsync("/onboarding/pautas-recomendadas", new { });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var pautasB = await empresaB.GetFromJsonAsync<List<PautaResp>>("/vacunas/pautas");
        pautasB!.Should().BeEmpty("la carga de A no afecta a B");

        // B carga las suyas y sigue viendo solo las propias.
        var resp = await empresaB.PostAsJsonAsync("/onboarding/pautas-recomendadas", new { Especies = new[] { "Gato" } });
        var carga = await resp.Content.ReadFromJsonAsync<CargaResp>();
        carga!.Creadas.Should().Be(3);
        var pautasBTras = await empresaB.GetFromJsonAsync<List<PautaResp>>("/vacunas/pautas");
        pautasBTras!.Should().OnlyContain(p => p.Especie == "Gato");
    }

    [Fact]
    public async Task Sin_permiso_de_empresa_activa_no_autoriza()
    {
        // Autenticado pero sin empresa seleccionada: el token no lleva el permiso vacuna.gestionar,
        // por lo que la política de autorización rechaza la petición (403).
        var cliente = await Ayudas.AutenticadoAsync(_fabrica);
        var resp = await cliente.PostAsJsonAsync("/onboarding/pautas-recomendadas", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
