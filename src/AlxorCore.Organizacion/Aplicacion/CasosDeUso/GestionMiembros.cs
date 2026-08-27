using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Organizacion.Dominio;

namespace AlxorCore.Organizacion.Aplicacion.CasosDeUso;

/// <summary>Vista de una membresía (usuario + rol dentro de una empresa).</summary>
public sealed record MembresiaDto(Guid UsuarioId, string RolCodigo, string RolNombre, string Estado, bool EsVeterinario);

/// <summary>Caso de uso: listar las membresías de una empresa.</summary>
public sealed class ListarMembresias
{
    private readonly IRepositorioMembresias _membresias;

    public ListarMembresias(IRepositorioMembresias membresias) => _membresias = membresias;

    public async Task<IReadOnlyList<MembresiaDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var membresias = await _membresias.ListarPorEmpresaAsync(empresaId, ct).ConfigureAwait(false);
        return membresias
            .Select(m => new MembresiaDto(m.UsuarioId, m.RolCodigo, NombreRol(m.RolCodigo), m.Estado.ToString(), m.EsVeterinario))
            .ToList();
    }

    private static string NombreRol(string codigo)
    {
        var rol = Rol.PorCodigoRol(codigo);
        return rol.EsCorrecto ? rol.Valor.Nombre : codigo;
    }
}

/// <summary>Caso de uso: dar de alta (o reactivar) la membresía de un usuario en una empresa con un rol.</summary>
public sealed class AgregarMembresia
{
    private readonly IRepositorioMembresias _membresias;
    private readonly IUnidadDeTrabajoOrganizacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AgregarMembresia(IRepositorioMembresias membresias, IUnidadDeTrabajoOrganizacion unidadDeTrabajo, IReloj reloj)
    {
        _membresias = membresias;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<MembresiaDto>> EjecutarAsync(Guid empresaId, Guid usuarioId, string? rolCodigo, CancellationToken ct = default)
    {
        var rol = Rol.PorCodigoRol(rolCodigo);
        if (rol.EsFallo)
        {
            return Resultado.Fallo<MembresiaDto>(rol.Error);
        }

        var existente = await _membresias.ObtenerAsync(usuarioId, empresaId, ct).ConfigureAwait(false);
        if (existente is not null)
        {
            if (existente.EstaActiva)
            {
                return Resultado.Fallo<MembresiaDto>(Error.Conflicto("membresia.ya_existe", "El usuario ya es miembro de la empresa."));
            }

            existente.Reactivar();
            existente.CambiarRol(rol.Valor);
            await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
            return Resultado.Ok(new MembresiaDto(usuarioId, rol.Valor.Codigo, rol.Valor.Nombre, existente.Estado.ToString(), existente.EsVeterinario));
        }

        var membresia = Membresia.Crear(usuarioId, empresaId, rol.Valor, _reloj);
        _membresias.Agregar(membresia);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(new MembresiaDto(usuarioId, rol.Valor.Codigo, rol.Valor.Nombre, membresia.Estado.ToString(), membresia.EsVeterinario));
    }
}

/// <summary>Caso de uso: cambiar el rol de un miembro de la empresa.</summary>
public sealed class CambiarRolMembresia
{
    private readonly IRepositorioMembresias _membresias;
    private readonly IUnidadDeTrabajoOrganizacion _unidadDeTrabajo;

    public CambiarRolMembresia(IRepositorioMembresias membresias, IUnidadDeTrabajoOrganizacion unidadDeTrabajo)
    {
        _membresias = membresias;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid usuarioId, string? rolCodigo, CancellationToken ct = default)
    {
        var rol = Rol.PorCodigoRol(rolCodigo);
        if (rol.EsFallo)
        {
            return Resultado.Fallo(rol.Error);
        }

        var membresia = await _membresias.ObtenerAsync(usuarioId, empresaId, ct).ConfigureAwait(false);
        if (membresia is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("membresia.no_encontrada", "El usuario no es miembro de la empresa."));
        }

        membresia.CambiarRol(rol.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: marcar o desmarcar a un miembro como veterinario/a de la empresa.</summary>
public sealed class MarcarVeterinarioMembresia
{
    private readonly IRepositorioMembresias _membresias;
    private readonly IUnidadDeTrabajoOrganizacion _unidadDeTrabajo;

    public MarcarVeterinarioMembresia(IRepositorioMembresias membresias, IUnidadDeTrabajoOrganizacion unidadDeTrabajo)
    {
        _membresias = membresias;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid usuarioId, bool esVeterinario, CancellationToken ct = default)
    {
        var membresia = await _membresias.ObtenerAsync(usuarioId, empresaId, ct).ConfigureAwait(false);
        if (membresia is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("membresia.no_encontrada", "El usuario no es miembro de la empresa."));
        }

        membresia.MarcarVeterinario(esVeterinario);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: revocar el acceso de un miembro a la empresa.</summary>
public sealed class RevocarMembresia
{
    private readonly IRepositorioMembresias _membresias;
    private readonly IUnidadDeTrabajoOrganizacion _unidadDeTrabajo;

    public RevocarMembresia(IRepositorioMembresias membresias, IUnidadDeTrabajoOrganizacion unidadDeTrabajo)
    {
        _membresias = membresias;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid usuarioId, CancellationToken ct = default)
    {
        var membresia = await _membresias.ObtenerAsync(usuarioId, empresaId, ct).ConfigureAwait(false);
        if (membresia is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("membresia.no_encontrada", "El usuario no es miembro de la empresa."));
        }

        membresia.Revocar();
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}
