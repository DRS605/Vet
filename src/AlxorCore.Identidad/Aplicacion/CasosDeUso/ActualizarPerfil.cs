using AlxorCore.Identidad.Aplicacion.Modelos;
using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Aplicacion.CasosDeUso;

/// <summary>Datos para actualizar el perfil propio: nombre visible y sexo.</summary>
public sealed record ActualizarPerfilComando(string Nombre, SexoUsuario Sexo);

/// <summary>Caso de uso: el usuario autenticado edita su propio perfil (nombre y sexo).</summary>
public sealed class ActualizarPerfil
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IUnidadDeTrabajoIdentidad _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarPerfil(IRepositorioUsuarios usuarios, IUnidadDeTrabajoIdentidad unidadDeTrabajo, IReloj reloj)
    {
        _usuarios = usuarios;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<PerfilUsuario>> EjecutarAsync(Guid usuarioId, ActualizarPerfilComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, ct).ConfigureAwait(false);
        if (usuario is null)
        {
            return Resultado.Fallo<PerfilUsuario>(Error.NoEncontrado("usuario.no_encontrado", "El usuario no existe."));
        }

        var resultado = usuario.ActualizarPerfil(comando.Nombre, comando.Sexo, _reloj);
        if (resultado.EsFallo)
        {
            return Resultado.Fallo<PerfilUsuario>(resultado.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(PerfilUsuario.Desde(usuario));
    }
}
