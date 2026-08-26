using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Persistencia;
using AlxorCore.Terceros.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Facturacion.Infraestructura;

/// <summary>Contexto de persistencia del módulo Facturación.</summary>
public sealed class FacturacionDbContext : DbContextEmpresaBase, IUnidadDeTrabajoFacturacion
{
    public FacturacionDbContext(DbContextOptions<FacturacionDbContext> opciones, IPublicadorEventos publicador, IContextoEmpresa contexto)
        : base(opciones, publicador, contexto)
    {
    }

    public const string Esquema = "facturacion";

    public DbSet<Factura> Facturas => Set<Factura>();

    public DbSet<FacturaRecurrente> FacturasRecurrentes => Set<FacturaRecurrente>();

    public DbSet<Presupuesto> Presupuestos => Set<Presupuesto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FacturacionDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

internal sealed class ConfiguracionFactura : IEntityTypeConfiguration<Factura>
{
    public void Configure(EntityTypeBuilder<Factura> builder)
    {
        builder.ToTable("factura");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.EmpresaId).HasColumnName("empresa_id").IsRequired();

        builder.Property(f => f.Prefijo).HasColumnName("prefijo").HasMaxLength(10).IsRequired();
        builder.Property(f => f.Ejercicio).HasColumnName("ejercicio").IsRequired();
        builder.Property(f => f.Numero).HasColumnName("numero").IsRequired();
        builder.Property(f => f.NumeroCompleto).HasColumnName("numero_completo").HasMaxLength(30).IsRequired();
        builder.HasIndex(f => new { f.EmpresaId, f.Prefijo, f.Ejercicio, f.Numero })
            .IsUnique().HasDatabaseName("ux_factura_numero");

        builder.Property(f => f.FechaEmision).HasColumnName("fecha_emision").IsRequired();
        builder.Property(f => f.FechaOperacion).HasColumnName("fecha_operacion").IsRequired();
        builder.Property(f => f.FechaVencimiento).HasColumnName("fecha_vencimiento").IsRequired();

        builder.Property(f => f.ClienteId).HasColumnName("cliente_id");
        builder.Property(f => f.ClienteNombre).HasColumnName("cliente_nombre").HasMaxLength(200).IsRequired();
        builder.Property(f => f.ClienteNif).HasColumnName("cliente_nif").HasMaxLength(20);
        builder.Property(f => f.ClienteCalle).HasColumnName("cliente_calle").HasMaxLength(200);
        builder.Property(f => f.ClienteCodigoPostal).HasColumnName("cliente_cp").HasMaxLength(10);
        builder.Property(f => f.ClientePoblacion).HasColumnName("cliente_poblacion").HasMaxLength(120);
        builder.Property(f => f.ClienteProvincia).HasColumnName("cliente_provincia").HasMaxLength(120);
        builder.Property(f => f.Pais).HasColumnName("pais").HasMaxLength(2).IsRequired();

        builder.Property(f => f.BaseImponible).HasColumnName("base_imponible").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(f => f.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(f => f.PorcentajeIrpf).HasColumnName("porcentaje_irpf").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(f => f.RetencionIrpf).HasColumnName("retencion_irpf").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(f => f.RecargoEquivalencia).HasColumnName("recargo_equivalencia").IsRequired();
        builder.Property(f => f.RecargoTotal).HasColumnName("recargo_total").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(f => f.Total).HasColumnName("total").HasColumnType("numeric(14,2)").IsRequired();

        builder.Property(f => f.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(f => f.TipoFactura).HasColumnName("tipo_factura").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(f => f.RectificaFacturaId).HasColumnName("rectifica_factura_id");
        builder.Property(f => f.MotivoRectificacion).HasColumnName("motivo_rectificacion").HasMaxLength(300);

        builder.Property(f => f.CreadoEn).HasColumnName("creado_en").IsRequired();

        builder.Property(f => f.Observaciones).HasColumnName("observaciones").HasMaxLength(Factura.LongitudMaximaObservaciones);

        // Campos VeriFactu/SII reservados (nullable, sin lógica en el MVP).
        builder.Property(f => f.Huella).HasColumnName("huella").HasMaxLength(128);
        builder.Property(f => f.HuellaAnterior).HasColumnName("huella_anterior").HasMaxLength(128);
        builder.Property(f => f.IdRegistro).HasColumnName("id_registro").HasMaxLength(64);
        builder.Property(f => f.TipoOperacion).HasColumnName("tipo_operacion").HasMaxLength(20);
        builder.Property(f => f.EstadoEnvioAeat).HasColumnName("estado_envio_aeat").HasMaxLength(20);
        builder.Property(f => f.FechaHoraGenRegistro).HasColumnName("fecha_hora_gen_registro");

        builder.Ignore(f => f.EventosDominio);

        builder.OwnsMany(f => f.Lineas, linea =>
        {
            linea.ToTable("linea_factura");
            linea.WithOwner().HasForeignKey("factura_id");
            linea.HasKey(l => l.Id);
            linea.Property(l => l.Id).HasColumnName("id");
            linea.Property(l => l.EmpresaId).HasColumnName("empresa_id").IsRequired();
            linea.Property(l => l.ProductoId).HasColumnName("producto_id");
            linea.Property(l => l.Descripcion).HasColumnName("descripcion").HasMaxLength(300).IsRequired();
            linea.Property(l => l.Cantidad).HasColumnName("cantidad").HasColumnType("numeric(14,3)").IsRequired();
            linea.Property(l => l.PrecioUnitario).HasColumnName("precio_unitario").HasColumnType("numeric(14,4)").IsRequired();
            linea.Property(l => l.CosteUnitario).HasColumnName("coste_unitario").HasColumnType("numeric(14,4)").IsRequired();
            linea.Property(l => l.PorcentajeDescuento).HasColumnName("descuento").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.CodigoIva).HasColumnName("codigo_iva").HasMaxLength(10).IsRequired();
            linea.Property(l => l.PorcentajeIva).HasColumnName("porcentaje_iva").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.PorcentajeRecargo).HasColumnName("porcentaje_recargo").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.Base).HasColumnName("base").HasColumnType("numeric(14,2)").IsRequired();
            linea.Property(l => l.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
            linea.Property(l => l.CuotaRecargo).HasColumnName("cuota_recargo").HasColumnType("numeric(14,2)").IsRequired();
            linea.Ignore(l => l.CosteTotal);
            linea.Ignore(l => l.Margen);
        });
    }
}

