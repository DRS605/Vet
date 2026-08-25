using AlxorCore.Nucleo.Comun;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Organizacion.Dominio;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Organizacion.Infraestructura.Persistencia;

internal sealed class RepositorioEmpresas : IRepositorioEmpresas, IConsultaEmpresas
{
    private readonly OrganizacionDbContext _contexto;

    public RepositorioEmpresas(OrganizacionDbContext contexto) => _contexto = contexto;

    public Task<Empresa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Empresas.SingleOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> ExisteNifAsync(string nif, CancellationToken ct = default)
    {
        var vo = Nif.Rehidratar(nif);
        return _contexto.Empresas.AnyAsync(e => e.Nif == vo, ct);
    }

    public void Agregar(Empresa empresa) => _contexto.Empresas.Add(empresa);

    public async Task<EmpresaDto?> ObtenerAsync(Guid empresaId, CancellationToken ct = default)
    {
        var empresa = await _contexto.Empresas.SingleOrDefaultAsync(e => e.Id == empresaId, ct).ConfigureAwait(false);
        return empresa is null ? null : EmpresaDto.Desde(empresa);
    }
}

internal sealed class RepositorioMembresias : IRepositorioMembresias
{
    private readonly OrganizacionDbContext _contexto;

    public RepositorioMembresias(OrganizacionDbContext contexto) => _contexto = contexto;

    public Task<Membresia?> ObtenerAsync(Guid usuarioId, Guid empresaId, CancellationToken ct = default) =>
        _contexto.Membresias.SingleOrDefaultAsync(m => m.UsuarioId == usuarioId && m.EmpresaId == empresaId, ct);

    public void Agregar(Membresia membresia) => _contexto.Membresias.Add(membresia);

    public async Task<IReadOnlyList<Membresia>> ListarPorEmpresaAsync(Guid empresaId, CancellationToken ct = default) =>
        await _contexto.Membresias.Where(m => m.EmpresaId == empresaId).OrderBy(m => m.CreadoEn).ToListAsync(ct).ConfigureAwait(false);
}

internal sealed class RepositorioSeries : IRepositorioSeries
{
    private readonly OrganizacionDbContext _contexto;

    public RepositorioSeries(OrganizacionDbContext contexto) => _contexto = contexto;

    public void Agregar(SerieNumeracion serie) => _contexto.Series.Add(serie);

    public async Task<IReadOnlyList<SerieNumeracion>> ListarAsync(Guid empresaId, CancellationToken ct = default) =>
        await _contexto.Series.Where(s => s.EmpresaId == empresaId).OrderBy(s => s.Prefijo).ToListAsync(ct).ConfigureAwait(false);

    public Task<bool> ExisteAsync(Guid empresaId, TipoDocumento tipo, int ejercicio, string prefijo, CancellationToken ct = default) =>
        _contexto.Series.AnyAsync(
            s => s.EmpresaId == empresaId && s.TipoDocumento == tipo && s.Ejercicio == ejercicio && s.Prefijo == prefijo,
            ct);
}

/// <summary>Consultas de lectura del módulo Organización (join membresía-empresa).</summary>
internal sealed class ConsultasOrganizacion : IConsultasOrganizacion
{
    private readonly OrganizacionDbContext _contexto;

    public ConsultasOrganizacion(OrganizacionDbContext contexto) => _contexto = contexto;

    public async Task<IReadOnlyList<EmpresaResumen>> ListarEmpresasDeUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var filas = await (
            from m in _contexto.Membresias
            where m.UsuarioId == usuarioId && m.Estado == EstadoMembresia.Activa
            join e in _contexto.Empresas on m.EmpresaId equals e.Id
            select new { e.Id, e.Nif, e.RazonSocial, m.RolCodigo })
            .ToListAsync(ct).ConfigureAwait(false);

        return filas
            .Select(f => new EmpresaResumen(f.Id, f.Nif.Valor, f.RazonSocial, f.RolCodigo))
            .ToList();
    }

    public Task<bool> ExisteAlgunaEmpresaAsync(CancellationToken ct = default) =>
        _contexto.Empresas.AnyAsync(ct);
}
