using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Aplicacion.CasosDeUso;

/// <summary>Resultado de invitar a un usuario: su resumen y el token para que fije su contraseña.</summary>
public sealed record ResultadoUsuarioInvitado(UsuarioResumen Usuario, string TokenRestablecimiento);

/// <summary>
/// Caso de uso: crear un usuario <b>invitado</b> a la plataforma. Se da de alta con una contraseña
/// aleatoria (que nadie conoce) y se emite un token de restablecimiento para que el invitado fije la
/// suya mediante el enlace. Lo usa la invitación de miembros a una empresa.
/// </summary>
public sealed class CrearUsuarioInvitado
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IHasherContrasena _hasher;
    private readonly IServicioVerificacionEmail _correo;
    private readonly IUnidadDeTrabajoIdentidad _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearUsuarioInvitado(
        IRepositorioUsuarios usuarios, IHasherContrasena hasher, IServicioVerificacionEmail correo,
        IUnidadDeTrabajoIdentidad unidadDeTrabajo, IReloj reloj)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _correo = correo;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ResultadoUsuarioInvitado>> EjecutarAsync(string emailTexto, string? nombre, CancellationToken ct = default) =>
        await EjecutarAsync(emailTexto, nombre, null, ct).ConfigureAwait(false);

    /// <summary>
    /// Crea el usuario invitado. Si <paramref name="contrasenaInicial"/> viene con valor, se fija esa
    /// contraseña y se da el email por verificado (el invitado puede entrar ya, sin correo). Si es nula,
    /// se mantiene el flujo por token de restablecimiento (enlace por correo).
    /// </summary>
    public async Task<Resultado<ResultadoUsuarioInvitado>> EjecutarAsync(string emailTexto, string? nombre, string? contrasenaInicial, CancellationToken ct = default)
    {
        var email = Email.Crear(emailTexto);
        if (email.EsFallo)
        {
            return Resultado.Fallo<ResultadoUsuarioInvitado>(email.Error);
        }

        var conClave = !string.IsNullOrEmpty(contrasenaInicial);
        if (conClave && (contrasenaInicial!.Length < RegistrarUsuario.LongitudMinimaContrasena || contrasenaInicial.Length > RegistrarUsuario.LongitudMaximaContrasena))
        {
            return Resultado.Fallo<ResultadoUsuarioInvitado>(
                Error.Validacion("usuario.contrasena_invalida", $"La contraseña debe tener entre {RegistrarUsuario.LongitudMinimaContrasena} y {RegistrarUsuario.LongitudMaximaContrasena} caracteres."));
        }

        if (await _usuarios.ExisteEmailAsync(email.Valor, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<ResultadoUsuarioInvitado>(
                Error.Conflicto("usuario.email_en_uso", "Ya existe una cuenta con ese correo electrónico."));
        }

        // Con contraseña inicial: se hashea la elegida por el admin. Sin ella: aleatoria (fijará la suya con token).
        var hash = HashContrasena.DesdeHash(_hasher.Hash(conClave ? contrasenaInicial! : TokenCuenta.Nuevo()));
        var nombreEfectivo = string.IsNullOrWhiteSpace(nombre) ? emailTexto.Split('@')[0] : nombre;

        var usuario = Usuario.Registrar(email.Valor, nombreEfectivo, hash, _reloj);
        if (usuario.EsFallo)
        {
            return Resultado.Fallo<ResultadoUsuarioInvitado>(usuario.Error);
        }

        if (conClave)
        {
            // El admin ya le ha dado credenciales; puede entrar de inmediato.
            usuario.Valor.VerificarEmail(_reloj);
            _usuarios.Agregar(usuario.Valor);
            await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
            var resumenClave = new UsuarioResumen(usuario.Valor.Id, usuario.Valor.Email.Valor, usuario.Valor.Nombre, usuario.Valor.EmailVerificado);
            return Resultado.Ok(new ResultadoUsuarioInvitado(resumenClave, string.Empty));
        }

        var token = TokenCuenta.Nuevo();
        usuario.Valor.EmitirTokenRestablecimiento(token, _reloj.AhoraUtc + RecuperarContrasena.Caducidad, _reloj);

        _usuarios.Agregar(usuario.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        await _correo.EnviarRestablecimientoAsync(usuario.Valor, token, ct).ConfigureAwait(false);

        var resumen = new UsuarioResumen(usuario.Valor.Id, usuario.Valor.Email.Valor, usuario.Valor.Nombre, usuario.Valor.EmailVerificado);
        return Resultado.Ok(new ResultadoUsuarioInvitado(resumen, token));
    }
}
