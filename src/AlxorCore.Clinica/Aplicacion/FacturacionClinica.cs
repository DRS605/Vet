using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>
/// Información clínica asociada a una factura para el panel de facturas: las especies y razas de los
/// animales facturados (derivadas de los actos clínicos) y la nota interna de la clínica.
/// </summary>
public sealed record FacturaClinicaDto(
    Guid FacturaId,
    IReadOnlyList<string> Especies,
    IReadOnlyList<string> Razas,
    string? Nota);

/// <summary>Datos para guardar la nota interna de una factura.</summary>
public sealed record DatosNotaFactura(string? Texto);

/// <summary>Repositorio de notas de factura (escritura).</summary>
public interface IRepositorioNotasFactura
{
    Task<NotaFactura?> ObtenerPorFacturaAsync(Guid empresaId, Guid facturaId, CancellationToken ct = default);

    void Agregar(NotaFactura nota);

    void Eliminar(NotaFactura nota);
}

/// <summary>Consultas de apoyo al panel de facturas (información clínica por factura).</summary>
public interface IConsultaFacturacionClinica
{
    /// <summary>Información clínica (especies, razas, nota) de todas las facturas de la empresa con actos o nota.</summary>
    Task<IReadOnlyList<FacturaClinicaDto>> ListarAsync(Guid empresaId, CancellationToken ct = default);
}

/// <summary>Caso de uso: listar la información clínica por factura (para el panel de facturas).</summary>
public sealed class ListarFacturacionClinica
{
    private readonly IConsultaFacturacionClinica _consulta;

    public ListarFacturacionClinica(IConsultaFacturacionClinica consulta) => _consulta = consulta;

    public Task<IReadOnlyList<FacturaClinicaDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, ct);
}

/// <summary>Caso de uso: guardar (crear/actualizar/borrar) la nota interna de una factura.</summary>
public sealed class GuardarNotaFactura
{
    private readonly IRepositorioNotasFactura _notas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public GuardarNotaFactura(IRepositorioNotasFactura notas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _notas = notas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid facturaId, DatosNotaFactura datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var nota = await _notas.ObtenerPorFacturaAsync(empresaId, facturaId, ct).ConfigureAwait(false);
        var texto = (datos.Texto ?? string.Empty).Trim();
        if (nota is null)
        {
            if (texto.Length > 0)
            {
                _notas.Agregar(NotaFactura.Crear(empresaId, facturaId, texto, _reloj));
            }
        }
        else if (texto.Length == 0)
        {
            // Vaciar la nota la elimina (no deja filas con texto en blanco).
            _notas.Eliminar(nota);
        }
        else
        {
            nota.Establecer(texto, _reloj);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}
