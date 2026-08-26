using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Persistencia;
using AlxorCore.Terceros.Aplicacion;
using AlxorCore.Terceros.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Terceros.Infraestructura;

/// <summary>Contexto de persistencia del módulo Terceros.</summary>
public sealed class TercerosDbContext : DbContextEmpresaBase, IUnidadDeTrabajoTerceros
{
    public TercerosDbContext(DbContextOptions<TercerosDbContext> opciones, IPublicadorEventos publicador, IContextoEmpresa contexto)
        : base(opciones, publicador, contexto)
    {
    }

    public const string Esquema = "terceros";

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TercerosDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

internal sealed class ConfiguracionCliente : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("cliente");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(c => c.Nombre).HasColumnName("nombre").HasMaxLength(Cliente.LongitudMaximaNombre).IsRequired();
        builder.Property(c => c.NifFiscal).HasColumnName("nif_fiscal").HasMaxLength(20);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(254);
        builder.Property(c => c.Telefono).HasColumnName("telefono").HasMaxLength(Cliente.LongitudMaximaTelefono);
        builder.OwnsOne(c => c.Direccion, d =>
        {
            d.Property(p => p.Calle).HasColumnName("direccion_calle").HasMaxLength(200);
            d.Property(p => p.CodigoPostal).HasColumnName("direccion_cp").HasMaxLength(10);
            d.Property(p => p.Poblacion).HasColumnName("direccion_poblacion").HasMaxLength(120);
            d.Property(p => p.Provincia).HasColumnName("direccion_provincia").HasMaxLength(120);
            d.Property(p => p.Pais).HasColumnName("direccion_pais").HasMaxLength(2);
        });
        builder.Property(c => c.PorcentajeIrpfDefecto).HasColumnName("irpf_defecto").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(c => c.RecargoEquivalencia).HasColumnName("recargo_equivalencia").IsRequired();
        builder.Property(c => c.Iban).HasColumnName("iban").HasMaxLength(34);
        builder.Property(c => c.MandatoReferencia).HasColumnName("mandato_referencia").HasMaxLength(35);
        builder.Property(c => c.MandatoFecha).HasColumnName("mandato_fecha");
        builder.Property(c => c.Activo).HasColumnName("activo").IsRequired();
        builder.Property(c => c.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(c => c.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(c => new { c.EmpresaId, c.Nombre }).HasDatabaseName("ix_cliente_empresa_nombre");
        builder.Ignore(c => c.EventosDominio);
    }
}

internal sealed class RepositorioClientes : IRepositorioClientes, IConsultaClientes
{
    private readonly TercerosDbContext _contexto;

    public RepositorioClientes(TercerosDbContext contexto) => _contexto = contexto;

    public Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Clientes.SingleOrDefaultAsync(c => c.Id == id, ct);

    public void Agregar(Cliente cliente) => _contexto.Clientes.Add(cliente);

    public async Task<ClienteDto?> ObtenerAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await _contexto.Clientes.SingleOrDefaultAsync(c => c.Id == clienteId, ct).ConfigureAwait(false);
        return cliente is null ? null : ClienteDto.Desde(cliente);
    }

    public async Task<IReadOnlyList<ClienteDto>> ListarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Clientes.Where(c => c.EmpresaId == empresaId);
        if (!incluirInactivos)
        {
            consulta = consulta.Where(c => c.Activo);
        }

        var clientes = await consulta.OrderBy(c => c.Nombre).ToListAsync(ct).ConfigureAwait(false);
        return clientes.Select(ClienteDto.Desde).ToList();
    }
}

internal sealed class ConfiguracionProveedor : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> builder)
    {
        builder.ToTable("proveedor");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(p => p.Nombre).HasColumnName("nombre").HasMaxLength(Proveedor.LongitudMaximaNombre).IsRequired();
        builder.Property(p => p.NifFiscal).HasColumnName("nif_fiscal").HasMaxLength(20);
        builder.Property(p => p.Email).HasColumnName("email").HasMaxLength(254);
        builder.OwnsOne(p => p.Direccion, d =>
        {
            d.Property(x => x.Calle).HasColumnName("direccion_calle").HasMaxLength(200);
            d.Property(x => x.CodigoPostal).HasColumnName("direccion_cp").HasMaxLength(10);
            d.Property(x => x.Poblacion).HasColumnName("direccion_poblacion").HasMaxLength(120);
            d.Property(x => x.Provincia).HasColumnName("direccion_provincia").HasMaxLength(120);
            d.Property(x => x.Pais).HasColumnName("direccion_pais").HasMaxLength(2);
        });
        builder.Property(p => p.PorcentajeIrpfDefecto).HasColumnName("irpf_defecto").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(p => p.FormaPago).HasColumnName("forma_pago").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(p => p.Activo).HasColumnName("activo").IsRequired();
        builder.Property(p => p.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(p => new { p.EmpresaId, p.Nombre }).HasDatabaseName("ix_proveedor_empresa_nombre");
        builder.Ignore(p => p.EventosDominio);
    }
}

internal sealed class RepositorioProveedores : IRepositorioProveedores, IConsultaProveedores
{
    private readonly TercerosDbContext _contexto;

    public RepositorioProveedores(TercerosDbContext contexto) => _contexto = contexto;

    public Task<Proveedor?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Proveedores.SingleOrDefaultAsync(p => p.Id == id, ct);

    public void Agregar(Proveedor proveedor) => _contexto.Proveedores.Add(proveedor);

    public async Task<ProveedorDto?> ObtenerAsync(Guid proveedorId, CancellationToken ct = default)
    {
        var proveedor = await _contexto.Proveedores.SingleOrDefaultAsync(p => p.Id == proveedorId, ct).ConfigureAwait(false);
        return proveedor is null ? null : ProveedorDto.Desde(proveedor);
    }

    public async Task<IReadOnlyList<ProveedorDto>> ListarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Proveedores.Where(p => p.EmpresaId == empresaId);
        if (!incluirInactivos)
        {
            consulta = consulta.Where(p => p.Activo);
        }

        var proveedores = await consulta.OrderBy(p => p.Nombre).ToListAsync(ct).ConfigureAwait(false);
        return proveedores.Select(ProveedorDto.Desde).ToList();
    }
}

/// <summary>Factoría en tiempo de diseño para migraciones.</summary>
public sealed class TercerosDbContextFactory : IDesignTimeDbContextFactory<TercerosDbContext>
{
    public TercerosDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";
        var opciones = new DbContextOptionsBuilder<TercerosDbContext>().UseNpgsql(conexion).Options;
        return new TercerosDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
    }

    private sealed class PublicadorInactivo : IPublicadorEventos
    {
        public Task PublicarAsync(IReadOnlyCollection<IEventoDominio> eventos, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ContextoVacio : IContextoEmpresa
    {
        public Guid? EmpresaId => null;
    }
}
