using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Aplicacion;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>Vista de un animal. Incluye datos derivados (<see cref="EdadMeses"/>, <see cref="EsCachorro"/>).</summary>
public sealed record AnimalDto(
    Guid Id,
    Guid ClienteId,
    string Nombre,
    string Especie,
    string? Raza,
    SexoAnimal Sexo,
    DateOnly? FechaNacimiento,
    string? Microchip,
    bool Esterilizado,
    decimal? PesoKg,
    string? Notas,
    bool Activo,
    int? EdadMeses,
    bool EsCachorro)
{
    /// <summary>
    /// Construye la vista. El cálculo de «cachorro» vive aquí (no en el dominio): recibe el umbral en
    /// meses de la especie del animal, resuelto por el repositorio desde el maestro de especies. Si no
    /// se conoce (especie sin registro), se usa el umbral por defecto.
    /// </summary>
    public static AnimalDto Desde(Animal a, DateOnly hoy, int umbralCachorroMeses = AlxorCore.Clinica.Dominio.Especie.MesesCachorroPorDefecto)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new AnimalDto(
            a.Id, a.ClienteId, a.Nombre, a.Especie, a.Raza, a.Sexo, a.FechaNacimiento, a.Microchip,
            a.Esterilizado, a.PesoKg, a.Notas, a.Activo, a.EdadMeses(hoy), a.EsCachorro(hoy, umbralCachorroMeses));
    }
}

/// <summary>Vista de una especie del maestro editable de la empresa.</summary>
public sealed record EspecieDto(
    Guid Id,
    string Nombre,
    int MesesCachorro,
    bool Activo,
    string? Emoji)
{
    public static EspecieDto Desde(Especie e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new EspecieDto(e.Id, e.Nombre, e.MesesCachorro, e.Activo, e.Emoji);
    }
}

/// <summary>Datos de una especie para crear o actualizar.</summary>
public sealed record DatosEspecie(string Nombre, int MesesCachorro = AlxorCore.Clinica.Dominio.Especie.MesesCachorroPorDefecto, string? Emoji = null);

/// <summary>Repositorio de especies (escritura).</summary>
public interface IRepositorioEspecies
{
    Task<Especie?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Especie especie);
}

/// <summary>Consultas de lectura del maestro de especies.</summary>
public interface IConsultaEspecies
{
    Task<EspecieDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Obtiene una especie por su nombre exacto dentro de la empresa (activa o no), o <c>null</c>.</summary>
    Task<EspecieDto?> ObtenerPorNombreAsync(Guid empresaId, string nombre, CancellationToken ct = default);

    Task<IReadOnlyList<EspecieDto>> ListarAsync(Guid empresaId, bool incluirInactivas = false, CancellationToken ct = default);

    /// <summary>¿Existe ya una especie con ese nombre en la empresa? Opcionalmente excluye un id (al actualizar).</summary>
    Task<bool> ExisteNombreAsync(Guid empresaId, string nombre, Guid? excluirId = null, CancellationToken ct = default);
}

/// <summary>Repositorio de animales (escritura).</summary>
public interface IRepositorioAnimales
{
    Task<Animal?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Animal animal);
}

/// <summary>Consultas de lectura de animales (las usan la propia API y, en el futuro, otros módulos veterinarios).</summary>
public interface IConsultaAnimales
{
    Task<AnimalDto?> ObtenerAsync(Guid animalId, CancellationToken ct = default);

    Task<IReadOnlyList<AnimalDto>> ListarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default);

    Task<IReadOnlyList<AnimalDto>> ListarPorClienteAsync(Guid clienteId, bool incluirInactivos = false, CancellationToken ct = default);
}

/// <summary>Vista de una consulta (entrada del historial clínico).</summary>
public sealed record ConsultaDto(
    Guid Id,
    Guid AnimalId,
    DateOnly Fecha,
    string? Motivo,
    string? Diagnostico,
    string? Tratamiento,
    decimal? PesoKg,
    string? Veterinario,
    bool Activo)
{
    public static ConsultaDto Desde(Consulta c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new ConsultaDto(
            c.Id, c.AnimalId, c.Fecha, c.Motivo, c.Diagnostico, c.Tratamiento, c.PesoKg, c.Veterinario, c.Activo);
    }
}