internal sealed class ConfiguracionFacturaRecurrente : IEntityTypeConfiguration<FacturaRecurrente>
{
    public void Configure(EntityTypeBuilder<FacturaRecurrente> builder)
    {
        builder.ToTable("factura_recurrente");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.EmpresaId).HasColumnName("empresa_id").IsRequired();

        builder.Property(r => r.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
        builder.Property(r => r.ClienteId).HasColumnName("cliente_id").IsRequired();
        builder.Property(r => r.Periodicidad).HasColumnName("periodicidad").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(r => r.ProximaEmision).HasColumnName("proxima_emision").IsRequired();
        builder.Property(r => r.FechaFin).HasColumnName("fecha_fin");
        builder.Property(r => r.PorcentajeIrpf).HasColumnName("porcentaje_irpf").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(r => r.Activa).HasColumnName("activa").IsRequired();
        builder.Property(r => r.FacturasGeneradas).HasColumnName("facturas_generadas").IsRequired();
        builder.Property(r => r.UltimaEmision).HasColumnName("ultima_emision");
        builder.Property(r => r.CreadoEn).HasColumnName("creado_en").IsRequired();

        builder.HasIndex(r => new { r.EmpresaId, r.Activa, r.ProximaEmision }).HasDatabaseName("ix_recurrente_vencidas");

        builder.Ignore(r => r.EventosDominio);

        builder.OwnsMany(r => r.Lineas, linea =>
        {
            linea.ToTable("linea_recurrente");
            linea.WithOwner().HasForeignKey("factura_recurrente_id");
            linea.HasKey(l => l.Id);
            linea.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            linea.Property(l => l.EmpresaId).HasColumnName("empresa_id").IsRequired();
            linea.Property(l => l.ProductoId).HasColumnName("producto_id");
            linea.Property(l => l.Descripcion).HasColumnName("descripcion").HasMaxLength(300).IsRequired();
            linea.Property(l => l.Cantidad).HasColumnName("cantidad").HasColumnType("numeric(14,3)").IsRequired();
            linea.Property(l => l.PrecioUnitario).HasColumnName("precio_unitario").HasColumnType("numeric(14,4)").IsRequired();
            linea.Property(l => l.PorcentajeDescuento).HasColumnName("descuento").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.CodigoIva).HasColumnName("codigo_iva").HasMaxLength(10).IsRequired();
            linea.Property(l => l.PorcentajeIva).HasColumnName("porcentaje_iva").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.Base).HasColumnName("base").HasColumnType("numeric(14,2)").IsRequired();
            linea.Property(l => l.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
        });
    }
}

internal sealed class RepositorioFacturasRecurrentes : IRepositorioFacturasRecurrentes, IConsultaFacturasRecurrentes
{
    private readonly FacturacionDbContext _contexto;
    private readonly IConsultaClientes _clientes;

