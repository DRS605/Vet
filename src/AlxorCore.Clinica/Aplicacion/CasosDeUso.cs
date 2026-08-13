using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Clinica.Aplicacion;

/// <summary>Datos de un animal para crear o actualizar.</summary>
public sealed record DatosAnimal(
    Guid ClienteId,
    string Nombre,
    EspecieAnimal Especie = EspecieAnimal.Perro,
    SexoAnimal Sexo = SexoAnimal.Desconocido,
    string? Raza = null,
    DateOnly? FechaNacimiento = null,
    string? Microchip = null,
    bool Esterilizado = false,
    decimal? PesoKg = null,
    string? Notas = null);

/// <summary>Datos de un animal al actualizar (el propietario no cambia; se toma del animal existente).</summary>
public sealed record DatosActualizarAnimal(
    string Nombre,
    EspecieAnimal Especie = EspecieAnimal.Perro,
    SexoAnimal Sexo = SexoAnimal.Desconocido,
    string? Raza = null,
    DateOnly? FechaNacimiento = null,
    string? Microchip = null,
    bool Esterilizado = false,
    decimal? PesoKg = null,
    string? Notas = null);

/// <summary>
/// Caso de uso: crear un animal en la empresa activa. Verifica que el cliente propietario existe en
/// la empresa (a través de <see cref="IConsultaClientes"/> del módulo Terceros).
/// </summary>
public sealed class CrearAnimal
{
    private readonly IRepositorioAnimales _animales;
    private readonly IConsultaClientes _clientes;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearAnimal(IRepositorioAnimales animales, IConsultaClientes clientes, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _animales = animales;
        _clientes = clientes;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<AnimalDto>> EjecutarAsync(Guid empresaId, DatosAnimal datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var cliente = await _clientes.ObtenerAsync(datos.ClienteId, ct).ConfigureAwait(false);
        if (cliente is null)
        {
            return Resultado.Fallo<AnimalDto>(Error.Validacion("animal.cliente_no_encontrado", "El cliente propietario no existe en esta empresa."));
        }

        var animal = Animal.Crear(
            empresaId, datos.ClienteId, datos.Nombre, datos.Especie, datos.Sexo, _reloj,
            datos.Raza, datos.FechaNacimiento, datos.Microchip, datos.Esterilizado, datos.PesoKg, datos.Notas);
        if (animal.EsFallo)
        {
            return Resultado.Fallo<AnimalDto>(animal.Error);
        }

        _animales.Agregar(animal.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        return Resultado.Ok(AnimalDto.Desde(animal.Valor, hoy));
    }
}

/// <summary>Caso de uso: actualizar un animal existente.</summary>
public sealed class ActualizarAnimal
{
    private readonly IRepositorioAnimales _animales;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarAnimal(IRepositorioAnimales animales, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _animales = animales;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<AnimalDto>> EjecutarAsync(Guid animalId, DatosActualizarAnimal datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var animal = await _animales.ObtenerPorIdAsync(animalId, ct).ConfigureAwait(false);
        if (animal is null)
        {
            return Resultado.Fallo<AnimalDto>(Error.NoEncontrado("animal.no_encontrado", "El animal no existe."));
        }

        var actualizado = animal.Actualizar(
            datos.Nombre, datos.Especie, datos.Sexo, _reloj,
            datos.Raza, datos.FechaNacimiento, datos.Microchip, datos.Esterilizado, datos.PesoKg, datos.Notas);
        if (actualizado.EsFallo)
        {
            return Resultado.Fallo<AnimalDto>(actualizado.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        return Resultado.Ok(AnimalDto.Desde(animal, hoy));
    }
}

/// <summary>Caso de uso: listar los animales de la empresa activa.</summary>
public sealed class ListarAnimales
{
    private readonly IConsultaAnimales _consulta;

    public ListarAnimales(IConsultaAnimales consulta) => _consulta = consulta;

    public Task<IReadOnlyList<AnimalDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, incluirInactivos: false, ct);
}

/// <summary>Caso de uso: listar los animales de un cliente concreto.</summary>
public sealed class ListarAnimalesDeCliente
{
    private readonly IConsultaAnimales _consulta;

    public ListarAnimalesDeCliente(IConsultaAnimales consulta) => _consulta = consulta;

    public Task<IReadOnlyList<AnimalDto>> EjecutarAsync(Guid clienteId, CancellationToken ct = default) =>
        _consulta.ListarPorClienteAsync(clienteId, incluirInactivos: false, ct);
}

/// <summary>Caso de uso: obtener un animal por su identificador.</summary>
public sealed class ObtenerAnimal
{
    private readonly IConsultaAnimales _consulta;

    public ObtenerAnimal(IConsultaAnimales consulta) => _consulta = consulta;

    public async Task<Resultado<AnimalDto>> EjecutarAsync(Guid animalId, CancellationToken ct = default)
    {
        var animal = await _consulta.ObtenerAsync(animalId, ct).ConfigureAwait(false);
        return animal is null
            ? Resultado.Fallo<AnimalDto>(Error.NoEncontrado("animal.no_encontrado", "El animal no existe."))
            : Resultado.Ok(animal);
    }
}

/// <summary>Caso de uso: dar de baja (baja lógica) un animal.</summary>
public sealed class DarDeBajaAnimal
{
    private readonly IRepositorioAnimales _animales;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public DarDeBajaAnimal(IRepositorioAnimales animales, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _animales = animales;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid animalId, CancellationToken ct = default)
    {
        var animal = await _animales.ObtenerPorIdAsync(animalId, ct).ConfigureAwait(false);
        if (animal is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("animal.no_encontrado", "El animal no existe."));
        }

        animal.Desactivar(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Datos de una consulta (entrada del historial clínico) para registrar.</summary>
public sealed record DatosConsulta(
    Guid AnimalId,
    DateOnly Fecha,
    string? Motivo = null,
    string? Diagnostico = null,
    string? Tratamiento = null,
    decimal? PesoKg = null,
    string? Veterinario = null);

/// <summary>Datos de una consulta al actualizar (el animal no cambia; se toma de la consulta existente).</summary>
public sealed record DatosActualizarConsulta(
    DateOnly Fecha,
    string? Motivo = null,
    string? Diagnostico = null,
    string? Tratamiento = null,
    decimal? PesoKg = null,
    string? Veterinario = null);

/// <summary>
/// Caso de uso: registrar una consulta en el historial de un animal de la empresa activa. Verifica
/// que el animal existe en la empresa (a través de <see cref="IConsultaAnimales"/>).
/// </summary>
public sealed class RegistrarConsulta
{
    private readonly IRepositorioConsultas _consultas;
    private readonly IConsultaAnimales _animales;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegistrarConsulta(IRepositorioConsultas consultas, IConsultaAnimales animales, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _consultas = consultas;
        _animales = animales;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ConsultaDto>> EjecutarAsync(Guid empresaId, DatosConsulta datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // El filtro multiempresa de EF Core garantiza que solo se encuentra el animal si pertenece a la empresa activa.
        var animal = await _animales.ObtenerAsync(datos.AnimalId, ct).ConfigureAwait(false);
        if (animal is null)
        {
            return Resultado.Fallo<ConsultaDto>(Error.Validacion("consulta.animal_no_encontrado", "El animal no existe en esta empresa."));
        }

        var consulta = Consulta.Crear(
            empresaId, datos.AnimalId, datos.Fecha, _reloj,
            datos.Motivo, datos.Diagnostico, datos.Tratamiento, datos.PesoKg, datos.Veterinario);
        if (consulta.EsFallo)
        {
            return Resultado.Fallo<ConsultaDto>(consulta.Error);
        }

        _consultas.Agregar(consulta.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ConsultaDto.Desde(consulta.Valor));
    }
}

/// <summary>Caso de uso: actualizar una consulta existente (el animal no cambia).</summary>
public sealed class ActualizarConsulta
{
    private readonly IRepositorioConsultas _consultas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarConsulta(IRepositorioConsultas consultas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _consultas = consultas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ConsultaDto>> EjecutarAsync(Guid consultaId, DatosActualizarConsulta datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var consulta = await _consultas.ObtenerPorIdAsync(consultaId, ct).ConfigureAwait(false);
        if (consulta is null)
        {
            return Resultado.Fallo<ConsultaDto>(Error.NoEncontrado("consulta.no_encontrada", "La consulta no existe."));
        }

        var actualizada = consulta.Actualizar(
            datos.Fecha, _reloj, datos.Motivo, datos.Diagnostico, datos.Tratamiento, datos.PesoKg, datos.Veterinario);
        if (actualizada.EsFallo)
        {
            return Resultado.Fallo<ConsultaDto>(actualizada.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ConsultaDto.Desde(consulta));
    }
}

/// <summary>Caso de uso: obtener una consulta por su identificador.</summary>
public sealed class ObtenerConsulta
{
    private readonly IConsultaConsultas _consulta;

    public ObtenerConsulta(IConsultaConsultas consulta) => _consulta = consulta;

    public async Task<Resultado<ConsultaDto>> EjecutarAsync(Guid consultaId, CancellationToken ct = default)
    {
        var consulta = await _consulta.ObtenerAsync(consultaId, ct).ConfigureAwait(false);
        return consulta is null
            ? Resultado.Fallo<ConsultaDto>(Error.NoEncontrado("consulta.no_encontrada", "La consulta no existe."))
            : Resultado.Ok(consulta);
    }
}

/// <summary>Caso de uso: listar el historial clínico (consultas) de un animal, de la más reciente a la más antigua.</summary>
public sealed class ListarConsultasDeAnimal
{
    private readonly IConsultaConsultas _consulta;

    public ListarConsultasDeAnimal(IConsultaConsultas consulta) => _consulta = consulta;

    public Task<IReadOnlyList<ConsultaDto>> EjecutarAsync(Guid animalId, CancellationToken ct = default) =>
        _consulta.ListarPorAnimalAsync(animalId, incluirAnuladas: false, ct);
}

/// <summary>Caso de uso: anular (baja lógica) una consulta del historial.</summary>
public sealed class AnularConsulta
{
    private readonly IRepositorioConsultas _consultas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AnularConsulta(IRepositorioConsultas consultas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _consultas = consultas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid consultaId, CancellationToken ct = default)
    {
        var consulta = await _consultas.ObtenerPorIdAsync(consultaId, ct).ConfigureAwait(false);
        if (consulta is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("consulta.no_encontrada", "La consulta no existe."));
        }

        consulta.Anular(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}
