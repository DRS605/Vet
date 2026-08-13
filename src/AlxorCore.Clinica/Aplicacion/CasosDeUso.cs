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

/// <summary>Datos de una pauta vacunal para crear o actualizar.</summary>
public sealed record DatosPautaVacunal(
    EspecieAnimal Especie,
    string Nombre,
    CaracterVacuna Caracter = CaracterVacuna.Recomendada,
    int? EdadInicioSemanas = null,
    int? PeriodicidadRefuerzoMeses = null);

/// <summary>
/// Caso de uso: crear una pauta vacunal (cuadro maestro) en la empresa activa. La combinación
/// (empresa, especie, nombre) es única: si ya existe se devuelve <c>pauta_vacunal.duplicada</c>.
/// </summary>
public sealed class CrearPautaVacunal
{
    private readonly IRepositorioPautasVacunales _pautas;
    private readonly IConsultaPautasVacunales _consulta;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearPautaVacunal(IRepositorioPautasVacunales pautas, IConsultaPautasVacunales consulta, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _pautas = pautas;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<PautaVacunalDto>> EjecutarAsync(Guid empresaId, DatosPautaVacunal datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var pauta = PautaVacunal.Crear(
            empresaId, datos.Especie, datos.Nombre, datos.Caracter, _reloj, datos.EdadInicioSemanas, datos.PeriodicidadRefuerzoMeses);
        if (pauta.EsFallo)
        {
            return Resultado.Fallo<PautaVacunalDto>(pauta.Error);
        }

        // La unicidad se controla antes de insertar (mismo patrón que otros catálogos del ERP): el
        // índice único de la BD es la barrera final; aquí damos un error de negocio claro.
        if (await _consulta.ExisteNombreAsync(empresaId, datos.Especie, datos.Nombre.Trim(), null, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<PautaVacunalDto>(Error.Conflicto("pauta_vacunal.duplicada", "Ya existe una pauta con ese nombre para esa especie."));
        }

        _pautas.Agregar(pauta.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(PautaVacunalDto.Desde(pauta.Valor));
    }
}

/// <summary>Caso de uso: actualizar una pauta vacunal existente.</summary>
public sealed class ActualizarPautaVacunal
{
    private readonly IRepositorioPautasVacunales _pautas;
    private readonly IConsultaPautasVacunales _consulta;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarPautaVacunal(IRepositorioPautasVacunales pautas, IConsultaPautasVacunales consulta, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _pautas = pautas;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<PautaVacunalDto>> EjecutarAsync(Guid pautaId, DatosPautaVacunal datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var pauta = await _pautas.ObtenerPorIdAsync(pautaId, ct).ConfigureAwait(false);
        if (pauta is null)
        {
            return Resultado.Fallo<PautaVacunalDto>(Error.NoEncontrado("pauta_vacunal.no_encontrada", "La pauta vacunal no existe."));
        }

        if (await _consulta.ExisteNombreAsync(pauta.EmpresaId, datos.Especie, datos.Nombre.Trim(), pautaId, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<PautaVacunalDto>(Error.Conflicto("pauta_vacunal.duplicada", "Ya existe una pauta con ese nombre para esa especie."));
        }

        var actualizada = pauta.Actualizar(
            datos.Especie, datos.Nombre, datos.Caracter, _reloj, datos.EdadInicioSemanas, datos.PeriodicidadRefuerzoMeses);
        if (actualizada.EsFallo)
        {
            return Resultado.Fallo<PautaVacunalDto>(actualizada.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(PautaVacunalDto.Desde(pauta));
    }
}

/// <summary>Caso de uso: obtener una pauta vacunal por su identificador.</summary>
public sealed class ObtenerPautaVacunal
{
    private readonly IConsultaPautasVacunales _consulta;

    public ObtenerPautaVacunal(IConsultaPautasVacunales consulta) => _consulta = consulta;

    public async Task<Resultado<PautaVacunalDto>> EjecutarAsync(Guid pautaId, CancellationToken ct = default)
    {
        var pauta = await _consulta.ObtenerAsync(pautaId, ct).ConfigureAwait(false);
        return pauta is null
            ? Resultado.Fallo<PautaVacunalDto>(Error.NoEncontrado("pauta_vacunal.no_encontrada", "La pauta vacunal no existe."))
            : Resultado.Ok(pauta);
    }
}

/// <summary>Caso de uso: listar las pautas vacunales de la empresa activa, con filtro opcional por especie.</summary>
public sealed class ListarPautasVacunales
{
    private readonly IConsultaPautasVacunales _consulta;

    public ListarPautasVacunales(IConsultaPautasVacunales consulta) => _consulta = consulta;

    public Task<IReadOnlyList<PautaVacunalDto>> EjecutarAsync(Guid empresaId, EspecieAnimal? especie = null, CancellationToken ct = default) =>
        especie is { } e
            ? _consulta.ListarPorEspecieAsync(empresaId, e, incluirInactivas: false, ct)
            : _consulta.ListarAsync(empresaId, incluirInactivas: false, ct);
}

/// <summary>Caso de uso: desactivar (baja lógica) una pauta vacunal.</summary>
public sealed class DesactivarPautaVacunal
{
    private readonly IRepositorioPautasVacunales _pautas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public DesactivarPautaVacunal(IRepositorioPautasVacunales pautas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _pautas = pautas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid pautaId, CancellationToken ct = default)
    {
        var pauta = await _pautas.ObtenerPorIdAsync(pautaId, ct).ConfigureAwait(false);
        if (pauta is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("pauta_vacunal.no_encontrada", "La pauta vacunal no existe."));
        }

        pauta.Desactivar(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Datos de una vacunación para registrar.</summary>
public sealed record DatosVacunacion(
    Guid AnimalId,
    DateOnly FechaAplicacion,
    Guid? PautaVacunalId = null,
    string? Nombre = null,
    string? Lote = null,
    DateOnly? ProximaDosis = null,
    string? Veterinario = null,
    string? Notas = null);

/// <summary>Datos de una vacunación al actualizar (el animal no cambia; se toma de la vacunación existente).</summary>
public sealed record DatosActualizarVacunacion(
    DateOnly FechaAplicacion,
    Guid? PautaVacunalId = null,
    string? Nombre = null,
    string? Lote = null,
    DateOnly? ProximaDosis = null,
    string? Veterinario = null,
    string? Notas = null);

/// <summary>
/// Caso de uso: registrar una vacunación de un animal de la empresa activa. Verifica que el animal
/// existe en la empresa (vía <see cref="IConsultaAnimales"/>). Si se indica una pauta, valida que
/// existe, es de la empresa y su especie coincide con la del animal; copia el nombre de la pauta si
/// no se ha dado uno y autocalcula la próxima dosis desde la periodicidad si no se ha indicado.
/// </summary>
public sealed class RegistrarVacunacion
{
    private readonly IRepositorioVacunaciones _vacunaciones;
    private readonly IConsultaAnimales _animales;
    private readonly IRepositorioPautasVacunales _pautas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegistrarVacunacion(
        IRepositorioVacunaciones vacunaciones,
        IConsultaAnimales animales,
        IRepositorioPautasVacunales pautas,
        IUnidadDeTrabajoClinica unidadDeTrabajo,
        IReloj reloj)
    {
        _vacunaciones = vacunaciones;
        _animales = animales;
        _pautas = pautas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<VacunacionDto>> EjecutarAsync(Guid empresaId, DatosVacunacion datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // El filtro multiempresa de EF Core garantiza que solo se encuentra el animal si pertenece a la empresa activa.
        var animal = await _animales.ObtenerAsync(datos.AnimalId, ct).ConfigureAwait(false);
        if (animal is null)
        {
            return Resultado.Fallo<VacunacionDto>(Error.Validacion("vacunacion.animal_no_encontrado", "El animal no existe en esta empresa."));
        }

        var nombre = datos.Nombre;
        var proximaDosis = datos.ProximaDosis;

        if (datos.PautaVacunalId is { } pautaId)
        {
            var pauta = await _pautas.ObtenerPorIdAsync(pautaId, ct).ConfigureAwait(false);
            if (pauta is null)
            {
                return Resultado.Fallo<VacunacionDto>(Error.Validacion("vacunacion.pauta_no_encontrada", "La pauta vacunal no existe en esta empresa."));
            }

            if (pauta.Especie != animal.Especie)
            {
                return Resultado.Fallo<VacunacionDto>(Error.Validacion("vacunacion.pauta_otra_especie", "La pauta vacunal es de otra especie distinta a la del animal."));
            }

            if (string.IsNullOrWhiteSpace(nombre))
            {
                nombre = pauta.Nombre;
            }

            proximaDosis ??= PautaVacunal.CalcularProximaDosis(datos.FechaAplicacion, pauta.PeriodicidadRefuerzoMeses);
        }

        var vacunacion = Vacunacion.Crear(
            empresaId, datos.AnimalId, nombre, datos.FechaAplicacion, _reloj,
            datos.PautaVacunalId, datos.Lote, proximaDosis, datos.Veterinario, datos.Notas);
        if (vacunacion.EsFallo)
        {
            return Resultado.Fallo<VacunacionDto>(vacunacion.Error);
        }

        _vacunaciones.Agregar(vacunacion.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(VacunacionDto.Desde(vacunacion.Valor));
    }
}

/// <summary>Caso de uso: actualizar una vacunación existente (el animal no cambia).</summary>
public sealed class ActualizarVacunacion
{
    private readonly IRepositorioVacunaciones _vacunaciones;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarVacunacion(IRepositorioVacunaciones vacunaciones, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _vacunaciones = vacunaciones;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<VacunacionDto>> EjecutarAsync(Guid vacunacionId, DatosActualizarVacunacion datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var vacunacion = await _vacunaciones.ObtenerPorIdAsync(vacunacionId, ct).ConfigureAwait(false);
        if (vacunacion is null)
        {
            return Resultado.Fallo<VacunacionDto>(Error.NoEncontrado("vacunacion.no_encontrada", "La vacunación no existe."));
        }

        var actualizada = vacunacion.Actualizar(
            datos.Nombre, datos.FechaAplicacion, _reloj,
            datos.PautaVacunalId, datos.Lote, datos.ProximaDosis, datos.Veterinario, datos.Notas);
        if (actualizada.EsFallo)
        {
            return Resultado.Fallo<VacunacionDto>(actualizada.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(VacunacionDto.Desde(vacunacion));
    }
}

/// <summary>Caso de uso: obtener una vacunación por su identificador.</summary>
public sealed class ObtenerVacunacion
{
    private readonly IConsultaVacunaciones _consulta;

    public ObtenerVacunacion(IConsultaVacunaciones consulta) => _consulta = consulta;

    public async Task<Resultado<VacunacionDto>> EjecutarAsync(Guid vacunacionId, CancellationToken ct = default)
    {
        var vacunacion = await _consulta.ObtenerAsync(vacunacionId, ct).ConfigureAwait(false);
        return vacunacion is null
            ? Resultado.Fallo<VacunacionDto>(Error.NoEncontrado("vacunacion.no_encontrada", "La vacunación no existe."))
            : Resultado.Ok(vacunacion);
    }
}

/// <summary>Caso de uso: listar las vacunaciones de un animal, de la más reciente a la más antigua.</summary>
public sealed class ListarVacunacionesDeAnimal
{
    private readonly IConsultaVacunaciones _consulta;

    public ListarVacunacionesDeAnimal(IConsultaVacunaciones consulta) => _consulta = consulta;

    public Task<IReadOnlyList<VacunacionDto>> EjecutarAsync(Guid animalId, CancellationToken ct = default) =>
        _consulta.ListarPorAnimalAsync(animalId, incluirAnuladas: false, ct);
}

/// <summary>Caso de uso: listar las próximas vacunas de la empresa en una ventana de días (recordatorios).</summary>
public sealed class ListarProximasVacunas
{
    private readonly IConsultaVacunaciones _consulta;
    private readonly IReloj _reloj;

    public ListarProximasVacunas(IConsultaVacunaciones consulta, IReloj reloj)
    {
        _consulta = consulta;
        _reloj = reloj;
    }

    public Task<IReadOnlyList<VacunacionDto>> EjecutarAsync(Guid empresaId, int dias = 30, CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var hasta = hoy.AddDays(dias < 0 ? 0 : dias);
        return _consulta.ListarProximasAsync(empresaId, hoy, hasta, incluirAnuladas: false, ct);
    }
}

/// <summary>Caso de uso: anular (baja lógica) una vacunación del historial.</summary>
public sealed class AnularVacunacion
{
    private readonly IRepositorioVacunaciones _vacunaciones;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AnularVacunacion(IRepositorioVacunaciones vacunaciones, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _vacunaciones = vacunaciones;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid vacunacionId, CancellationToken ct = default)
    {
        var vacunacion = await _vacunaciones.ObtenerPorIdAsync(vacunacionId, ct).ConfigureAwait(false);
        if (vacunacion is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("vacunacion.no_encontrada", "La vacunación no existe."));
        }

        vacunacion.Anular(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}