    public RepositorioFacturasRecurrentes(FacturacionDbContext contexto, IConsultaClientes clientes)
    {
        _contexto = contexto;
        _clientes = clientes;
    }

    public void Agregar(FacturaRecurrente recurrente) => _contexto.FacturasRecurrentes.Add(recurrente);

    public Task<FacturaRecurrente?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.FacturasRecurrentes.SingleOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<FacturaRecurrente>> ListarVencidasAsync(DateOnly hoy, CancellationToken ct = default)
    {
        return await _contexto.FacturasRecurrentes
            .Where(r => r.Activa && r.ProximaEmision <= hoy && (r.FechaFin == null || r.ProximaEmision <= r.FechaFin))
            .OrderBy(r => r.ProximaEmision)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    // Recorre TODAS las empresas: ignora el filtro multiempresa a propósito (solo lo usa el proceso
    // automático en segundo plano, que luego opera empresa por empresa con su contexto fijado).
    public async Task<IReadOnlyList<Guid>> EmpresasConVencidasAsync(DateOnly hoy, CancellationToken ct = default)
    {
        return await _contexto.FacturasRecurrentes
            .IgnoreQueryFilters()
            .Where(r => r.Activa && r.ProximaEmision <= hoy && (r.FechaFin == null || r.ProximaEmision <= r.FechaFin))
            .Select(r => r.EmpresaId)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<FacturaRecurrenteDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var recurrente = await _contexto.FacturasRecurrentes.SingleOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
        return recurrente is null ? null : FacturaRecurrenteDto.Desde(recurrente);
    }

    public async Task<IReadOnlyList<FacturaRecurrenteResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var recurrentes = await _contexto.FacturasRecurrentes
            .Where(r => r.EmpresaId == empresaId)
            .OrderByDescending(r => r.Activa).ThenBy(r => r.ProximaEmision)
            .ToListAsync(ct).ConfigureAwait(false);

        var nombresCliente = new Dictionary<Guid, string>();
        var resumenes = new List<FacturaRecurrenteResumen>(recurrentes.Count);
        foreach (var r in recurrentes)
        {
            if (!nombresCliente.TryGetValue(r.ClienteId, out var nombre))
            {
                var cliente = await _clientes.ObtenerAsync(r.ClienteId, ct).ConfigureAwait(false);
                nombre = cliente?.Nombre ?? "—";
                nombresCliente[r.ClienteId] = nombre;
            }

            var dto = FacturaRecurrenteDto.Desde(r);
            resumenes.Add(new FacturaRecurrenteResumen(r.Id, r.Nombre, nombre, r.Periodicidad.ToString(), r.ProximaEmision, r.Activa, dto.Total));
        }

        return resumenes;
    }
}

internal sealed class RepositorioFacturas : IRepositorioFacturas, IConsultaFacturas
{
    private readonly FacturacionDbContext _contexto;

    public RepositorioFacturas(FacturacionDbContext contexto) => _contexto = contexto;

    public Task<Factura?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Facturas.SingleOrDefaultAsync(f => f.Id == id, ct);

    public void Agregar(Factura factura) => _contexto.Facturas.Add(factura);

    public Task<string?> UltimaHuellaAsync(Guid empresaId, CancellationToken ct = default) =>
        _contexto.Facturas
            .Where(f => f.EmpresaId == empresaId && f.Huella != null)
            .OrderByDescending(f => f.FechaHoraGenRegistro).ThenByDescending(f => f.Numero)
            .Select(f => f.Huella)
            .FirstOrDefaultAsync(ct);

    public async Task<FacturaDto?> ObtenerAsync(Guid facturaId, CancellationToken ct = default)
    {
        var factura = await _contexto.Facturas.SingleOrDefaultAsync(f => f.Id == facturaId, ct).ConfigureAwait(false);
        return factura is null ? null : FacturaDto.Desde(factura);
    }

    public async Task<IReadOnlyList<FacturaResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var facturas = await _contexto.Facturas
            .Where(f => f.EmpresaId == empresaId)
            .OrderByDescending(f => f.FechaEmision).ThenByDescending(f => f.Numero)
            .ToListAsync(ct).ConfigureAwait(false);

        return facturas
            .Select(f => new FacturaResumen(
                f.Id, f.NumeroCompleto, f.FechaEmision, f.FechaVencimiento, f.ClienteNombre, f.ClienteNif, f.BaseImponible, f.CuotaIva, f.RetencionIrpf, f.Total, f.Estado.ToString(), f.TipoFactura.ToString()))
            .ToList();
    }

    public async Task<IReadOnlyList<LineaMargenDto>> ListarLineasMargenAsync(Guid empresaId, DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var facturas = await _contexto.Facturas
            .Where(f => f.EmpresaId == empresaId && f.Estado == EstadoFactura.Emitida && f.FechaEmision >= desde && f.FechaEmision <= hasta)
            .ToListAsync(ct).ConfigureAwait(false);

        return facturas
            .SelectMany(f => f.Lineas.Select(l => new LineaMargenDto(l.ProductoId, l.Descripcion, l.Cantidad, l.Base, l.CosteTotal)))
            .ToList();
    }
}

