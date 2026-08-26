using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>Vista de un artículo de inventario (incluye el aviso de stock bajo).</summary>
public sealed record ArticuloInventarioDto(
    Guid Id, string Nombre, CategoriaInventario Categoria, string? Unidad,
    decimal Stock, decimal StockMinimo, DateOnly? Caducidad, string? Notas, bool Activo, bool BajoStock)
{
    public static ArticuloInventarioDto Desde(ArticuloInventario a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new ArticuloInventarioDto(
            a.Id, a.Nombre, a.Categoria, a.Unidad, a.Stock, a.StockMinimo, a.Caducidad, a.Notas, a.Activo, a.BajoStock);
    }
}

/// <summary>Datos para crear o actualizar un artículo de inventario.</summary>
public sealed record DatosArticuloInventario(
    string Nombre, CategoriaInventario Categoria = CategoriaInventario.Medicamento, string? Unidad = null,
    decimal Stock = 0m, decimal StockMinimo = 0m, DateOnly? Caducidad = null, string? Notas = null);

/// <summary>Ajuste de stock (delta: positivo entrada, negativo salida).</summary>
public sealed record DatosAjusteStock(decimal Delta);

/// <summary>Repositorio de inventario (escritura).</summary>
public interface IRepositorioInventario
{
    Task<ArticuloInventario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(ArticuloInventario articulo);
}

/// <summary>Consultas de lectura del inventario.</summary>
public interface IConsultaInventario
{
    Task<IReadOnlyList<ArticuloInventarioDto>> ListarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default);
}

/// <summary>Caso de uso: crear un artículo de inventario.</summary>
public sealed class CrearArticuloInventario
{
    private readonly IRepositorioInventario _inv;
    private readonly IUnidadDeTrabajoClinica _uow;
    private readonly IReloj _reloj;

    public CrearArticuloInventario(IRepositorioInventario inv, IUnidadDeTrabajoClinica uow, IReloj reloj)
    {
        _inv = inv; _uow = uow; _reloj = reloj;
    }

    public async Task<Resultado<ArticuloInventarioDto>> EjecutarAsync(Guid empresaId, DatosArticuloInventario datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var a = ArticuloInventario.Crear(empresaId, datos.Nombre, datos.Categoria, datos.Unidad, datos.Stock, datos.StockMinimo, datos.Caducidad, datos.Notas, _reloj);
        if (a.EsFallo)
        {
            return Resultado.Fallo<ArticuloInventarioDto>(a.Error);
        }

        _inv.Agregar(a.Valor);
        await _uow.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ArticuloInventarioDto.Desde(a.Valor));
    }
}

/// <summary>Caso de uso: actualizar un artículo de inventario.</summary>
public sealed class ActualizarArticuloInventario
{
    private readonly IRepositorioInventario _inv;
    private readonly IUnidadDeTrabajoClinica _uow;
    private readonly IReloj _reloj;

    public ActualizarArticuloInventario(IRepositorioInventario inv, IUnidadDeTrabajoClinica uow, IReloj reloj)
    {
        _inv = inv; _uow = uow; _reloj = reloj;
    }

    public async Task<Resultado<ArticuloInventarioDto>> EjecutarAsync(Guid id, DatosArticuloInventario datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var a = await _inv.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (a is null)
        {
            return Resultado.Fallo<ArticuloInventarioDto>(Error.NoEncontrado("inventario.no_encontrado", "El artículo no existe."));
        }

        var r = a.Actualizar(datos.Nombre, datos.Categoria, datos.Unidad, datos.Stock, datos.StockMinimo, datos.Caducidad, datos.Notas, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<ArticuloInventarioDto>(r.Error);
        }

        await _uow.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ArticuloInventarioDto.Desde(a));
    }
}

/// <summary>Caso de uso: ajustar el stock de un artículo (entrada/salida).</summary>
public sealed class AjustarStockArticulo
{
    private readonly IRepositorioInventario _inv;
    private readonly IUnidadDeTrabajoClinica _uow;
    private readonly IReloj _reloj;

    public AjustarStockArticulo(IRepositorioInventario inv, IUnidadDeTrabajoClinica uow, IReloj reloj)
    {
        _inv = inv; _uow = uow; _reloj = reloj;
    }

    public async Task<Resultado<ArticuloInventarioDto>> EjecutarAsync(Guid id, decimal delta, CancellationToken ct = default)
    {
        var a = await _inv.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (a is null)
        {
            return Resultado.Fallo<ArticuloInventarioDto>(Error.NoEncontrado("inventario.no_encontrado", "El artículo no existe."));
        }

        var r = a.AjustarStock(delta, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<ArticuloInventarioDto>(r.Error);
        }

        await _uow.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ArticuloInventarioDto.Desde(a));
    }
}

/// <summary>Caso de uso: dar de baja un artículo de inventario.</summary>
public sealed class DesactivarArticuloInventario
{
    private readonly IRepositorioInventario _inv;
    private readonly IUnidadDeTrabajoClinica _uow;
    private readonly IReloj _reloj;

    public DesactivarArticuloInventario(IRepositorioInventario inv, IUnidadDeTrabajoClinica uow, IReloj reloj)
    {
        _inv = inv; _uow = uow; _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken ct = default)
    {
        var a = await _inv.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (a is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("inventario.no_encontrado", "El artículo no existe."));
        }

        a.Desactivar(_reloj);
        await _uow.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: listar el inventario de la empresa.</summary>
public sealed class ListarInventario
{
    private readonly IConsultaInventario _consulta;

    public ListarInventario(IConsultaInventario consulta) => _consulta = consulta;

    public Task<IReadOnlyList<ArticuloInventarioDto>> EjecutarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, incluirInactivos, ct);
}
