using AlxorCore.Clinica.Dominio;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>Vista de la definición de un campo personalizado del maestro de la empresa.</summary>
public sealed record CampoPersonalizadoDto(
    Guid Id,
    EntidadPersonalizable Entidad,
    string Etiqueta,
    string Clave,
    TipoCampo Tipo,
    IReadOnlyList<string> Opciones,
    bool Obligatorio,
    int Orden,
    bool Activo)
{
    public static CampoPersonalizadoDto Desde(CampoPersonalizado c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new CampoPersonalizadoDto(
            c.Id, c.Entidad, c.Etiqueta, c.Clave, c.Tipo, c.OpcionesLista, c.Obligatorio, c.Orden, c.Activo);
    }
}

/// <summary>Datos para crear o actualizar la definición de un campo personalizado.</summary>
public sealed record DatosCampoPersonalizado(
    EntidadPersonalizable Entidad,
    string Etiqueta,
    TipoCampo Tipo,
    string? Opciones = null,
    bool Obligatorio = false,
    int Orden = 0);

/// <summary>
/// Definición de un campo personalizado junto con su valor para una ficha concreta. Es lo que consume
/// el formulario: sabe cómo pintar el campo (tipo, opciones) y qué valor tiene ese cliente/animal.
/// </summary>
public sealed record ValorCampoDto(
    Guid CampoId,
    string Etiqueta,
    string Clave,
    TipoCampo Tipo,
    IReadOnlyList<string> Opciones,
    bool Obligatorio,
    int Orden,
    string? Valor);

/// <summary>Valor entrante de un campo personalizado al guardar una ficha.</summary>
public sealed record DatosValorCampo(Guid CampoId, string? Valor);

/// <summary>Repositorio de definiciones de campos personalizados (escritura).</summary>
public interface IRepositorioCamposPersonalizados
{
    Task<CampoPersonalizado?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(CampoPersonalizado campo);
}

/// <summary>Consultas de lectura del maestro de campos personalizados.</summary>
public interface IConsultaCamposPersonalizados
{
    Task<CampoPersonalizadoDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CampoPersonalizadoDto>> ListarAsync(Guid empresaId, EntidadPersonalizable entidad, bool incluirInactivos = false, CancellationToken ct = default);

    /// <summary>¿Existe ya un campo con esa clave para la entidad en la empresa? Opcionalmente excluye un id (al actualizar).</summary>
    Task<bool> ExisteClaveAsync(Guid empresaId, EntidadPersonalizable entidad, string clave, Guid? excluirId = null, CancellationToken ct = default);
}

/// <summary>Repositorio de valores de campos personalizados (escritura).</summary>
public interface IRepositorioValoresCampos
{
    Task<IReadOnlyList<ValorCampoPersonalizado>> ListarPorRegistroAsync(Guid registroId, CancellationToken ct = default);

    void Agregar(ValorCampoPersonalizado valor);

    void Eliminar(ValorCampoPersonalizado valor);
}

/// <summary>Consultas de lectura de los valores de campos personalizados de una ficha.</summary>
public interface IConsultaValoresCampos
{
    /// <summary>Definiciones activas de la entidad con el valor actual de la ficha (o nulo), ordenadas por orden.</summary>
    Task<IReadOnlyList<ValorCampoDto>> ObtenerParaRegistroAsync(Guid empresaId, EntidadPersonalizable entidad, Guid registroId, CancellationToken ct = default);
}
