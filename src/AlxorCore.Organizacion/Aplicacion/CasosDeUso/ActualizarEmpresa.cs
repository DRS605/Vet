using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Organizacion.Dominio;

namespace AlxorCore.Organizacion.Aplicacion.CasosDeUso;

/// <summary>Datos maestros editables de una empresa.</summary>
public sealed record ActualizarEmpresaComando(
    string Nif,
    string RazonSocial,
    string? Calle = null,
    string? CodigoPostal = null,
    string? Poblacion = null,
    string? Provincia = null,
    RegimenIva RegimenIva = RegimenIva.General);

/// <summary>
/// Caso de uso: actualizar los datos maestros de una empresa (NIF, razón social, dirección, IVA).
/// No modifica documentos ya congelados; solo cambia los datos de la empresa a futuro.
/// </summary>
public sealed class ActualizarEmpresa
{
    private readonly IRepositorioEmpresas _empresas;
    private readonly IUnidadDeTrabajoOrganizacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarEmpresa(IRepositorioEmpresas empresas, IUnidadDeTrabajoOrganizacion unidadDeTrabajo, IReloj reloj)
    {
        _empresas = empresas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<EmpresaDto>> EjecutarAsync(Guid empresaId, ActualizarEmpresaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var empresa = await _empresas.ObtenerPorIdAsync(empresaId, ct).ConfigureAwait(false);
        if (empresa is null)
        {
            return Resultado.Fallo<EmpresaDto>(Error.NoEncontrado("empresa.no_encontrada", "La empresa no existe."));
        }

        var nif = Nif.Crear(comando.Nif);
        if (nif.EsFallo)
        {
            return Resultado.Fallo<EmpresaDto>(nif.Error);
        }

        // Si cambia el NIF, comprobamos que no lo tenga ya otra empresa.
        if (!string.Equals(nif.Valor.Valor, empresa.Nif.Valor, StringComparison.Ordinal)
            && await _empresas.ExisteNifAsync(nif.Valor.Valor, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<EmpresaDto>(Error.Conflicto("empresa.nif_en_uso", "Ya existe una empresa con ese NIF."));
        }

        var direccion = Direccion.Crear(comando.Calle, comando.CodigoPostal, comando.Poblacion, comando.Provincia);

        var actualizacion = empresa.ActualizarDatos(nif.Valor, comando.RazonSocial, direccion, comando.RegimenIva, _reloj);
        if (actualizacion.EsFallo)
        {
            return Resultado.Fallo<EmpresaDto>(actualizacion.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(EmpresaDto.Desde(empresa));
    }
}

/// <summary>Caso de uso: establecer (o quitar) el logo de la empresa activa.</summary>
public sealed class ActualizarLogoEmpresa
{
    private readonly IRepositorioEmpresas _empresas;
    private readonly IUnidadDeTrabajoOrganizacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarLogoEmpresa(IRepositorioEmpresas empresas, IUnidadDeTrabajoOrganizacion unidadDeTrabajo, IReloj reloj)
    {
        _empresas = empresas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<EmpresaDto>> EjecutarAsync(Guid empresaId, string? logo, CancellationToken ct = default)
    {
        var empresa = await _empresas.ObtenerPorIdAsync(empresaId, ct).ConfigureAwait(false);
        if (empresa is null)
        {
            return Resultado.Fallo<EmpresaDto>(Error.NoEncontrado("empresa.no_encontrada", "La empresa no existe."));
        }

        empresa.EstablecerLogo(logo, _reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(EmpresaDto.Desde(empresa));
    }
}
