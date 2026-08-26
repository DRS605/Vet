using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Aplicacion.CasosDeUso;

/// <summary>Datos para que el usuario cambie su propia contraseña.</summary>
public sealed record CambiarContrasenaComando(string ClaveActual, string NuevaClave);

/// <summary>
/// Caso de uso: el usuario autenticado cambia su contraseña. Comprueba la clave actual y valida la
/// nueva con las mismas reglas del alta antes de sustituir el hash.
/// </summary>
public sealed class CambiarContrasena
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IHasherContrasena _hasher;
    private readonly IUnidadDeTrabajoIdentidad _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CambiarContrasena(
        IRepositorioUsuarios usuarios,
        IHasherContrasena hasher,
        IUnidadDeTrabajoIdentidad unidadDeTrabajo,
        IReloj reloj)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid usuarioId, CambiarContrasenaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, ct).ConfigureAwait(false);
        if (usuario is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("usuario.no_encontrado", "El usuario no existe."));
        }

        if (!_hasher.Verificar(usuario.HashContrasena.Valor, comando.ClaveActual ?? string.Empty))
        {
            return Resultado.Fallo(Error.Validacion("contrasena.actual_incorrecta", "La contraseña actual no es correcta."));
        }

        var errorContrasena = ValidarContrasena(comando.NuevaClave);
        if (errorContrasena is not null)
        {
            return Resultado.Fallo(errorContrasena);
        }

        var hash = HashContrasena.DesdeHash(_hasher.Hash(comando.NuevaClave));
        usuario.CambiarContrasena(hash, _reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }

    private static Error? ValidarContrasena(string? contrasena)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < RegistrarUsuario.LongitudMinimaContrasena)
        {
            return Error.Validacion(
                "contrasena.corta",
                $"La contraseña debe tener al menos {RegistrarUsuario.LongitudMinimaContrasena} caracteres.");
        }

        if (contrasena.Length > RegistrarUsuario.LongitudMaximaContrasena)
        {
            return Error.Validacion("contrasena.larga", "La contraseña es demasiado larga.");
        }

        return null;
    }
}
