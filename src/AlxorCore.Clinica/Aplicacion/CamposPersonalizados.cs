using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>
/// Caso de uso: crear un campo personalizado en el maestro de la empresa activa. La clave (derivada de
/// la etiqueta) es única por empresa y entidad; si ya existe se devuelve <c>campo.duplicado</c>.
/// </summary>
public sealed class CrearCampoPersonalizado
{
    private readonly IRepositorioCamposPersonalizados _campos;
    private readonly IConsultaCamposPersonalizados _consulta;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearCampoPersonalizado(IRepositorioCamposPersonalizados campos, IConsultaCamposPersonalizados consulta, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _campos = campos;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CampoPersonalizadoDto>> EjecutarAsync(Guid empresaId, DatosCampoPersonalizado datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var campo = CampoPersonalizado.Crear(
            empresaId, datos.Entidad, datos.Etiqueta, datos.Tipo, datos.Opciones, datos.Obligatorio, datos.Orden, _reloj);
        if (campo.EsFallo)
        {
            return Resultado.Fallo<CampoPersonalizadoDto>(campo.Error);
        }

        if (await _consulta.ExisteClaveAsync(empresaId, datos.Entidad, campo.Valor.Clave, null, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<CampoPersonalizadoDto>(Error.Conflicto("campo.duplicado", "Ya existe un campo con ese nombre para esta ficha."));
        }

        _campos.Agregar(campo.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(CampoPersonalizadoDto.Desde(campo.Valor));
    }
}

/// <summary>Caso de uso: actualizar la definición de un campo personalizado.</summary>
public sealed class ActualizarCampoPersonalizado
{
    private readonly IRepositorioCamposPersonalizados _campos;
    private readonly IConsultaCamposPersonalizados _consulta;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarCampoPersonalizado(IRepositorioCamposPersonalizados campos, IConsultaCamposPersonalizados consulta, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _campos = campos;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CampoPersonalizadoDto>> EjecutarAsync(Guid campoId, DatosCampoPersonalizado datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var campo = await _campos.ObtenerPorIdAsync(campoId, ct).ConfigureAwait(false);
        if (campo is null)
        {
            return Resultado.Fallo<CampoPersonalizadoDto>(Error.NoEncontrado("campo.no_encontrado", "El campo personalizado no existe."));
        }

        var actualizado = campo.Actualizar(datos.Etiqueta, datos.Tipo, datos.Opciones, datos.Obligatorio, datos.Orden, _reloj);
        if (actualizado.EsFallo)
        {
            return Resultado.Fallo<CampoPersonalizadoDto>(actualizado.Error);
        }

        if (await _consulta.ExisteClaveAsync(campo.EmpresaId, campo.Entidad, campo.Clave, campoId, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<CampoPersonalizadoDto>(Error.Conflicto("campo.duplicado", "Ya existe un campo con ese nombre para esta ficha."));
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(CampoPersonalizadoDto.Desde(campo));
    }
}

/// <summary>Caso de uso: dar de baja (baja lógica) un campo personalizado. Sus valores no se borran.</summary>
public sealed class DesactivarCampoPersonalizado
{
    private readonly IRepositorioCamposPersonalizados _campos;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public DesactivarCampoPersonalizado(IRepositorioCamposPersonalizados campos, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _campos = campos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid campoId, CancellationToken ct = default)
    {
        var campo = await _campos.ObtenerPorIdAsync(campoId, ct).ConfigureAwait(false);
        if (campo is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("campo.no_encontrado", "El campo personalizado no existe."));
        }

        campo.Desactivar(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: listar los campos personalizados de una entidad (activos por defecto).</summary>
public sealed class ListarCamposPersonalizados
{
    private readonly IConsultaCamposPersonalizados _consulta;

    public ListarCamposPersonalizados(IConsultaCamposPersonalizados consulta) => _consulta = consulta;

    public Task<IReadOnlyList<CampoPersonalizadoDto>> EjecutarAsync(Guid empresaId, EntidadPersonalizable entidad, bool incluirInactivos = false, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, entidad, incluirInactivos, ct);
}

/// <summary>Caso de uso: obtener los campos personalizados de una ficha con su valor actual.</summary>
public sealed class ObtenerCamposDeRegistro
{
    private readonly IConsultaValoresCampos _consulta;

    public ObtenerCamposDeRegistro(IConsultaValoresCampos consulta) => _consulta = consulta;

    public Task<IReadOnlyList<ValorCampoDto>> EjecutarAsync(Guid empresaId, EntidadPersonalizable entidad, Guid registroId, CancellationToken ct = default) =>
        _consulta.ObtenerParaRegistroAsync(empresaId, entidad, registroId, ct);
}

/// <summary>
/// Caso de uso: guardar de una vez los valores de los campos personalizados de una ficha. Recorre las
/// definiciones activas de la entidad (así valida los obligatorios aunque el cliente no los envíe),
/// normaliza cada valor según su tipo, y crea/actualiza/borra el valor almacenado. Es transaccional:
/// si algún valor es inválido no se guarda ninguno.
/// </summary>
public sealed class GuardarCamposDeRegistro
{
    private readonly IConsultaCamposPersonalizados _definiciones;
    private readonly IRepositorioCamposPersonalizados _campos;
    private readonly IRepositorioValoresCampos _valores;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public GuardarCamposDeRegistro(
        IConsultaCamposPersonalizados definiciones,
        IRepositorioCamposPersonalizados campos,
        IRepositorioValoresCampos valores,
        IUnidadDeTrabajoClinica unidadDeTrabajo,
        IReloj reloj)
    {
        _definiciones = definiciones;
        _campos = campos;
        _valores = valores;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, EntidadPersonalizable entidad, Guid registroId, IReadOnlyCollection<DatosValorCampo> valores, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(valores);

        var definiciones = await _definiciones.ListarAsync(empresaId, entidad, false, ct).ConfigureAwait(false);
        if (definiciones.Count == 0)
        {
            return Resultado.Ok();
        }

        var entrantes = new Dictionary<Guid, string?>();
        foreach (var v in valores)
        {
            entrantes[v.CampoId] = v.Valor;
        }

        var existentes = (await _valores.ListarPorRegistroAsync(registroId, ct).ConfigureAwait(false))
            .Where(v => v.Entidad == entidad)
            .ToDictionary(v => v.CampoId);

        // Primera pasada: valida y normaliza todo antes de tocar la base (todo o nada).
        var normalizados = new Dictionary<Guid, string?>();
        foreach (var dto in definiciones)
        {
            var campo = await _campos.ObtenerPorIdAsync(dto.Id, ct).ConfigureAwait(false);
            if (campo is null)
            {
                continue;
            }

            entrantes.TryGetValue(campo.Id, out var bruto);
            var normalizado = campo.NormalizarValor(bruto);
            if (normalizado.EsFallo)
            {
                return Resultado.Fallo(normalizado.Error);
            }

            normalizados[campo.Id] = normalizado.Valor;
        }

        // Segunda pasada: aplica los cambios.
        foreach (var (campoId, valor) in normalizados)
        {
            existentes.TryGetValue(campoId, out var actual);
            if (valor is null)
            {
                if (actual is not null)
                {
                    _valores.Eliminar(actual);
                }

                continue;
            }

            if (actual is null)
            {
                _valores.Agregar(ValorCampoPersonalizado.Crear(empresaId, campoId, entidad, registroId, valor, _reloj));
            }
            else
            {
                actual.Establecer(valor, _reloj);
            }
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}
