using AlxorCore.Clinica.Aplicacion;
using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Clinica.Infraestructura;

/// <summary>Contexto de persistencia del módulo Clínica (producto veterinario).</summary>
public sealed class ClinicaDbContext : DbContextEmpresaBase, IUnidadDeTrabajoClinica
{
    public ClinicaDbContext(DbContextOptions<ClinicaDbContext> opciones, IPublicadorEventos publicador, IContextoEmpresa contexto)
        : base(opciones, publicador, contexto)
    {
    }

    public const string Esquema = "clinica";

    public DbSet<Animal> Animales => Set<Animal>();

    public DbSet<Consulta> Consultas => Set<Consulta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClinicaDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

internal sealed class ConfiguracionAnimal : IEntityTypeConfiguration<Animal>
{
    public void Configure(EntityTypeBuilder<Animal> builder)
    {
        builder.ToTable("animal");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(a => a.ClienteId).HasColumnName("cliente_id").IsRequired();
        builder.Property(a => a.Nombre).HasColumnName("nombre").HasMaxLength(Animal.LongitudMaximaNombre).IsRequired();
        builder.Property(a => a.Especie).HasColumnName("especie").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(a => a.Raza).HasColumnName("raza").HasMaxLength(Animal.LongitudMaximaRaza);
        builder.Property(a => a.Sexo).HasColumnName("sexo").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(a => a.FechaNacimiento).HasColumnName("fecha_nacimiento");
        builder.Property(a => a.Microchip).HasColumnName("microchip").HasMaxLength(Animal.LongitudMaximaMicrochip);
        builder.Property(a => a.Esterilizado).HasColumnName("esterilizado").IsRequired();
        builder.Property(a => a.PesoKg).HasColumnName("peso_kg").HasColumnType("numeric(6,3)");
        builder.Property(a => a.Notas).HasColumnName("notas").HasMaxLength(Animal.LongitudMaximaNotas);
        builder.Property(a => a.Activo).HasColumnName("activo").IsRequired();
        builder.Property(a => a.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(a => a.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(a => new { a.EmpresaId, a.ClienteId }).HasDatabaseName("ix_animal_empresa_cliente");
        builder.Ignore(a => a.EventosDominio);
    }
}

internal sealed class RepositorioAnimales : IRepositorioAnimales, IConsultaAnimales
{
    private readonly ClinicaDbContext _contexto;
    private readonly IReloj _reloj;

    public RepositorioAnimales(ClinicaDbContext contexto, IReloj reloj)
    {
        _contexto = contexto;
        _reloj = reloj;
    }

    public Task<Animal?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Animales.SingleOrDefaultAsync(a => a.Id == id, ct);

    public void Agregar(Animal animal) => _contexto.Animales.Add(animal);

    public async Task<AnimalDto?> ObtenerAsync(Guid animalId, CancellationToken ct = default)
    {
        var animal = await _contexto.Animales.SingleOrDefaultAsync(a => a.Id == animalId, ct).ConfigureAwait(false);
        return animal is null ? null : AnimalDto.Desde(animal, Hoy);
    }

    public async Task<IReadOnlyList<AnimalDto>> ListarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Animales.Where(a => a.EmpresaId == empresaId);
        if (!incluirInactivos)
        {
            consulta = consulta.Where(a => a.Activo);
        }

        var animales = await consulta.OrderBy(a => a.Nombre).ToListAsync(ct).ConfigureAwait(false);
        return animales.Select(a => AnimalDto.Desde(a, Hoy)).ToList();
    }

    public async Task<IReadOnlyList<AnimalDto>> ListarPorClienteAsync(Guid clienteId, bool incluirInactivos = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Animales.Where(a => a.ClienteId == clienteId);
        if (!incluirInactivos)
        {
            consulta = consulta.Where(a => a.Activo);
        }

        var animales = await consulta.OrderBy(a => a.Nombre).ToListAsync(ct).ConfigureAwait(false);
        return animales.Select(a => AnimalDto.Desde(a, Hoy)).ToList();
    }

    private DateOnly Hoy => DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
}

internal sealed class ConfiguracionConsulta : IEntityTypeConfiguration<Consulta>
{
    public void Configure(EntityTypeBuilder<Consulta> builder)
    {
        builder.ToTable("consulta");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(c => c.AnimalId).HasColumnName("animal_id").IsRequired();
        builder.Property(c => c.Fecha).HasColumnName("fecha").IsRequired();
        builder.Property(c => c.Motivo).HasColumnName("motivo").HasMaxLength(Consulta.LongitudMaximaMotivo);
        builder.Property(c => c.Diagnostico).HasColumnName("diagnostico").HasMaxLength(Consulta.LongitudMaximaDiagnostico);
        builder.Property(c => c.Tratamiento).HasColumnName("tratamiento").HasMaxLength(Consulta.LongitudMaximaTratamiento);
        builder.Property(c => c.PesoKg).HasColumnName("peso_kg").HasColumnType("numeric(6,3)");
        builder.Property(c => c.Veterinario).HasColumnName("veterinario").HasMaxLength(Consulta.LongitudMaximaVeterinario);
        builder.Property(c => c.Activo).HasColumnName("activo").IsRequired();
        builder.Property(c => c.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(c => c.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(c => new { c.EmpresaId, c.AnimalId }).HasDatabaseName("ix_consulta_empresa_animal");
        builder.Ignore(c => c.EventosDominio);
    }
}

internal sealed class RepositorioConsultas : IRepositorioConsultas, IConsultaConsultas
{
    private readonly ClinicaDbContext _contexto;

    public RepositorioConsultas(ClinicaDbContext contexto) => _contexto = contexto;

    public Task<Consulta?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Consultas.SingleOrDefaultAsync(c => c.Id == id, ct);

    public void Agregar(Consulta consulta) => _contexto.Consultas.Add(consulta);

    public async Task<ConsultaDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var consulta = await _contexto.Consultas.SingleOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
        return consulta is null ? null : ConsultaDto.Desde(consulta);
    }

    public async Task<IReadOnlyList<ConsultaDto>> ListarPorAnimalAsync(Guid animalId, bool incluirAnuladas = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Consultas.Where(c => c.AnimalId == animalId);
        if (!incluirAnuladas)
        {
            consulta = consulta.Where(c => c.Activo);
        }

        var consultas = await consulta
            .OrderByDescending(c => c.Fecha)
            .ThenByDescending(c => c.CreadoEn)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return consultas.Select(ConsultaDto.Desde).ToList();
    }
}

/// <summary>Factoría en tiempo de diseño para migraciones.</summary>
public sealed class ClinicaDbContextFactory : IDesignTimeDbContextFactory<ClinicaDbContext>
{
    public ClinicaDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";
        var opciones = new DbContextOptionsBuilder<ClinicaDbContext>().UseNpgsql(conexion).Options;
        return new ClinicaDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
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