internal sealed class ConfiguracionPresupuesto : IEntityTypeConfiguration<Presupuesto>
{
    public void Configure(EntityTypeBuilder<Presupuesto> builder)
    {
        builder.ToTable("presupuesto");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(p => p.NumeroCompleto).HasColumnName("numero_completo").HasMaxLength(30).IsRequired();
        builder.Property(p => p.ClienteId).HasColumnName("cliente_id").IsRequired();
        builder.Property(p => p.ClienteNombre).HasColumnName("cliente_nombre").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Fecha).HasColumnName("fecha").IsRequired();
        builder.Property(p => p.Validez).HasColumnName("validez").IsRequired();
        builder.Property(p => p.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(p => p.BaseImponible).HasColumnName("base_imponible").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(p => p.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(p => p.Total).HasColumnName("total").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(p => p.FacturaId).HasColumnName("factura_id");
        builder.Property(p => p.CreadoEn).HasColumnName("creado_en").IsRequired();

        builder.OwnsMany(p => p.Lineas, linea =>
        {
            linea.ToTable("linea_presupuesto");
            linea.WithOwner().HasForeignKey("presupuesto_id");
            linea.HasKey(l => l.Id);
            linea.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            linea.Property(l => l.EmpresaId).HasColumnName("empresa_id").IsRequired();
            linea.Property(l => l.ProductoId).HasColumnName("producto_id");
            linea.Property(l => l.Descripcion).HasColumnName("descripcion").HasMaxLength(300).IsRequired();
            linea.Property(l => l.Cantidad).HasColumnName("cantidad").HasColumnType("numeric(14,3)").IsRequired();
            linea.Property(l => l.PrecioUnitario).HasColumnName("precio_unitario").HasColumnType("numeric(14,4)").IsRequired();
            linea.Property(l => l.PorcentajeDescuento).HasColumnName("descuento").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.CodigoIva).HasColumnName("codigo_iva").HasMaxLength(10).IsRequired();
            linea.Property(l => l.PorcentajeIva).HasColumnName("porcentaje_iva").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.Base).HasColumnName("base").HasColumnType("numeric(14,2)").IsRequired();
            linea.Property(l => l.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
        });

        builder.HasIndex(p => new { p.EmpresaId, p.Fecha }).HasDatabaseName("ix_presupuesto_empresa_fecha");
        builder.Ignore(p => p.EventosDominio);
    }
}

internal sealed class RepositorioPresupuestos : IRepositorioPresupuestos, IConsultaPresupuestos
{
    private readonly FacturacionDbContext _contexto;

    public RepositorioPresupuestos(FacturacionDbContext contexto) => _contexto = contexto;

    public Task<Presupuesto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Presupuestos.SingleOrDefaultAsync(p => p.Id == id, ct);

    public void Agregar(Presupuesto presupuesto) => _contexto.Presupuestos.Add(presupuesto);

    public async Task<long> SiguienteNumeroAsync(Guid empresaId, int ejercicio, CancellationToken ct = default)
    {
        var count = await _contexto.Presupuestos
            .Where(p => p.EmpresaId == empresaId && p.Fecha.Year == ejercicio)
            .CountAsync(ct).ConfigureAwait(false);
        return count + 1;
    }

    public async Task<PresupuestoDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _contexto.Presupuestos.SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        return p is null ? null : PresupuestoDto.Desde(p);
    }

    public async Task<IReadOnlyList<PresupuestoResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var presupuestos = await _contexto.Presupuestos
            .Where(p => p.EmpresaId == empresaId)
            .OrderByDescending(p => p.Fecha).ThenByDescending(p => p.NumeroCompleto)
            .ToListAsync(ct).ConfigureAwait(false);
        return presupuestos
            .Select(p => new PresupuestoResumen(p.Id, p.NumeroCompleto, p.Fecha, p.Validez, p.ClienteNombre, p.Total, p.Estado.ToString(), p.FacturaId))
            .ToList();
    }
}

/// <summary>Factoría en tiempo de diseño para migraciones.</summary>
public sealed class FacturacionDbContextFactory : IDesignTimeDbContextFactory<FacturacionDbContext>
{
    public FacturacionDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";
        var opciones = new DbContextOptionsBuilder<FacturacionDbContext>().UseNpgsql(conexion).Options;
        return new FacturacionDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
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
