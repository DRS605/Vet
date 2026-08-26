using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>Vista de una raza del maestro editable de la empresa.</summary>
public sealed record RazaDto(Guid Id, string Especie, string Nombre, bool Activo)
{
    public static RazaDto Desde(Raza r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new RazaDto(r.Id, r.Especie, r.Nombre, r.Activo);
    }
}

/// <summary>Datos para crear una raza (especie + nombre) o actualizarla (solo el nombre cuenta).</summary>
public sealed record DatosRaza(string Especie, string Nombre);

/// <summary>Repositorio de razas (escritura).</summary>
public interface IRepositorioRazas
{
    Task<Raza?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Raza raza);
}

/// <summary>Consultas de lectura del maestro de razas.</summary>
public interface IConsultaRazas
{
    Task<RazaDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lista las razas de la empresa; si se indica especie, solo las de esa especie.</summary>
    Task<IReadOnlyList<RazaDto>> ListarAsync(Guid empresaId, string? especie = null, bool incluirInactivas = false, CancellationToken ct = default);

    /// <summary>¿Existe ya una raza con ese nombre para la especie en la empresa? Opcionalmente excluye un id (al actualizar).</summary>
    Task<bool> ExisteNombreAsync(Guid empresaId, string especie, string nombre, Guid? excluirId = null, CancellationToken ct = default);
}

/// <summary>Caso de uso: crear una raza en el maestro (única por empresa y especie).</summary>
public sealed class CrearRaza
{
    private readonly IRepositorioRazas _razas;
    private readonly IConsultaRazas _consulta;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearRaza(IRepositorioRazas razas, IConsultaRazas consulta, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _razas = razas;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<RazaDto>> EjecutarAsync(Guid empresaId, DatosRaza datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var raza = Raza.Crear(empresaId, datos.Especie, datos.Nombre, _reloj);
        if (raza.EsFallo)
        {
            return Resultado.Fallo<RazaDto>(raza.Error);
        }

        if (await _consulta.ExisteNombreAsync(empresaId, raza.Valor.Especie, raza.Valor.Nombre, null, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<RazaDto>(Error.Conflicto("raza.duplicada", "Ya existe una raza con ese nombre para esa especie."));
        }

        _razas.Agregar(raza.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(RazaDto.Desde(raza.Valor));
    }
}

/// <summary>Caso de uso: actualizar el nombre de una raza.</summary>
public sealed class ActualizarRaza
{
    private readonly IRepositorioRazas _razas;
    private readonly IConsultaRazas _consulta;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarRaza(IRepositorioRazas razas, IConsultaRazas consulta, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _razas = razas;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<RazaDto>> EjecutarAsync(Guid razaId, DatosRaza datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var raza = await _razas.ObtenerPorIdAsync(razaId, ct).ConfigureAwait(false);
        if (raza is null)
        {
            return Resultado.Fallo<RazaDto>(Error.NoEncontrado("raza.no_encontrada", "La raza no existe."));
        }

        var actualizada = raza.Actualizar(datos.Nombre, _reloj);
        if (actualizada.EsFallo)
        {
            return Resultado.Fallo<RazaDto>(actualizada.Error);
        }

        if (await _consulta.ExisteNombreAsync(raza.EmpresaId, raza.Especie, raza.Nombre, razaId, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<RazaDto>(Error.Conflicto("raza.duplicada", "Ya existe una raza con ese nombre para esa especie."));
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(RazaDto.Desde(raza));
    }
}

/// <summary>Caso de uso: dar de baja (baja lógica) una raza del maestro.</summary>
public sealed class DesactivarRaza
{
    private readonly IRepositorioRazas _razas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public DesactivarRaza(IRepositorioRazas razas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _razas = razas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid razaId, CancellationToken ct = default)
    {
        var raza = await _razas.ObtenerPorIdAsync(razaId, ct).ConfigureAwait(false);
        if (raza is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("raza.no_encontrada", "La raza no existe."));
        }

        raza.Desactivar(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: listar las razas del maestro (todas o filtradas por especie).</summary>
public sealed class ListarRazas
{
    private readonly IConsultaRazas _consulta;

    public ListarRazas(IConsultaRazas consulta) => _consulta = consulta;

    public Task<IReadOnlyList<RazaDto>> EjecutarAsync(Guid empresaId, string? especie = null, bool incluirInactivas = false, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, especie, incluirInactivas, ct);
}
