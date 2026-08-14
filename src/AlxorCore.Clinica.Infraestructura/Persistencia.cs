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

    public DbSet<Recordatorio> Recordatorios => Set<Recordatorio>();

    public DbSet<Cita> Citas => Set<Cita>();

    public DbSet<ActoClinico> ActosClinicos => Set<ActoClinico>();

    public DbSet<AccesoPortal> AccesosPortal => Set<AccesoPortal>();

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

internal sealed class ConfiguracionRecordatorio : IEntityTypeConfiguration<Recordatorio>
{
    public void Configure(EntityTypeBuilder<Recordatorio> builder)
    {
        builder.ToTable("recordatorio");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(r => r.AnimalId).HasColumnName("animal_id").IsRequired();
        builder.Property(r => r.Tipo).HasColumnName("tipo").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(r => r.Titulo).HasColumnName("titulo").HasMaxLength(Recordatorio.LongitudMaximaTitulo).IsRequired();
        builder.Property(r => r.FechaObjetivo).HasColumnName("fecha_objetivo").IsRequired();
        builder.Property(r => r.Notas).HasColumnName("notas").HasMaxLength(Recordatorio.LongitudMaximaNotas);
        builder.Property(r => r.ReferenciaTipo).HasColumnName("referencia_tipo").HasMaxLength(Recordatorio.LongitudMaximaReferenciaTipo);
        builder.Property(r => r.ReferenciaId).HasColumnName("referencia_id");
        builder.Property(r => r.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(r => r.FechaEnvio).HasColumnName("fecha_envio");
        builder.Property(r => r.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(r => r.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(r => new { r.EmpresaId, r.AnimalId }).HasDatabaseName("ix_recordatorio_empresa_animal");
        builder.HasIndex(r => new { r.EmpresaId, r.Estado, r.FechaObjetivo }).HasDatabaseName("ix_recordatorio_empresa_estado_fecha");

        // Deduplicación por origen: un mismo vencimiento (referencia) no genera dos recordatorios en la
        // misma empresa. Índice único parcial: solo aplica a los recordatorios con referencia (los
        // manuales, sin referencia, quedan fuera).
        builder.HasIndex(r => new { r.EmpresaId, r.ReferenciaTipo, r.ReferenciaId })
            .HasDatabaseName("ix_recordatorio_referencia")
            .IsUnique()
            .HasFilter("referencia_id IS NOT NULL");

        builder.Ignore(r => r.EventosDominio);
    }
}

internal sealed class RepositorioRecordatorios : IRepositorioRecordatorios, IConsultaRecordatorios
{
    private readonly ClinicaDbContext _contexto;

    public RepositorioRecordatorios(ClinicaDbContext contexto) => _contexto = contexto;

    public Task<Recordatorio?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Recordatorios.SingleOrDefaultAsync(r => r.Id == id, ct);

    public void Agregar(Recordatorio recordatorio) => _contexto.Recordatorios.Add(recordatorio);

    public async Task<RecordatorioDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var recordatorio = await _contexto.Recordatorios.SingleOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
        return recordatorio is null ? null : RecordatorioDto.Desde(recordatorio);
    }

    public async Task<IReadOnlyList<RecordatorioDto>> ListarAsync(Guid empresaId, EstadoRecordatorio? estado = null, DateOnly? desde = null, DateOnly? hasta = null, CancellationToken ct = default)
    {
        var consulta = _contexto.Recordatorios.Where(r => r.EmpresaId == empresaId);
        if (estado is { } e)
        {
            consulta = consulta.Where(r => r.Estado == e);
        }

        if (desde is { } d)
        {
            consulta = consulta.Where(r => r.FechaObjetivo >= d);
        }

        if (hasta is { } h)
        {
            consulta = consulta.Where(r => r.FechaObjetivo <= h);
        }

        var recordatorios = await consulta
            .OrderBy(r => r.FechaObjetivo)
            .ThenBy(r => r.Titulo)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return recordatorios.Select(RecordatorioDto.Desde).ToList();
    }

    public async Task<IReadOnlyList<RecordatorioDto>> ListarPendientesAsync(Guid empresaId, DateOnly hasta, CancellationToken ct = default)
    {
        var recordatorios = await _contexto.Recordatorios
            .Where(r => r.EmpresaId == empresaId && r.Estado == EstadoRecordatorio.Pendiente && r.FechaObjetivo <= hasta)
            .OrderBy(r => r.FechaObjetivo)
            .ThenBy(r => r.Titulo)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return recordatorios.Select(RecordatorioDto.Desde).ToList();
    }

    public Task<bool> ExisteConReferenciaAsync(Guid empresaId, string referenciaTipo, Guid referenciaId, CancellationToken ct = default) =>
        _contexto.Recordatorios.AnyAsync(
            r => r.EmpresaId == empresaId && r.ReferenciaTipo == referenciaTipo && r.ReferenciaId == referenciaId,
            ct);
}

internal sealed class ConfiguracionCita : IEntityTypeConfiguration<Cita>
{
    public void Configure(EntityTypeBuilder<Cita> builder)
    {
        builder.ToTable("cita");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(c => c.AnimalId).HasColumnName("animal_id").IsRequired();
        builder.Property(c => c.Inicio).HasColumnName("inicio").IsRequired();
        builder.Property(c => c.DuracionMinutos).HasColumnName("duracion_minutos").IsRequired();
        builder.Property(c => c.Tipo).HasColumnName("tipo").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(c => c.Motivo).HasColumnName("motivo").HasMaxLength(Cita.LongitudMaximaMotivo);
        builder.Property(c => c.Veterinario).HasColumnName("veterinario").HasMaxLength(Cita.LongitudMaximaVeterinario);
        builder.Property(c => c.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(c => c.Notas).HasColumnName("notas").HasMaxLength(Cita.LongitudMaximaNotas);
        builder.Property(c => c.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(c => c.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(c => new { c.EmpresaId, c.AnimalId }).HasDatabaseName("ix_cita_empresa_animal");
        builder.HasIndex(c => new { c.EmpresaId, c.Inicio }).HasDatabaseName("ix_cita_empresa_inicio");
        // Índice de apoyo para la agenda y los KPI (filtran por estado dentro de una ventana de inicio).
        builder.HasIndex(c => new { c.EmpresaId, c.Estado, c.Inicio }).HasDatabaseName("ix_cita_empresa_estado_inicio");
        builder.Ignore(c => c.EventosDominio);
    }
}

internal sealed class RepositorioCitas : IRepositorioCitas, IConsultaCitas
{
    private readonly ClinicaDbContext _contexto;

    public RepositorioCitas(ClinicaDbContext contexto) => _contexto = contexto;

    public Task<Cita?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Citas.SingleOrDefaultAsync(c => c.Id == id, ct);

    public void Agregar(Cita cita) => _contexto.Citas.Add(cita);

    public async Task<CitaDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var cita = await _contexto.Citas.SingleOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
        return cita is null ? null : CitaDto.Desde(cita);
    }

    public async Task<IReadOnlyList<CitaDto>> ListarPorAnimalAsync(Guid animalId, bool incluirCanceladas = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Citas.Where(c => c.AnimalId == animalId);
        if (!incluirCanceladas)
        {
            consulta = consulta.Where(c => c.Estado != EstadoCita.Cancelada);
        }

        var citas = await consulta
            .OrderByDescending(c => c.Inicio)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return citas.Select(CitaDto.Desde).ToList();
    }

    public async Task<IReadOnlyList<CitaDto>> ListarAgendaAsync(Guid empresaId, DateTimeOffset desde, DateTimeOffset hasta, EstadoCita? estado = null, string? veterinario = null, CancellationToken ct = default)
    {
        var consulta = _contexto.Citas
            .Where(c => c.EmpresaId == empresaId && c.Inicio >= desde && c.Inicio <= hasta);

        if (estado is { } e)
        {
            consulta = consulta.Where(c => c.Estado == e);
        }

        if (!string.IsNullOrWhiteSpace(veterinario))
        {
            var vet = veterinario.Trim();
            consulta = consulta.Where(c => c.Veterinario == vet);
        }

        var citas = await consulta
            .OrderBy(c => c.Inicio)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return citas.Select(CitaDto.Desde).ToList();
    }

    public async Task<ResumenCitasDto> ResumenAsync(Guid empresaId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct = default)
    {
        // Un único recorrido en BD que cuenta por estado; el porcentaje se compone en memoria.
        var conteos = await _contexto.Citas
            .Where(c => c.EmpresaId == empresaId && c.Inicio >= desde && c.Inicio <= hasta)
            .GroupBy(c => c.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        int Contar(EstadoCita estado) => conteos.SingleOrDefault(x => x.Estado == estado)?.Cantidad ?? 0;

        var solicitadas = Contar(EstadoCita.Solicitada);
        var confirmadas = Contar(EstadoCita.Confirmada);
        var atendidas = Contar(EstadoCita.Atendida);
        var canceladas = Contar(EstadoCita.Cancelada);
        var noPresentado = Contar(EstadoCita.NoPresentado);
        var total = solicitadas + confirmadas + atendidas + canceladas + noPresentado;

        // KPI de confirmación: las atendidas cuentan como confirmadas (acudieron).
        var porcentaje = total == 0
            ? 0
            : (int)Math.Round((confirmadas + atendidas) * 100.0 / total, MidpointRounding.AwayFromZero);

        return new ResumenCitasDto(total, solicitadas, confirmadas, atendidas, canceladas, noPresentado, porcentaje);
    }

    public async Task<IReadOnlyList<PuntoConfirmacionMensualDto>> ConfirmacionMensualAsync(Guid empresaId, int meses, DateOnly hoy, CancellationToken ct = default)
    {
        // Ventana: los últimos «meses» meses naturales incluido el actual. Se agrupa en memoria por
        // año/mes del inicio (en UTC) para que la serie sea determinista con independencia del huso.
        var primerMes = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-(meses - 1));
        var inicioVentana = new DateTimeOffset(primerMes.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var citas = await _contexto.Citas
            .Where(c => c.EmpresaId == empresaId && c.Inicio >= inicioVentana)
            .Select(c => new { c.Inicio, c.Estado })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var porMes = citas
            .GroupBy(c => (c.Inicio.UtcDateTime.Year, c.Inicio.UtcDateTime.Month))
            .ToDictionary(
                g => g.Key,
                g => (Citadas: g.Count(), Confirmadas: g.Count(c => c.Estado is EstadoCita.Confirmada or EstadoCita.Atendida)));

        var serie = new List<PuntoConfirmacionMensualDto>(meses);
        for (var i = 0; i < meses; i++)
        {
            var mes = primerMes.AddMonths(i);
            var clave = (mes.Year, mes.Month);
            var valores = porMes.TryGetValue(clave, out var v) ? v : (Citadas: 0, Confirmadas: 0);
            serie.Add(new PuntoConfirmacionMensualDto(mes.Year, mes.Month, valores.Citadas, valores.Confirmadas));
        }

        return serie;
    }
}

internal sealed class ConfiguracionActoClinico : IEntityTypeConfiguration<ActoClinico>
{
    public void Configure(EntityTypeBuilder<ActoClinico> builder)
    {
        builder.ToTable("acto_clinico");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(a => a.AnimalId).HasColumnName("animal_id").IsRequired();
        builder.Property(a => a.ClienteId).HasColumnName("cliente_id").IsRequired();
        builder.Property(a => a.Fecha).HasColumnName("fecha").IsRequired();
        builder.Property(a => a.Concepto).HasColumnName("concepto").HasMaxLength(ActoClinico.LongitudMaximaConcepto).IsRequired();
        builder.Property(a => a.Importe).HasColumnName("importe").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(a => a.PorcentajeIva).HasColumnName("porcentaje_iva").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(a => a.ReferenciaTipo).HasColumnName("referencia_tipo").HasMaxLength(ActoClinico.LongitudMaximaReferenciaTipo);
        builder.Property(a => a.ReferenciaId).HasColumnName("referencia_id");
        builder.Property(a => a.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(a => a.FacturaId).HasColumnName("factura_id");
        builder.Property(a => a.CobradoTicketEn).HasColumnName("cobrado_ticket_en");
        builder.Property(a => a.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(a => a.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        // La facturación busca los pendientes de un cliente; la operativa, los actos de un animal.
        builder.HasIndex(a => new { a.EmpresaId, a.ClienteId, a.Estado }).HasDatabaseName("ix_acto_clinico_empresa_cliente_estado");
        builder.HasIndex(a => new { a.EmpresaId, a.AnimalId }).HasDatabaseName("ix_acto_clinico_empresa_animal");
        builder.Ignore(a => a.EventosDominio);
    }
}

internal sealed class RepositorioActosClinicos : IRepositorioActosClinicos, IConsultaActosClinicos
{
    private readonly ClinicaDbContext _contexto;

    public RepositorioActosClinicos(ClinicaDbContext contexto) => _contexto = contexto;

    public Task<ActoClinico?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.ActosClinicos.SingleOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<ActoClinico>> ObtenerVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return Array.Empty<ActoClinico>();
        }

        return await _contexto.ActosClinicos.Where(a => ids.Contains(a.Id)).ToListAsync(ct).ConfigureAwait(false);
    }

    public void Agregar(ActoClinico acto) => _contexto.ActosClinicos.Add(acto);

    public async Task<ActoClinicoDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var acto = await _contexto.ActosClinicos.SingleOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false);
        return acto is null ? null : ActoClinicoDto.Desde(acto);
    }

    public async Task<IReadOnlyList<ActoClinicoDto>> ListarPorAnimalAsync(Guid animalId, CancellationToken ct = default)
    {
        var actos = await _contexto.ActosClinicos
            .Where(a => a.AnimalId == animalId)
            .OrderByDescending(a => a.Fecha)
            .ThenByDescending(a => a.CreadoEn)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return actos.Select(ActoClinicoDto.Desde).ToList();
    }

    public async Task<IReadOnlyList<ActoClinicoDto>> ListarPorEstadoAsync(Guid empresaId, EstadoActo estado, CancellationToken ct = default)
    {
        var actos = await _contexto.ActosClinicos
            .Where(a => a.EmpresaId == empresaId && a.Estado == estado)
            .OrderByDescending(a => a.Fecha)
            .ThenByDescending(a => a.CreadoEn)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return actos.Select(ActoClinicoDto.Desde).ToList();
    }

    public async Task<IReadOnlyList<ActoClinicoDto>> ListarPendientesDeClienteAsync(Guid empresaId, Guid clienteId, CancellationToken ct = default)
    {
        var actos = await _contexto.ActosClinicos
            .Where(a => a.EmpresaId == empresaId && a.ClienteId == clienteId && a.Estado == EstadoActo.Pendiente)
            .OrderBy(a => a.Fecha)
            .ThenBy(a => a.CreadoEn)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return actos.Select(ActoClinicoDto.Desde).ToList();
    }
}

internal sealed class ConfiguracionAccesoPortal : IEntityTypeConfiguration<AccesoPortal>
{
    public void Configure(EntityTypeBuilder<AccesoPortal> builder)
    {
        builder.ToTable("acceso_portal");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(a => a.ClienteId).HasColumnName("cliente_id").IsRequired();
        builder.Property(a => a.Token).HasColumnName("token").HasMaxLength(AccesoPortal.LongitudMaximaToken).IsRequired();
        builder.Property(a => a.Activo).HasColumnName("activo").IsRequired();
        builder.Property(a => a.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(a => a.RevocadoEn).HasColumnName("revocado_en");

        // El token es la credencial del portal: único en todo el sistema (índice único global).
        builder.HasIndex(a => a.Token).HasDatabaseName("ux_acceso_portal_token").IsUnique();
        // Un cliente tiene como mucho un acceso activo (índice único parcial).
        builder.HasIndex(a => new { a.EmpresaId, a.ClienteId })
            .HasDatabaseName("ux_acceso_portal_cliente_activo")
            .IsUnique()
            .HasFilter("activo");
        builder.Ignore(a => a.EventosDominio);
    }
}

internal sealed class RepositorioAccesosPortal : IRepositorioAccesosPortal, IConsultaAccesosPortal
{
    private readonly ClinicaDbContext _contexto;

    public RepositorioAccesosPortal(ClinicaDbContext contexto) => _contexto = contexto;

    public Task<AccesoPortal?> ObtenerActivoPorClienteAsync(Guid clienteId, CancellationToken ct = default) =>
        _contexto.AccesosPortal.SingleOrDefaultAsync(a => a.ClienteId == clienteId && a.Activo, ct);

    public void Agregar(AccesoPortal acceso) => _contexto.AccesosPortal.Add(acceso);

    // Resolución del token: ignora el filtro multiempresa (aún no hay empresa fijada). Es la única
    // consulta que cruza empresas, y solo mediante un token opaco de ≥32 bytes. Solo resuelve accesos
    // activos: un token revocado o inexistente devuelve null (⇒ 404, sin filtrar información).
    public Task<AccesoPortal?> ObtenerPorTokenAsync(string token, CancellationToken ct = default) =>
        _contexto.AccesosPortal
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(a => a.Token == token && a.Activo, ct);

    public Task<AccesoPortal?> ObtenerPorClienteAsync(Guid clienteId, CancellationToken ct = default) =>
        _contexto.AccesosPortal.SingleOrDefaultAsync(a => a.ClienteId == clienteId && a.Activo, ct);
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
