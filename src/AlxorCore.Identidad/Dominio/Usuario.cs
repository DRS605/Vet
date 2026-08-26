using AlxorCore.Identidad.Dominio.Eventos;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Dominio;

/// <summary>
/// Usuario de la plataforma. Es una identidad global (no pertenece a una empresa): un mismo
/// usuario podrá operar en varias empresas a través de sus membresías (módulo Organización).
/// Raíz de agregado responsable de sus invariantes de estado y credenciales.
/// </summary>
public sealed class Usuario : RaizAgregado<Guid>
{
    public const int LongitudMaximaNombre = 120;

    // Constructor privado para EF Core (rehidratación desde la base de datos).
    private Usuario(Guid id)
        : base(id)
    {
        Email = null!;
        Nombre = null!;
        HashContrasena = null!;
    }

    private Usuario(Guid id, Email email, string nombre, HashContrasena hash, DateTimeOffset ahora)
        : base(id)
    {
        Email = email;
        Nombre = nombre;
        HashContrasena = hash;
        Estado = EstadoUsuario.Activo;
        EmailVerificado = false;
        Sexo = SexoUsuario.NoIndicado;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Correo electrónico (identificador de acceso, único en la plataforma).</summary>
    public Email Email { get; private set; }

    /// <summary>Nombre visible del usuario.</summary>
    public string Nombre { get; private set; }

    /// <summary>Sexo del usuario (dato de perfil opcional; por defecto <see cref="SexoUsuario.NoIndicado"/>).</summary>
    public SexoUsuario Sexo { get; private set; }

    /// <summary>Contraseña cifrada.</summary>
    public HashContrasena HashContrasena { get; private set; }

    /// <summary>Estado de la cuenta.</summary>
    public EstadoUsuario Estado { get; private set; }

    /// <summary>Indica si el usuario ha verificado su correo.</summary>
    public bool EmailVerificado { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    // --- Tokens de cuenta (solo se almacena el hash) ---
    public string? TokenVerificacionHash { get; private set; }
    public DateTimeOffset? TokenVerificacionExpira { get; private set; }
    public string? TokenRestablecimientoHash { get; private set; }
    public DateTimeOffset? TokenRestablecimientoExpira { get; private set; }

    /// <summary>Indica si el usuario puede autenticarse en este momento.</summary>
    public bool PuedeAutenticarse => Estado == EstadoUsuario.Activo;

    /// <summary>
    /// Registra un nuevo usuario ya validado. El correo se activa de inmediato para no añadir
    /// fricción al alta; la verificación de correo se solicita aparte y no bloquea el uso.
    /// </summary>
    public static Resultado<Usuario> Registrar(Email email, string? nombre, HashContrasena hash, IReloj reloj)
    {
        var nombreNormalizado = (nombre ?? string.Empty).Trim();

        if (nombreNormalizado.Length == 0)
        {
            return Resultado.Fallo<Usuario>(Error.Validacion("usuario.nombre_vacio", "El nombre es obligatorio."));
        }

        if (nombreNormalizado.Length > LongitudMaximaNombre)
        {
            return Resultado.Fallo<Usuario>(Error.Validacion("usuario.nombre_largo", "El nombre es demasiado largo."));
        }

        var usuario = new Usuario(Guid.NewGuid(), email, nombreNormalizado, hash, reloj.AhoraUtc);
        usuario.RegistrarEvento(new UsuarioRegistrado(usuario.Id, email.Valor, reloj.AhoraUtc));
        return Resultado.Ok(usuario);
    }

    /// <summary>
    /// Actualiza los datos de perfil editables por el propio usuario: nombre visible y sexo.
    /// Aplica las mismas reglas de validación del nombre que en el alta.
    /// </summary>
    public Resultado ActualizarPerfil(string? nombre, SexoUsuario sexo, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var nombreNormalizado = (nombre ?? string.Empty).Trim();
        if (nombreNormalizado.Length == 0)
        {
            return Resultado.Fallo(Error.Validacion("usuario.nombre_vacio", "El nombre es obligatorio."));
        }

        if (nombreNormalizado.Length > LongitudMaximaNombre)
        {
            return Resultado.Fallo(Error.Validacion("usuario.nombre_largo", "El nombre es demasiado largo."));
        }

        Nombre = nombreNormalizado;
        Sexo = sexo;
        Tocar(reloj);
        return Resultado.Ok();
    }

    /// <summary>Marca el correo como verificado. Idempotente.</summary>
    public void VerificarEmail(IReloj reloj)
    {
        if (EmailVerificado)
        {
            return;
        }

        EmailVerificado = true;
        TokenVerificacionHash = null;
        TokenVerificacionExpira = null;
        Tocar(reloj);
        RegistrarEvento(new EmailUsuarioVerificado(Id, reloj.AhoraUtc));
    }

    /// <summary>Emite un token de verificación de correo (se almacena su hash) con su caducidad.</summary>
    public void EmitirTokenVerificacion(string token, DateTimeOffset expira, IReloj reloj)
    {
        TokenVerificacionHash = TokenCuenta.Hash(token);
        TokenVerificacionExpira = expira;
        Tocar(reloj);
    }

    /// <summary>Confirma el correo comprobando el token y su caducidad.</summary>
    public Resultado ConfirmarEmailConToken(string token, IReloj reloj)
    {
        if (EmailVerificado)
        {
            return Resultado.Ok();
        }

        if (TokenVerificacionHash is null || TokenVerificacionHash != TokenCuenta.Hash(token))
        {
            return Resultado.Fallo(Error.Validacion("verificacion.token_invalido", "El enlace de verificación no es válido."));
        }

        if (TokenVerificacionExpira is not null && TokenVerificacionExpira < reloj.AhoraUtc)
        {
            return Resultado.Fallo(Error.Validacion("verificacion.token_caducado", "El enlace de verificación ha caducado."));
        }

        VerificarEmail(reloj);
        return Resultado.Ok();
    }

    /// <summary>Emite un token de restablecimiento de contraseña (se almacena su hash) con su caducidad.</summary>
    public void EmitirTokenRestablecimiento(string token, DateTimeOffset expira, IReloj reloj)
    {
        TokenRestablecimientoHash = TokenCuenta.Hash(token);
        TokenRestablecimientoExpira = expira;
        Tocar(reloj);
    }

    /// <summary>Restablece la contraseña comprobando el token y su caducidad. El token se consume.</summary>
    public Resultado RestablecerConToken(string token, HashContrasena nuevoHash, IReloj reloj)
    {
        if (TokenRestablecimientoHash is null || TokenRestablecimientoHash != TokenCuenta.Hash(token))
        {
            return Resultado.Fallo(Error.Validacion("restablecimiento.token_invalido", "El enlace de restablecimiento no es válido."));
        }

        if (TokenRestablecimientoExpira is not null && TokenRestablecimientoExpira < reloj.AhoraUtc)
        {
            return Resultado.Fallo(Error.Validacion("restablecimiento.token_caducado", "El enlace de restablecimiento ha caducado."));
        }

        TokenRestablecimientoHash = null;
        TokenRestablecimientoExpira = null;
        CambiarContrasena(nuevoHash, reloj);
        return Resultado.Ok();
    }

    /// <summary>Sustituye la contraseña por un nuevo hash.</summary>
    public void CambiarContrasena(HashContrasena nuevoHash, IReloj reloj)
    {
        HashContrasena = nuevoHash;
        Tocar(reloj);
        RegistrarEvento(new ContrasenaUsuarioCambiada(Id, reloj.AhoraUtc));
    }

    /// <summary>Suspende la cuenta. Idempotente.</summary>
    public void Suspender(IReloj reloj)
    {
        if (Estado == EstadoUsuario.Suspendido)
        {
            return;
        }

        Estado = EstadoUsuario.Suspendido;
        Tocar(reloj);
        RegistrarEvento(new UsuarioSuspendido(Id, reloj.AhoraUtc));
    }

    /// <summary>Reactiva una cuenta suspendida. Idempotente.</summary>
    public void Reactivar(IReloj reloj)
    {
        if (Estado == EstadoUsuario.Activo)
        {
            return;
        }

        Estado = EstadoUsuario.Activo;
        Tocar(reloj);
        RegistrarEvento(new UsuarioReactivado(Id, reloj.AhoraUtc));
    }

    private void Tocar(IReloj reloj) => ActualizadoEn = reloj.AhoraUtc;
}
