using AlxorCore.Nucleo.Resultados;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using AlxorCore.Organizacion.Aplicacion.Puertos;

namespace AlxorCore.Organizacion.Aplicacion.CasosDeUso;

/// <summary>Caso de uso: listar las empresas del usuario autenticado.</summary>
public sealed class ListarMisEmpresas
{
    private readonly IConsultasOrganizacion _consultas;

    public ListarMisEmpresas(IConsultasOrganizacion consultas) => _consultas = consultas;

    public async Task<IReadOnlyList<EmpresaResumen>> EjecutarAsync(Guid usuarioId, CancellationToken ct = default) =>
        await _consultas.ListarEmpresasDeUsuarioAsync(usuarioId, ct).ConfigureAwait(false);
}

/// <summary>
/// Caso de uso: consultar si la instalación ya está inicializada, es decir, si existe al menos una
/// empresa. Lo usa el endpoint público <c>/estado-instalacion</c> para que la SPA decida entre
/// mostrar el asistente de primer arranque o el login normal. Es una instalación monoclínica local:
/// exponer solo este booleano de forma anónima es aceptable.
/// </summary>
public sealed class ConsultarEstadoInstalacion
{
    private readonly IConsultasOrganizacion _consultas;

    public ConsultarEstadoInstalacion(IConsultasOrganizacion consultas) => _consultas = consultas;

    public async Task<bool> EstaInicializadaAsync(CancellationToken ct = default) =>
        await _consultas.ExisteAlgunaEmpresaAsync(ct).ConfigureAwait(false);
}

/// <summary>Caso de uso: obtener una empresa por su identificador (dentro del contexto de la empresa activa).</summary>
public sealed class ObtenerEmpresa
{
    private readonly IRepositorioEmpresas _empresas;

    public ObtenerEmpresa(IRepositorioEmpresas empresas) => _empresas = empresas;

    public async Task<Resultado<EmpresaDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var empresa = await _empresas.ObtenerPorIdAsync(empresaId, ct).ConfigureAwait(false);
        return empresa is null
            ? Resultado.Fallo<EmpresaDto>(Error.NoEncontrado("empresa.no_encontrada", "La empresa no existe."))
            : Resultado.Ok(EmpresaDto.Desde(empresa));
    }
}
