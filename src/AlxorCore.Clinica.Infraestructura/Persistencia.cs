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

    public DbSet<PautaVacunal> PautasVacunales => Set<PautaVacunal>();

    public DbSet<Vacunacion> Vacunaciones => Set<Vacunacion>();

    public DbSet<Cirugia> Cirugias => Set<Cirugia>();

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

internal sealed class ConfiguracionPautaVacunal : IEntityTypeConfiguration<PautaVacunal>
{
    public void Configure(EntityTypeBuilder<PautaVacunal> builder)
    {
        builder.ToTable("pauta_vacunal");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(p => p.Especie).HasColumnName("especie").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(p => p.Nombre).HasColumnName("nombre").HasMaxLength(PautaVacunal.LongitudMaximaNombre).IsRequired();
        builder.Property(p => p.Caracter).HasColumnName("caracter").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(p => p.EdadInicioSemanas).HasColumnName("edad_inicio_semanas");
        builder.Property(p => p.PeriodicidadRefuerzoMeses).HasColumnName("periodicidad_refuerzo_meses");
        builder.Property(p => p.Activo).HasColumnName("activo").IsRequired();
        builder.Property(p => p.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(p => new { p.EmpresaId, p.Especie, p.Nombre })
            .HasDatabaseName("ix_pauta_vacunal_empresa_especie_nombre")
            .IsUnique();
        builder.Ignore(p => p.EventosDominio);
    }
}

internal sealed class RepositorioPautasVacunales : IRepositorioPautasVacunales, IConsultaPautasVacunales
{
    private readonly ClinicaDbContext _contexto;

    public RepositorioPautasVacunales(ClinicaDbContext contexto) => _contexto = contexto;

    public Task<PautaVacunal?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.PautasVacunales.SingleOrDefaultAsync(p => p.Id == id, ct);

    public void Agregar(PautaVacunal pauta) => _contexto.PautasVacunales.Add(pauta);

    public async Task<PautaVacunalDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var pauta = await _contexto.PautasVacunales.SingleOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
        return pauta is null ? null : PautaVacunalDto.Desde(pauta);
    }

    public async Task<IReadOnlyList<PautaVacunalDto>> ListarAsync(Guid empresaId, bool incluirInactivas = false, CancellationToken ct = default)
    {
        var consulta = _contexto.PautasVacunales.Where(p => p.EmpresaId == empresaId);
        if (!incluirInactivas)
        {
            consulta = consulta.Where(p => p.Activo);
        }

        var pautas = await consulta.OrderBy(p => p.Especie).ThenBy(p => p.Nombre).ToListAsync(ct).ConfigureAwait(false);
        return pautas.Select(PautaVacunalDto.Desde).ToList();
    }

    public async Task<IReadOnlyList<PautaVacunalDto>> ListarPorEspecieAsync(Guid empresaId, EspecieAnimal especie, bool incluirInactivas = false, CancellationToken ct = default)
    {
        var consulta = _contexto.PautasVacunales.Where(p => p.EmpresaId == empresaId && p.Especie == especie);
        if (!incluirInactivas)
        {
            consulta = consulta.Where(p => p.Activo);
        }

        var pautas = await consulta.OrderBy(p => p.Nombre).ToListAsync(ct).ConfigureAwait(false);
        return pautas.Select(PautaVacunalDto.Desde).ToList();
    }

    public Task<bool> ExisteNombreAsync(Guid empresaId, EspecieAnimal especie, string nombre, Guid? excluirId = null, CancellationToken ct = default) =>
        _contexto.PautasVacunales.AnyAsync(
            p => p.EmpresaId == empresaId && p.Especie == especie && p.Nombre == nombre && (excluirId == null || p.Id != excluirId),
            ct);
}