/// <summary>Repositorio de consultas (escritura).</summary>
public interface IRepositorioConsultas
{
    Task<Consulta?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Consulta consulta);
}

/// <summary>Consultas de lectura del historial clínico.</summary>
public interface IConsultaConsultas
{
    Task<ConsultaDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ConsultaDto>> ListarPorAnimalAsync(Guid animalId, bool incluirAnuladas = false, CancellationToken ct = default);
}

/// <summary>Vista de una pauta vacunal (cuadro maestro de vacunación por especie).</summary>
public sealed record PautaVacunalDto(
    Guid Id,
    string Especie,
    string Nombre,
    CaracterVacuna Caracter,
    int? EdadInicioSemanas,
    int? PeriodicidadRefuerzoMeses,
    bool Activo)
{
    public static PautaVacunalDto Desde(PautaVacunal p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return new PautaVacunalDto(
            p.Id, p.Especie, p.Nombre, p.Caracter, p.EdadInicioSemanas, p.PeriodicidadRefuerzoMeses, p.Activo);
    }
}

/// <summary>Repositorio de pautas vacunales (escritura).</summary>
public interface IRepositorioPautasVacunales
{
    Task<PautaVacunal?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(PautaVacunal pauta);
}

/// <summary>Consultas de lectura de pautas vacunales.</summary>
public interface IConsultaPautasVacunales
{
    Task<PautaVacunalDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<PautaVacunalDto>> ListarAsync(Guid empresaId, bool incluirInactivas = false, CancellationToken ct = default);

    Task<IReadOnlyList<PautaVacunalDto>> ListarPorEspecieAsync(Guid empresaId, string especie, bool incluirInactivas = false, CancellationToken ct = default);

    /// <summary>¿Existe ya una pauta con ese nombre para la especie en la empresa? Opcionalmente excluye un id (al actualizar).</summary>
    Task<bool> ExisteNombreAsync(Guid empresaId, string especie, string nombre, Guid? excluirId = null, CancellationToken ct = default);
}

/// <summary>Vista de una vacunación (dosis aplicada a un animal).</summary>
public sealed record VacunacionDto(
    Guid Id,
    Guid AnimalId,
    Guid? PautaVacunalId,
    string Nombre,
    DateOnly FechaAplicacion,
    string? Lote,
    DateOnly? ProximaDosis,
    string? Veterinario,
    string? Notas,
    bool Activo)
{
    public static VacunacionDto Desde(Vacunacion v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return new VacunacionDto(
            v.Id, v.AnimalId, v.PautaVacunalId, v.Nombre, v.FechaAplicacion, v.Lote, v.ProximaDosis, v.Veterinario, v.Notas, v.Activo);
    }
}

/// <summary>Repositorio de vacunaciones (escritura).</summary>
public interface IRepositorioVacunaciones
{
    Task<Vacunacion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Vacunacion vacunacion);
}

/// <summary>Consultas de lectura del historial de vacunaciones.</summary>
public interface IConsultaVacunaciones
{
    Task<VacunacionDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<VacunacionDto>> ListarPorAnimalAsync(Guid animalId, bool incluirAnuladas = false, CancellationToken ct = default);

    /// <summary>Vacunaciones cuya próxima dosis cae en la ventana [desde, hasta], ordenadas por próxima dosis ascendente. Base para recordatorios/KPI.</summary>
    Task<IReadOnlyList<VacunacionDto>> ListarProximasAsync(Guid empresaId, DateOnly desde, DateOnly hasta, bool incluirAnuladas = false, CancellationToken ct = default);
}

/// <summary>Vista de una cirugía (intervención quirúrgica de un animal).</summary>
public sealed record CirugiaDto(
    Guid Id,
    Guid AnimalId,
    DateOnly Fecha,
    string Nombre,
    string? Descripcion,
    string? Cirujano,
    string? Anestesia,
    string? Complicaciones,
    DateOnly? ProximaRevision,
    bool Activo)
{
    public static CirugiaDto Desde(Cirugia c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new CirugiaDto(
            c.Id, c.AnimalId, c.Fecha, c.Nombre, c.Descripcion, c.Cirujano, c.Anestesia, c.Complicaciones, c.ProximaRevision, c.Activo);
    }
}

