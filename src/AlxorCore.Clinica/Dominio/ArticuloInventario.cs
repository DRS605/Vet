using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>Se ha creado un artículo de inventario.</summary>
public sealed record ArticuloInventarioCreado(Guid ArticuloId, Guid EmpresaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Categoría de un artículo de inventario.</summary>
public enum CategoriaInventario
{
    /// <summary>Medicamento.</summary>
    Medicamento,

    /// <summary>Vacuna.</summary>
    Vacuna,

    /// <summary>Material fungible o quirúrgico.</summary>
    Material,

    /// <summary>Alimento / dietético.</summary>
    Alimento,

    /// <summary>Otro.</summary>
    Otro,
}

/// <summary>
/// Artículo del inventario de la clínica (medicamentos, vacunas, material…). Lleva el stock actual, un
/// stock mínimo para avisar de reposición y una caducidad opcional para avisar de vencimiento.
/// </summary>
public sealed class ArticuloInventario : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 120;
    public const int LongitudMaximaUnidad = 20;
    public const int LongitudMaximaNotas = 500;

    private ArticuloInventario(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private ArticuloInventario(
        Guid id, Guid empresaId, string nombre, CategoriaInventario categoria, string? unidad,
        decimal stock, decimal stockMinimo, DateOnly? caducidad, string? notas, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Nombre = nombre;
        Categoria = categoria;
        Unidad = unidad;
        Stock = stock;
        StockMinimo = stockMinimo;
        Caducidad = caducidad;
        Notas = notas;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    public string Nombre { get; private set; }

    public CategoriaInventario Categoria { get; private set; }

    /// <summary>Unidad de medida (uds, ml, comprimidos…). Opcional.</summary>
    public string? Unidad { get; private set; }

    /// <summary>Existencias actuales.</summary>
    public decimal Stock { get; private set; }

    /// <summary>Stock mínimo por debajo del cual se avisa de reposición (0 = sin aviso).</summary>
    public decimal StockMinimo { get; private set; }

    /// <summary>Fecha de caducidad del lote en stock. Opcional.</summary>
    public DateOnly? Caducidad { get; private set; }

    public string? Notas { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>¿Está en o por debajo del stock mínimo? Solo cuando hay un mínimo definido (&gt; 0).</summary>
    public bool BajoStock => StockMinimo > 0 && Stock <= StockMinimo;

    public static Resultado<ArticuloInventario> Crear(
        Guid empresaId, string? nombre, CategoriaInventario categoria, string? unidad,
        decimal stock, decimal stockMinimo, DateOnly? caducidad, string? notas, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, categoria, unidad, stock, stockMinimo, notas);
        if (error is not null)
        {
            return Resultado.Fallo<ArticuloInventario>(error);
        }

        var articulo = new ArticuloInventario(
            Guid.NewGuid(), empresaId, nombre!.Trim(), categoria, Normalizar(unidad),
            stock, stockMinimo, caducidad, Normalizar(notas), reloj.AhoraUtc);
        articulo.RegistrarEvento(new ArticuloInventarioCreado(articulo.Id, empresaId, reloj.AhoraUtc));
        return Resultado.Ok(articulo);
    }

    public Resultado Actualizar(
        string? nombre, CategoriaInventario categoria, string? unidad,
        decimal stock, decimal stockMinimo, DateOnly? caducidad, string? notas, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, categoria, unidad, stock, stockMinimo, notas);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Nombre = nombre!.Trim();
        Categoria = categoria;
        Unidad = Normalizar(unidad);
        Stock = stock;
        StockMinimo = stockMinimo;
        Caducidad = caducidad;
        Notas = Normalizar(notas);
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Ajusta el stock sumando <paramref name="delta"/> (negativo para salidas). No baja de cero.</summary>
    public Resultado AjustarStock(decimal delta, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        var nuevo = Stock + delta;
        if (nuevo < 0)
        {
            return Resultado.Fallo(Error.Validacion("inventario.stock_negativo", "No hay stock suficiente para esa salida."));
        }

        Stock = nuevo;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public void Desactivar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static Error? Validar(string? nombre, CategoriaInventario categoria, string? unidad, decimal stock, decimal stockMinimo, string? notas)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("inventario.nombre_vacio", "El nombre del artículo es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("inventario.nombre_largo", "El nombre del artículo es demasiado largo.");
        }

        if (!Enum.IsDefined(categoria))
        {
            return Error.Validacion("inventario.categoria_invalida", "La categoría no es válida.");
        }

        if (unidad is not null && unidad.Trim().Length > LongitudMaximaUnidad)
        {
            return Error.Validacion("inventario.unidad_larga", "La unidad es demasiado larga.");
        }

        if (stock < 0)
        {
            return Error.Validacion("inventario.stock_invalido", "El stock no puede ser negativo.");
        }

        if (stockMinimo < 0)
        {
            return Error.Validacion("inventario.minimo_invalido", "El stock mínimo no puede ser negativo.");
        }

        if (notas is not null && notas.Trim().Length > LongitudMaximaNotas)
        {
            return Error.Validacion("inventario.notas_largas", "Las notas son demasiado largas.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
