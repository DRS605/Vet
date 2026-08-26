using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de la verificación de correo y el restablecimiento de contraseña.</summary>
public sealed class CuentaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public CuentaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record PerfilResp(Guid Id, string Email, string Nombre, bool EmailVerificado, string Sexo);
    private sealed record RegistroResp(PerfilResp Perfil, string TokenVerificacion);
    private sealed record LoginResp(string Token, PerfilResp Usuario);
    private sealed record RecuperarResp(string Mensaje, string? Token);

    private static string Email() => $"u{Guid.NewGuid():N}@ejemplo.com";

    /// <summary>Registra e inicia sesión, devolviendo un cliente autenticado y su correo.</summary>
    private async Task<(HttpClient Cliente, string Correo)> ClienteAutenticadoAsync()
    {
        var cliente = _fabrica.CreateClient();
        var email = Email();
        await cliente.PostAsJsonAsync("/auth/registro", new { Email = email, Nombre = "Ana", Contrasena = "contrasena123" });
        var login = await (await cliente.PostAsJsonAsync("/auth/login", new { Email = email, Contrasena = "contrasena123" }))
            .Content.ReadFromJsonAsync<LoginResp>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return (cliente, email);
    }

    [Fact]
    public async Task Actualizar_perfil_cambia_nombre_y_sexo_y_lo_refleja_el_me()
    {
        var (cliente, _) = await ClienteAutenticadoAsync();

        var actualizar = await cliente.PutAsJsonAsync("/cuenta/perfil", new { Nombre = "Ana Vet", Sexo = "Mujer" });
        actualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var devuelto = await actualizar.Content.ReadFromJsonAsync<PerfilResp>();
        devuelto!.Nombre.Should().Be("Ana Vet");
        devuelto.Sexo.Should().Be("Mujer");

        // El "me" (/auth/perfil) refleja el nuevo nombre y sexo.
        var me = await cliente.GetFromJsonAsync<PerfilResp>("/auth/perfil");
        me!.Nombre.Should().Be("Ana Vet");
        me.Sexo.Should().Be("Mujer");
    }

    [Fact]
    public async Task Sexo_por_defecto_es_no_indicado()
    {
        var (cliente, _) = await ClienteAutenticadoAsync();
        var me = await cliente.GetFromJsonAsync<PerfilResp>("/auth/perfil");
        me!.Sexo.Should().Be("NoIndicado");
    }

    [Fact]
    public async Task Cambiar_contrasena_permite_entrar_con_la_nueva()
    {
        var (cliente, email) = await ClienteAutenticadoAsync();

        var cambio = await cliente.PostAsJsonAsync("/cuenta/cambiar-clave", new { ClaveActual = "contrasena123", NuevaClave = "nueva-clave-123" });
        cambio.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var viejo = await cliente.PostAsJsonAsync("/auth/login", new { Email = email, Contrasena = "contrasena123" });
        viejo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var nuevo = await cliente.PostAsJsonAsync("/auth/login", new { Email = email, Contrasena = "nueva-clave-123" });
        nuevo.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cambiar_contrasena_con_actual_incorrecta_falla()
    {
        var (cliente, _) = await ClienteAutenticadoAsync();

        var cambio = await cliente.PostAsJsonAsync("/cuenta/cambiar-clave", new { ClaveActual = "no-es-la-actual", NuevaClave = "nueva-clave-123" });
        cambio.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Verificar_email_con_el_token_del_registro()
    {
        var cliente = _fabrica.CreateClient();
        var email = Email();

        var registro = await (await cliente.PostAsJsonAsync("/auth/registro", new { Email = email, Nombre = "Ana", Contrasena = "contrasena123" }))
            .Content.ReadFromJsonAsync<RegistroResp>();
        registro!.Perfil.EmailVerificado.Should().BeFalse();
        registro.TokenVerificacion.Should().NotBeNullOrEmpty();

        var verif = await cliente.PostAsJsonAsync("/auth/verificar-email", new { Token = registro.TokenVerificacion });
        verif.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await (await cliente.PostAsJsonAsync("/auth/login", new { Email = email, Contrasena = "contrasena123" }))
            .Content.ReadFromJsonAsync<LoginResp>();
        login!.Usuario.EmailVerificado.Should().BeTrue();
    }

    [Fact]
    public async Task Verificar_con_token_invalido_falla()
    {
        var cliente = _fabrica.CreateClient();
        var r = await cliente.PostAsJsonAsync("/auth/verificar-email", new { Token = "no-existe" });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Restablecer_contrasena_permite_entrar_con_la_nueva_y_no_con_la_vieja()
    {
        var cliente = _fabrica.CreateClient();
        var email = Email();
        await cliente.PostAsJsonAsync("/auth/registro", new { Email = email, Nombre = "Ana", Contrasena = "contrasena123" });

        var recuperar = await (await cliente.PostAsJsonAsync("/auth/recuperar", new { Email = email }))
            .Content.ReadFromJsonAsync<RecuperarResp>();
        recuperar!.Token.Should().NotBeNullOrEmpty();

        var reset = await cliente.PostAsJsonAsync("/auth/restablecer", new { Token = recuperar.Token, NuevaContrasena = "nueva-clave-99" });
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // La contraseña vieja ya no vale.
        var viejo = await cliente.PostAsJsonAsync("/auth/login", new { Email = email, Contrasena = "contrasena123" });
        viejo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // La nueva sí.
        var nuevo = await cliente.PostAsJsonAsync("/auth/login", new { Email = email, Contrasena = "nueva-clave-99" });
        nuevo.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Recuperar_con_correo_inexistente_responde_igual_sin_revelar()
    {
        var cliente = _fabrica.CreateClient();
        var r = await cliente.PostAsJsonAsync("/auth/recuperar", new { Email = Email() });
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await r.Content.ReadFromJsonAsync<RecuperarResp>();
        cuerpo!.Token.Should().BeNull(); // no hay cuenta → no hay token
        cuerpo.Mensaje.Should().NotBeNullOrEmpty();
    }
}