internal sealed class ConfiguracionVacunacion : IEntityTypeConfiguration<Vacunacion>
{
    public void Configure(EntityTypeBuilder<Vacunacion> builder)
    {
        builder.ToTable("vacunacion");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(v => v.AnimalId).HasColumnName("animal_id").IsRequired();
        builder.Property(v => v.PautaVacunalId).HasColumnName("pauta_vacunal_id");
        builder.Property(v => v.Nombre).HasColumnName("nombre").HasMaxLength(Vacunacion.LongitudMaximaNombre).IsRequired();
        builder.Property(v => v.FechaAplicacion).HasColumnName("fecha_aplicacion").IsRequired();
        builder.Property(v => v.Lote).HasColumnName("lote").HasMaxLength(Vacunacion.LongitudMaximaLote);
        builder.Property(v => v.ProximaDosis).HasColumnName("proxima_dosis");
        builder.Property(v => v.Veterinario).HasColumnName("veterinario").HasMaxLength(Vacunacion.LongitudMaximaVeterinario);
        builder.Property(v => v.Notas).HasColumnName("notas").HasMaxLength(Vacunacion.LongitudMaximaNotas);
        builder.Property(v => v.Activo).HasColumnName("activo").IsRequired();
        builder.Property(v => v.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(v => v.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(v => new { v.EmpresaId, v.AnimalId }).HasDatabaseName("ix_vacunacion_empresa_animal");
        builder.HasIndex(v => new { v.EmpresaId, v.ProximaDosis }).HasDatabaseName("ix_vacunacion_empresa_proxima_dosis");
        builder.Ignore(v => v.EventosDominio);
    }
}

internal sealed class RepositorioVacunaciones : IRepositorioVacunaciones, IConsultaVacunaciones
{
    private readonly ClinicaDbContext _contexto;

    public RepositorioVacunaciones(ClinicaDbContext contexto) => _contexto = contexto;

    public Task<Vacunacion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Vacunaciones.SingleOrDefaultAsync(v => v.Id == id, ct);

    public void Agregar(Vacunacion vacunacion) => _contexto.Vacunaciones.Add(vacunacion);

    public async Task<VacunacionDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var vacunacion = await _contexto.Vacunaciones.SingleOrDefaultAsync(v => v.Id == id, ct).ConfigureAwait(false);
        return vacunacion is null ? null : VacunacionDto.Desde(vacunacion);
    }

    public async Task<IReadOnlyList<VacunacionDto>> ListarPorAnimalAsync(Guid animalId, bool incluirAnuladas = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Vacunaciones.Where(v => v.AnimalId == animalId);
        if (!incluirAnuladas)
        {
            consulta = consulta.Where(v => v.Activo);
        }

        var vacunaciones = await consulta
            .OrderByDescending(v => v.FechaAplicacion)
            .ThenByDescending(v => v.CreadoEn)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return vacunaciones.Select(VacunacionDto.Desde).ToList();
    }

    public async Task<IReadOnlyList<VacunacionDto>> ListarProximasAsync(Guid empresaId, DateOnly desde, DateOnly hasta, bool incluirAnuladas = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Vacunaciones
            .Where(v => v.EmpresaId == empresaId && v.ProximaDosis != null && v.ProximaDosis >= desde && v.ProximaDosis <= hasta);
        if (!incluirAnuladas)
        {
            consulta = consulta.Where(v => v.Activo);
        }

        var vacunaciones = await consulta
            .OrderBy(v => v.ProximaDosis)
            .ThenBy(v => v.Nombre)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return vacunaciones.Select(VacunacionDto.Desde).ToList();
    }
}

internal sealed class ConfiguracionCirugia : IEntityTypeConfiguration<Cirugia>
{
    public void Configure(EntityTypeBuilder<Cirugia> builder)
    {
        builder.ToTable("cirugia");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(c => c.AnimalId).HasColumnName("animal_id").IsRequired();
        builder.Property(c => c.Fecha).HasColumnName("fecha").IsRequired();
        builder.Property(c => c.Nombre).HasColumnName("nombre").HasMaxLength(Cirugia.LongitudMaximaNombre).IsRequired();
        builder.Property(c => c.Descripcion).HasColumnName("descripcion").HasMaxLength(Cirugia.LongitudMaximaDescripcion);
        builder.Property(c => c.Cirujano).HasColumnName("cirujano").HasMaxLength(Cirugia.LongitudMaximaCirujano);
        builder.Property(c => c.Anestesia).HasColumnName("anestesia").HasMaxLength(Cirugia.LongitudMaximaAnestesia);
        builder.Property(c => c.Complicaciones).HasColumnName("complicaciones").HasMaxLength(Cirugia.LongitudMaximaComplicaciones);
        builder.Property(c => c.ProximaRevision).HasColumnName("proxima_revision");
        builder.Property(c => c.Activo).HasColumnName("activo").IsRequired();
        builder.Property(c => c.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(c => c.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(c => new { c.EmpresaId, c.AnimalId }).HasDatabaseName("ix_cirugia_empresa_animal");
        builder.HasIndex(c => new { c.EmpresaId, c.ProximaRevision }).HasDatabaseName("ix_cirugia_empresa_proxima_revision");
        builder.Ignore(c => c.EventosDominio);
    }
}

internal sealed class RepositorioCirugias : IRepositorioCirugias, IConsultaCirugias
{
    private readonly ClinicaDbContext _contexto;

    public RepositorioCirugias(ClinicaDbContext contexto) => _contexto = contexto;

    public Task<Cirugia?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Cirugias.SingleOrDefaultAsync(c => c.Id == id, ct);

    public void Agregar(Cirugia cirugia) => _contexto.Cirugias.Add(cirugia);

    public async Task<CirugiaDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var cirugia = await _contexto.Cirugias.SingleOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
        return cirugia is null ? null : CirugiaDto.Desde(cirugia);
    }

    public async Task<IReadOnlyList<CirugiaDto>> ListarPorAnimalAsync(Guid animalId, bool incluirAnuladas = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Cirugias.Where(c => c.AnimalId == animalId);
        if (!incluirAnuladas)
        {
            consulta = consulta.Where(c => c.Activo);
        }

        var cirugias = await consulta
            .OrderByDescending(c => c.Fecha)
            .ThenByDescending(c => c.CreadoEn)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return cirugias.Select(CirugiaDto.Desde).ToList();
    }

    public async Task<IReadOnlyList<CirugiaDto>> ListarProximasRevisionesAsync(Guid empresaId, DateOnly desde, DateOnly hasta, bool incluirAnuladas = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Cirugias
            .Where(c => c.EmpresaId == empresaId && c.ProximaRevision != null && c.ProximaRevision >= desde && c.ProximaRevision <= hasta);
        if (!incluirAnuladas)
        {
            consulta = consulta.Where(c => c.Activo);
        }

        var cirugias = await consulta
            .OrderBy(c => c.ProximaRevision)
            .ThenBy(c => c.Nombre)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return cirugias.Select(CirugiaDto.Desde).ToList();
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