/// <summary>Repositorio de cirugías (escritura).</summary>
public interface IRepositorioCirugias
{
    Task<Cirugia?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Cirugia cirugia);
}

/// <summary>Consultas de lectura del historial de cirugías.</summary>
public interface IConsultaCirugias
{
    Task<CirugiaDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CirugiaDto>> ListarPorAnimalAsync(Guid animalId, bool incluirAnuladas = false, CancellationToken ct = default);

    /// <summary>Cirugías cuya próxima revisión cae en la ventana [desde, hasta], ordenadas por próxima revisión ascendente. Base para recordatorios/KPI.</summary>
    Task<IReadOnlyList<CirugiaDto>> ListarProximasRevisionesAsync(Guid empresaId, DateOnly desde, DateOnly hasta, bool incluirAnuladas = false, CancellationToken ct = default);
}

/// <summary>Vista de un recordatorio (aviso asociado a un animal).</summary>
public sealed record RecordatorioDto(
    Guid Id,
    Guid AnimalId,
    TipoRecordatorio Tipo,
    string Titulo,
    DateOnly FechaObjetivo,
    string? Notas,
    string? ReferenciaTipo,
    Guid? ReferenciaId,
    EstadoRecordatorio Estado,
    DateTimeOffset? FechaEnvio)
{
    public static RecordatorioDto Desde(Recordatorio r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new RecordatorioDto(
            r.Id, r.AnimalId, r.Tipo, r.Titulo, r.FechaObjetivo, r.Notas,
            r.ReferenciaTipo, r.ReferenciaId, r.Estado, r.FechaEnvio);
    }
}

/// <summary>Repositorio de recordatorios (escritura).</summary>
public interface IRepositorioRecordatorios
{
    Task<Recordatorio?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Recordatorio recordatorio);
}

/// <summary>Consultas de lectura de recordatorios.</summary>
public interface IConsultaRecordatorios
{
    Task<RecordatorioDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lista los recordatorios de la empresa, con filtros opcionales por estado y ventana [desde, hasta] de fecha objetivo.</summary>
    Task<IReadOnlyList<RecordatorioDto>> ListarAsync(Guid empresaId, EstadoRecordatorio? estado = null, DateOnly? desde = null, DateOnly? hasta = null, CancellationToken ct = default);

    /// <summary>Lista los recordatorios pendientes con fecha objetivo hasta la indicada, ordenados por fecha objetivo ascendente.</summary>
    Task<IReadOnlyList<RecordatorioDto>> ListarPendientesAsync(Guid empresaId, DateOnly hasta, CancellationToken ct = default);

    /// <summary>¿Existe ya un recordatorio con ese origen (tipo + id de referencia) en la empresa? Base de la deduplicación.</summary>
    Task<bool> ExisteConReferenciaAsync(Guid empresaId, string referenciaTipo, Guid referenciaId, CancellationToken ct = default);
}

/// <summary>Vista de una cita (entrada de la agenda).</summary>
public sealed record CitaDto(
    Guid Id,
    Guid AnimalId,
    DateTimeOffset Inicio,
    int DuracionMinutos,
    TipoCita Tipo,
    string? Motivo,
    string? Veterinario,
    EstadoCita Estado,
    string? Notas)
{
    public static CitaDto Desde(Cita c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new CitaDto(
            c.Id, c.AnimalId, c.Inicio, c.DuracionMinutos, c.Tipo, c.Motivo, c.Veterinario, c.Estado, c.Notas);
    }
}

/// <summary>
/// Resumen de citas de una ventana temporal. <see cref="PorcentajeConfirmacion"/> es el KPI de
/// confirmación: (confirmadas + atendidas) / total × 100, redondeado (0 si no hay citas). Las citas
/// atendidas cuentan como confirmadas: acudieron.
/// </summary>
public sealed record ResumenCitasDto(
    int Total,
    int Solicitadas,
    int Confirmadas,
    int Atendidas,
    int Canceladas,
    int NoPresentado,
    int PorcentajeConfirmacion);

