using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Terceros.Dominio;

namespace AlxorCore.Terceros.Aplicacion;

/// <summary>Una fila de la importación de clientes, con su número de línea en el CSV.</summary>
public sealed record FilaImportacionCliente(int Fila, DatosCliente Datos);

/// <summary>
/// Caso de uso: importar clientes por lotes (desde CSV). Valida cada fila con las mismas reglas del
/// dominio; en previsualización no persiste, y al confirmar da de alta las filas correctas en una
/// sola transacción. Las filas con error se devuelven con su número de línea y motivo.
/// </summary>
public sealed class ImportarClientes
{
    private readonly IRepositorioClientes _clientes;
    private readonly IUnidadDeTrabajoTerceros _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ImportarClientes(IRepositorioClientes clientes, IUnidadDeTrabajoTerceros unidadDeTrabajo, IReloj reloj)
    {
        _clientes = clientes;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<ResultadoImportacion> EjecutarAsync(
        Guid empresaId, IReadOnlyList<FilaImportacionCliente> filas, bool previsualizar, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filas);

        var errores = new List<ErrorFila>();
        var validos = new List<Cliente>();

        foreach (var fila in filas)
        {
            var d = fila.Datos;
            var direccion = Direccion.Crear(d.Calle, d.CodigoPostal, d.Poblacion, d.Provincia, d.Pais);
            var cliente = Cliente.Crear(empresaId, d.Nombre, d.NifFiscal, d.Email, direccion, d.PorcentajeIrpfDefecto, _reloj, telefono: d.Telefono);
            if (cliente.EsFallo)
            {
                errores.Add(new ErrorFila(fila.Fila, cliente.Error.Mensaje));
            }
            else
            {
                validos.Add(cliente.Valor);
            }
        }

        if (!previsualizar && validos.Count > 0)
        {
            foreach (var cliente in validos)
            {
                _clientes.Agregar(cliente);
            }

            await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        }

        return new ResultadoImportacion(filas.Count, validos.Count, previsualizar ? 0 : validos.Count, previsualizar, errores);
    }
}
