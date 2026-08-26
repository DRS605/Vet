using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>Vista de un adjunto (sin el contenido binario), para listar la galería de la ficha.</summary>
public sealed record AdjuntoDto(Guid Id, Guid AnimalId, string NombreArchivo, string TipoMime, int Tamano, bool EsImagen, DateTimeOffset CreadoEn)
{
    public static AdjuntoDto Desde(Adjunto a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new AdjuntoDto(a.Id, a.AnimalId, a.NombreArchivo, a.TipoMime, a.Tamano, a.EsImagen, a.CreadoEn);
    }
}

/// <summary>Contenido descargable de un adjunto.</summary>
public sealed record ContenidoAdjunto(byte[] Datos, string TipoMime, string NombreArchivo);

/// <summary>Datos para subir un adjunto.</summary>
public sealed record DatosAdjunto(string? NombreArchivo, string? TipoMime, byte[]? Datos);

/// <summary>Repositorio de adjuntos (escritura).</summary>
public interface IRepositorioAdjuntos
{
    Task<Adjunto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Adjunto adjunto);

    void Eliminar(Adjunto adjunto);
}

/// <summary>Consultas de lectura de adjuntos.</summary>
public interface IConsultaAdjuntos
{
    Task<IReadOnlyList<AdjuntoDto>> ListarPorAnimalAsync(Guid animalId, CancellationToken ct = default);

    /// <summary>Contenido binario de un adjunto para descargarlo, o <c>null</c> si no existe.</summary>
    Task<ContenidoAdjunto?> ObtenerContenidoAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Caso de uso: subir un adjunto a la ficha de un animal.</summary>
public sealed class SubirAdjunto
{
    private readonly IRepositorioAdjuntos _adjuntos;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public SubirAdjunto(IRepositorioAdjuntos adjuntos, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _adjuntos = adjuntos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<AdjuntoDto>> EjecutarAsync(Guid empresaId, Guid animalId, DatosAdjunto datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var adjunto = Adjunto.Crear(empresaId, animalId, datos.NombreArchivo, datos.TipoMime, datos.Datos, _reloj);
        if (adjunto.EsFallo)
        {
            return Resultado.Fallo<AdjuntoDto>(adjunto.Error);
        }

        _adjuntos.Agregar(adjunto.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(AdjuntoDto.Desde(adjunto.Valor));
    }
}

/// <summary>Caso de uso: listar los adjuntos de un animal.</summary>
public sealed class ListarAdjuntosDeAnimal
{
    private readonly IConsultaAdjuntos _consulta;

    public ListarAdjuntosDeAnimal(IConsultaAdjuntos consulta) => _consulta = consulta;

    public Task<IReadOnlyList<AdjuntoDto>> EjecutarAsync(Guid animalId, CancellationToken ct = default) =>
        _consulta.ListarPorAnimalAsync(animalId, ct);
}

/// <summary>Caso de uso: obtener el contenido de un adjunto para descargarlo.</summary>
public sealed class DescargarAdjunto
{
    private readonly IConsultaAdjuntos _consulta;

    public DescargarAdjunto(IConsultaAdjuntos consulta) => _consulta = consulta;

    public Task<ContenidoAdjunto?> EjecutarAsync(Guid id, CancellationToken ct = default) =>
        _consulta.ObtenerContenidoAsync(id, ct);
}

/// <summary>Caso de uso: eliminar un adjunto.</summary>
public sealed class EliminarAdjunto
{
    private readonly IRepositorioAdjuntos _adjuntos;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;

    public EliminarAdjunto(IRepositorioAdjuntos adjuntos, IUnidadDeTrabajoClinica unidadDeTrabajo)
    {
        _adjuntos = adjuntos;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken ct = default)
    {
        var adjunto = await _adjuntos.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (adjunto is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("adjunto.no_encontrado", "El adjunto no existe."));
        }

        _adjuntos.Eliminar(adjunto);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}