/// <summary>Punto de la serie mensual de confirmación (para el gráfico del panel).</summary>
public sealed record PuntoConfirmacionMensualDto(int Anio, int Mes, int Citadas, int Confirmadas);

/// <summary>Repositorio de citas (escritura).</summary>
public interface IRepositorioCitas
{
    Task<Cita?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Cita cita);
}

/// <summary>Consultas de lectura de la agenda y de los KPI de citas.</summary>
public interface IConsultaCitas
{
    Task<CitaDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lista las citas de un animal, ordenadas por inicio descendente. Excluye las canceladas salvo que se pida lo contrario.</summary>
    Task<IReadOnlyList<CitaDto>> ListarPorAnimalAsync(Guid animalId, bool incluirCanceladas = false, CancellationToken ct = default);

    /// <summary>La agenda: citas de la empresa cuyo inicio cae en [desde, hasta], ordenadas por inicio ascendente, con filtros opcionales por estado y veterinario.</summary>
    Task<IReadOnlyList<CitaDto>> ListarAgendaAsync(Guid empresaId, DateTimeOffset desde, DateTimeOffset hasta, EstadoCita? estado = null, string? veterinario = null, CancellationToken ct = default);

    /// <summary>Resumen de citas (KPI de confirmación) de la ventana [desde, hasta].</summary>
    Task<ResumenCitasDto> ResumenAsync(Guid empresaId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct = default);

    /// <summary>Serie de confirmación de los últimos <paramref name="meses"/> meses (para el gráfico del panel), ordenada del más antiguo al más reciente.</summary>
    Task<IReadOnlyList<PuntoConfirmacionMensualDto>> ConfirmacionMensualAsync(Guid empresaId, int meses, DateOnly hoy, CancellationToken ct = default);
}

/// <summary>Vista de un acto clínico (línea facturable).</summary>
public sealed record ActoClinicoDto(
    Guid Id,
    Guid AnimalId,
    Guid ClienteId,
    DateOnly Fecha,
    string Concepto,
    decimal Importe,
    decimal PorcentajeIva,
    string? ReferenciaTipo,
    Guid? ReferenciaId,
    EstadoActo Estado,
    Guid? FacturaId,
    DateTimeOffset? CobradoTicketEn)
{
    public static ActoClinicoDto Desde(ActoClinico a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new ActoClinicoDto(
            a.Id, a.AnimalId, a.ClienteId, a.Fecha, a.Concepto, a.Importe, a.PorcentajeIva,
            a.ReferenciaTipo, a.ReferenciaId, a.Estado, a.FacturaId, a.CobradoTicketEn);
    }
}

/// <summary>Repositorio de actos clínicos (escritura).</summary>
public interface IRepositorioActosClinicos
{
    Task<ActoClinico?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Carga varios actos por su identificador (para facturar un lote de una vez).</summary>
    Task<IReadOnlyList<ActoClinico>> ObtenerVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    void Agregar(ActoClinico acto);
}

/// <summary>Consultas de lectura de actos clínicos.</summary>
public interface IConsultaActosClinicos
{
    Task<ActoClinicoDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ActoClinicoDto>> ListarPorAnimalAsync(Guid animalId, CancellationToken ct = default);

    /// <summary>Lista los actos de la empresa en un estado (p. ej. los pendientes de facturar).</summary>
    Task<IReadOnlyList<ActoClinicoDto>> ListarPorEstadoAsync(Guid empresaId, EstadoActo estado, CancellationToken ct = default);

    /// <summary>Lista los actos pendientes de un cliente (candidatos a una misma factura).</summary>
    Task<IReadOnlyList<ActoClinicoDto>> ListarPendientesDeClienteAsync(Guid empresaId, Guid clienteId, CancellationToken ct = default);
}

/// <summary>Unidad de trabajo del módulo Clínica.</summary>
public interface IUnidadDeTrabajoClinica : IUnidadDeTrabajo;
