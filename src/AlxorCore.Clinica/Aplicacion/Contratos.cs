using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Aplicacion;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>Vista de un animal. Incluye datos derivados (<see cref="EdadMeses"/>, <see cref="EsCachorro"/>).</summary>
public sealed record AnimalDto(
    Guid Id,
    Guid ClienteId,
    string Nombre,
    EspecieAnimal Especie,
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
    public static AnimalDto Desde(Animal a, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new AnimalDto(
            a.Id, a.ClienteId, a.Nombre, a.Especie, a.Raza, a.Sexo, a.FechaNacimiento, a.Microchip,
            a.Esterilizado, a.PesoKg, a.Notas, a.Activo, a.EdadMeses(hoy), a.EsCachorro(hoy));
    }
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

/// <summary>Unidad de trabajo del módulo Clínica.</summary>
public interface IUnidadDeTrabajoClinica : IUnidadDeTrabajo;
