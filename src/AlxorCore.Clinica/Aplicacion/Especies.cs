using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>
/// Caso de uso: crear una especie en el maestro de la empresa activa. El nombre es único por empresa;
/// si ya existe se devuelve <c>especie.duplicada</c>.
/// </summary>
public sealed class CrearEspecie
{
    private readonly IRepositorioEspecies _especies;
    private readonly IConsultaEspecies _consulta;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearEspecie(IRepositorioEspecies especies, IConsultaEspecies consulta, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _especies = especies;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<EspecieDto>> EjecutarAsync(Guid empresaId, DatosEspecie datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var especie = Especie.Crear(empresaId, datos.Nombre, datos.MesesCachorro, _reloj, datos.Emoji);
        if (especie.EsFallo)
        {
            return Resultado.Fallo<EspecieDto>(especie.Error);
        }

        if (await _consulta.ExisteNombreAsync(empresaId, datos.Nombre.Trim(), null, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<EspecieDto>(Error.Conflicto("especie.duplicada", "Ya existe una especie con ese nombre en esta empresa."));
        }

        _especies.Agregar(especie.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(EspecieDto.Desde(especie.Valor));
    }
}

/// <summary>Caso de uso: actualizar una especie del maestro (nombre y umbral de cachorro).</summary>
public sealed class ActualizarEspecie
{
    private readonly IRepositorioEspecies _especies;
    private readonly IConsultaEspecies _consulta;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarEspecie(IRepositorioEspecies especies, IConsultaEspecies consulta, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _especies = especies;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<EspecieDto>> EjecutarAsync(Guid especieId, DatosEspecie datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var especie = await _especies.ObtenerPorIdAsync(especieId, ct).ConfigureAwait(false);
        if (especie is null)
        {
            return Resultado.Fallo<EspecieDto>(Error.NoEncontrado("especie.no_encontrada", "La especie no existe."));
        }

        if (await _consulta.ExisteNombreAsync(especie.EmpresaId, datos.Nombre.Trim(), especieId, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<EspecieDto>(Error.Conflicto("especie.duplicada", "Ya existe una especie con ese nombre en esta empresa."));
        }

        var actualizada = especie.Actualizar(datos.Nombre, datos.MesesCachorro, _reloj, datos.Emoji);
        if (actualizada.EsFallo)
        {
            return Resultado.Fallo<EspecieDto>(actualizada.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(EspecieDto.Desde(especie));
    }
}

/// <summary>Caso de uso: dar de baja (baja lógica) una especie del maestro.</summary>
public sealed class DesactivarEspecie
{
    private readonly IRepositorioEspecies _especies;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public DesactivarEspecie(IRepositorioEspecies especies, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _especies = especies;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid especieId, CancellationToken ct = default)
    {
        var especie = await _especies.ObtenerPorIdAsync(especieId, ct).ConfigureAwait(false);
        if (especie is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("especie.no_encontrada", "La especie no existe."));
        }

        especie.Desactivar(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: obtener una especie del maestro por su identificador.</summary>
public sealed class ObtenerEspecie
{
    private readonly IConsultaEspecies _consulta;

    public ObtenerEspecie(IConsultaEspecies consulta) => _consulta = consulta;

    public async Task<Resultado<EspecieDto>> EjecutarAsync(Guid especieId, CancellationToken ct = default)
    {
        var especie = await _consulta.ObtenerAsync(especieId, ct).ConfigureAwait(false);
        return especie is null
            ? Resultado.Fallo<EspecieDto>(Error.NoEncontrado("especie.no_encontrada", "La especie no existe."))
            : Resultado.Ok(especie);
    }
}

/// <summary>Caso de uso: listar las especies del maestro de la empresa activa (activas por defecto).</summary>
public sealed class ListarEspecies
{
    private readonly IConsultaEspecies _consulta;

    public ListarEspecies(IConsultaEspecies consulta) => _consulta = consulta;

    public Task<IReadOnlyList<EspecieDto>> EjecutarAsync(Guid empresaId, bool incluirInactivas = false, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, incluirInactivas, ct);
}

/// <summary>Resultado de la siembra del maestro de especies: cuántas se crearon y cuántas ya existían.</summary>
public sealed record SiembraEspeciesResultado(int Creadas, int YaExistentes);

/// <summary>
/// Caso de uso: sembrar en la empresa activa las especies por defecto (Perro, Gato, Conejo, Ave,
/// Hurón, Reptil, Otro con sus umbrales de cachorro). Es <b>idempotente</b>: no duplica las que ya
/// existan (por nombre). Se ejecuta al crear la empresa y desde el asistente/arranque.
/// </summary>
public sealed class SembrarEspeciesPorDefecto
{
    private readonly IRepositorioEspecies _especies;
    private readonly IConsultaEspecies _consulta;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public SembrarEspeciesPorDefecto(IRepositorioEspecies especies, IConsultaEspecies consulta, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _especies = especies;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<SiembraEspeciesResultado>> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var creadas = 0;
        var yaExistentes = 0;

        foreach (var (nombre, meses, emoji) in EspeciesPorDefecto.Todas)
        {
            if (await _consulta.ExisteNombreAsync(empresaId, nombre, null, ct).ConfigureAwait(false))
            {
                yaExistentes++;
                continue;
            }

            var especie = Especie.Crear(empresaId, nombre, meses, _reloj, emoji);
            if (especie.EsFallo)
            {
                return Resultado.Fallo<SiembraEspeciesResultado>(especie.Error);
            }

            _especies.Agregar(especie.Valor);
            creadas++;
        }

        if (creadas > 0)
        {
            await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        }

        return Resultado.Ok(new SiembraEspeciesResultado(creadas, yaExistentes));
    }
}

/// <summary>
/// Especies por defecto del producto veterinario (maestro editable). Coinciden con los valores del
/// antiguo enumerado fijo para no dejar huérfano ningún dato existente: Conejo se considera cachorro
/// hasta los 6 meses; el resto, hasta los 12.
/// </summary>
public static class EspeciesPorDefecto
{
    /// <summary>Lista de (nombre, meses de cachorro, emoji) sembrada en cada empresa nueva.</summary>
    public static readonly IReadOnlyList<(string Nombre, int MesesCachorro, string Emoji)> Todas = new (string, int, string)[]
    {
        ("Perro", 12, "🐕"),
        ("Gato", 12, "🐈"),
        ("Conejo", 6, "🐇"),
        ("Ave", 12, "🦜"),
        ("Huron", 12, "🦡"),
        ("Reptil", 12, "🦎"),
        ("Otro", 12, "🐾"),
    };
}
