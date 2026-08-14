using AlxorCore.Clinica.Dominio;
using AlxorCore.Documentos.Aplicacion;
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

/// <summary>Datos de una cirugía (intervención quirúrgica) para registrar.</summary>
public sealed record DatosCirugia(
    Guid AnimalId,
    DateOnly Fecha,
    string Nombre,
    string? Descripcion = null,
    string? Cirujano = null,
    string? Anestesia = null,
    string? Complicaciones = null,
    DateOnly? ProximaRevision = null);

/// <summary>Datos de una cirugía al actualizar (el animal no cambia; se toma de la cirugía existente).</summary>
public sealed record DatosActualizarCirugia(
    DateOnly Fecha,
    string Nombre,
    string? Descripcion = null,
    string? Cirujano = null,
    string? Anestesia = null,
    string? Complicaciones = null,
    DateOnly? ProximaRevision = null);

/// <summary>
/// Caso de uso: registrar una cirugía en el historial de un animal de la empresa activa. Verifica
/// que el animal existe en la empresa (a través de <see cref="IConsultaAnimales"/>).
/// </summary>
public sealed class RegistrarCirugia
{
    private readonly IRepositorioCirugias _cirugias;
    private readonly IConsultaAnimales _animales;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegistrarCirugia(IRepositorioCirugias cirugias, IConsultaAnimales animales, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _cirugias = cirugias;
        _animales = animales;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CirugiaDto>> EjecutarAsync(Guid empresaId, DatosCirugia datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // El filtro multiempresa de EF Core garantiza que solo se encuentra el animal si pertenece a la empresa activa.
        var animal = await _animales.ObtenerAsync(datos.AnimalId, ct).ConfigureAwait(false);
        if (animal is null)
        {
            return Resultado.Fallo<CirugiaDto>(Error.Validacion("cirugia.animal_no_encontrado", "El animal no existe en esta empresa."));
        }

        var cirugia = Cirugia.Crear(
            empresaId, datos.AnimalId, datos.Fecha, datos.Nombre, _reloj,
            datos.Descripcion, datos.Cirujano, datos.Anestesia, datos.Complicaciones, datos.ProximaRevision);
        if (cirugia.EsFallo)
        {
            return Resultado.Fallo<CirugiaDto>(cirugia.Error);
        }

        _cirugias.Agregar(cirugia.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(CirugiaDto.Desde(cirugia.Valor));
    }
}

/// <summary>Caso de uso: actualizar una cirugía existente (el animal no cambia).</summary>
public sealed class ActualizarCirugia
{
    private readonly IRepositorioCirugias _cirugias;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarCirugia(IRepositorioCirugias cirugias, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _cirugias = cirugias;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CirugiaDto>> EjecutarAsync(Guid cirugiaId, DatosActualizarCirugia datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var cirugia = await _cirugias.ObtenerPorIdAsync(cirugiaId, ct).ConfigureAwait(false);
        if (cirugia is null)
        {
            return Resultado.Fallo<CirugiaDto>(Error.NoEncontrado("cirugia.no_encontrada", "La cirugía no existe."));
        }

        var actualizada = cirugia.Actualizar(
            datos.Fecha, datos.Nombre, _reloj,
            datos.Descripcion, datos.Cirujano, datos.Anestesia, datos.Complicaciones, datos.ProximaRevision);
        if (actualizada.EsFallo)
        {
            return Resultado.Fallo<CirugiaDto>(actualizada.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(CirugiaDto.Desde(cirugia));
    }
}

/// <summary>Caso de uso: obtener una cirugía por su identificador.</summary>
public sealed class ObtenerCirugia
{
    private readonly IConsultaCirugias _consulta;

    public ObtenerCirugia(IConsultaCirugias consulta) => _consulta = consulta;

    public async Task<Resultado<CirugiaDto>> EjecutarAsync(Guid cirugiaId, CancellationToken ct = default)
    {
        var cirugia = await _consulta.ObtenerAsync(cirugiaId, ct).ConfigureAwait(false);
        return cirugia is null
            ? Resultado.Fallo<CirugiaDto>(Error.NoEncontrado("cirugia.no_encontrada", "La cirugía no existe."))
            : Resultado.Ok(cirugia);
    }
}

/// <summary>Caso de uso: listar las cirugías de un animal, de la más reciente a la más antigua.</summary>
public sealed class ListarCirugiasDeAnimal
{
    private readonly IConsultaCirugias _consulta;

    public ListarCirugiasDeAnimal(IConsultaCirugias consulta) => _consulta = consulta;

    public Task<IReadOnlyList<CirugiaDto>> EjecutarAsync(Guid animalId, CancellationToken ct = default) =>
        _consulta.ListarPorAnimalAsync(animalId, incluirAnuladas: false, ct);
}

/// <summary>Caso de uso: listar las próximas revisiones quirúrgicas de la empresa en una ventana de días (recordatorios).</summary>
public sealed class ListarProximasRevisiones
{
    private readonly IConsultaCirugias _consulta;
    private readonly IReloj _reloj;

    public ListarProximasRevisiones(IConsultaCirugias consulta, IReloj reloj)
    {
        _consulta = consulta;
        _reloj = reloj;
    }

    public Task<IReadOnlyList<CirugiaDto>> EjecutarAsync(Guid empresaId, int dias = 30, CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var hasta = hoy.AddDays(dias < 0 ? 0 : dias);
        return _consulta.ListarProximasRevisionesAsync(empresaId, hoy, hasta, incluirAnuladas: false, ct);
    }
}

/// <summary>Caso de uso: anular (baja lógica) una cirugía del historial.</summary>
public sealed class AnularCirugia
{
    private readonly IRepositorioCirugias _cirugias;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AnularCirugia(IRepositorioCirugias cirugias, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _cirugias = cirugias;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid cirugiaId, CancellationToken ct = default)
    {
        var cirugia = await _cirugias.ObtenerPorIdAsync(cirugiaId, ct).ConfigureAwait(false);
        if (cirugia is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("cirugia.no_encontrada", "La cirugía no existe."));
        }

        cirugia.Anular(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Datos de un recordatorio para crear (manual o desde un vencimiento).</summary>
public sealed record DatosRecordatorio(
    Guid AnimalId,
    TipoRecordatorio Tipo,
    string Titulo,
    DateOnly FechaObjetivo,
    string? Notas = null,
    string? ReferenciaTipo = null,
    Guid? ReferenciaId = null);

/// <summary>Datos de un recordatorio al actualizar (el animal, el tipo y la referencia no cambian).</summary>
public sealed record DatosActualizarRecordatorio(
    string Titulo,
    DateOnly FechaObjetivo,
    string? Notas = null);

/// <summary>Fallo del envío de un recordatorio dentro de un envío por lotes.</summary>
public sealed record FalloEnvioRecordatorio(Guid RecordatorioId, string Codigo, string Mensaje);

/// <summary>Resumen del envío de recordatorios pendientes.</summary>
public sealed record ResumenEnvioRecordatorios(int Enviados, IReadOnlyList<FalloEnvioRecordatorio> Fallidos);

/// <summary>
/// Caso de uso: crear un recordatorio manual para un animal de la empresa activa. Verifica que el
/// animal existe en la empresa (a través de <see cref="IConsultaAnimales"/>).
/// </summary>
public sealed class CrearRecordatorio
{
    private readonly IRepositorioRecordatorios _recordatorios;
    private readonly IConsultaAnimales _animales;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearRecordatorio(IRepositorioRecordatorios recordatorios, IConsultaAnimales animales, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _recordatorios = recordatorios;
        _animales = animales;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<RecordatorioDto>> EjecutarAsync(Guid empresaId, DatosRecordatorio datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // El filtro multiempresa de EF Core garantiza que solo se encuentra el animal si pertenece a la empresa activa.
        var animal = await _animales.ObtenerAsync(datos.AnimalId, ct).ConfigureAwait(false);
        if (animal is null)
        {
            return Resultado.Fallo<RecordatorioDto>(Error.Validacion("recordatorio.animal_no_encontrado", "El animal no existe en esta empresa."));
        }

        var recordatorio = Recordatorio.Crear(
            empresaId, datos.AnimalId, datos.Tipo, datos.Titulo, datos.FechaObjetivo, _reloj,
            datos.Notas, datos.ReferenciaTipo, datos.ReferenciaId);
        if (recordatorio.EsFallo)
        {
            return Resultado.Fallo<RecordatorioDto>(recordatorio.Error);
        }

        _recordatorios.Agregar(recordatorio.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(RecordatorioDto.Desde(recordatorio.Valor));
    }
}

/// <summary>
/// Caso de uso: generar recordatorios reuniendo lo que vence en una ventana de días —vacunas
/// (<see cref="IConsultaVacunaciones.ListarProximasAsync"/>) y revisiones de cirugía
/// (<see cref="IConsultaCirugias.ListarProximasRevisionesAsync"/>)—. Por cada vencimiento que aún
/// no tenga un recordatorio (deduplicado por <c>ReferenciaTipo</c> + <c>ReferenciaId</c>) crea uno
/// pendiente. Guarda todos en una única unidad de trabajo y devuelve el número creado.
/// </summary>
public sealed class GenerarRecordatorios
{
    /// <summary>Referencia de origen de un recordatorio nacido de una vacunación.</summary>
    public const string ReferenciaVacunacion = "vacunacion";

    /// <summary>Referencia de origen de un recordatorio nacido de una revisión de cirugía.</summary>
    public const string ReferenciaCirugia = "cirugia";

    private readonly IRepositorioRecordatorios _recordatorios;
    private readonly IConsultaRecordatorios _consultaRecordatorios;
    private readonly IConsultaVacunaciones _vacunaciones;
    private readonly IConsultaCirugias _cirugias;
    private readonly IConsultaAnimales _animales;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public GenerarRecordatorios(
        IRepositorioRecordatorios recordatorios,
        IConsultaRecordatorios consultaRecordatorios,
        IConsultaVacunaciones vacunaciones,
        IConsultaCirugias cirugias,
        IConsultaAnimales animales,
        IUnidadDeTrabajoClinica unidadDeTrabajo,
        IReloj reloj)
    {
        _recordatorios = recordatorios;
        _consultaRecordatorios = consultaRecordatorios;
        _vacunaciones = vacunaciones;
        _cirugias = cirugias;
        _animales = animales;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<int>> EjecutarAsync(Guid empresaId, int ventanaDias = 30, CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var hasta = hoy.AddDays(ventanaDias < 0 ? 0 : ventanaDias);

        var nombresAnimales = new Dictionary<Guid, string>();
        var creados = 0;

        var vacunas = await _vacunaciones.ListarProximasAsync(empresaId, hoy, hasta, incluirAnuladas: false, ct).ConfigureAwait(false);
        foreach (var vacuna in vacunas)
        {
            if (vacuna.ProximaDosis is not { } fecha)
            {
                continue;
            }

            if (await _consultaRecordatorios.ExisteConReferenciaAsync(empresaId, ReferenciaVacunacion, vacuna.Id, ct).ConfigureAwait(false))
            {
                continue;
            }

            var nombre = await ResolverNombreAnimalAsync(vacuna.AnimalId, nombresAnimales, ct).ConfigureAwait(false);
            var titulo = $"{vacuna.Nombre} de {nombre}";
            var recordatorio = Recordatorio.Crear(
                empresaId, vacuna.AnimalId, TipoRecordatorio.Vacuna, titulo, fecha, _reloj,
                referenciaTipo: ReferenciaVacunacion, referenciaId: vacuna.Id);
            if (recordatorio.EsCorrecto)
            {
                _recordatorios.Agregar(recordatorio.Valor);
                creados++;
            }
        }

        var revisiones = await _cirugias.ListarProximasRevisionesAsync(empresaId, hoy, hasta, incluirAnuladas: false, ct).ConfigureAwait(false);
        foreach (var cirugia in revisiones)
        {
            if (cirugia.ProximaRevision is not { } fecha)
            {
                continue;
            }

            if (await _consultaRecordatorios.ExisteConReferenciaAsync(empresaId, ReferenciaCirugia, cirugia.Id, ct).ConfigureAwait(false))
            {
                continue;
            }

            var nombre = await ResolverNombreAnimalAsync(cirugia.AnimalId, nombresAnimales, ct).ConfigureAwait(false);
            var titulo = $"Revisión de {cirugia.Nombre} de {nombre}";
            var recordatorio = Recordatorio.Crear(
                empresaId, cirugia.AnimalId, TipoRecordatorio.Revision, titulo, fecha, _reloj,
                referenciaTipo: ReferenciaCirugia, referenciaId: cirugia.Id);
            if (recordatorio.EsCorrecto)
            {
                _recordatorios.Agregar(recordatorio.Valor);
                creados++;
            }
        }

        if (creados > 0)
        {
            await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        }

        return Resultado.Ok(creados);
    }

    private async Task<string> ResolverNombreAnimalAsync(Guid animalId, Dictionary<Guid, string> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(animalId, out var nombre))
        {
            return nombre;
        }

        var animal = await _animales.ObtenerAsync(animalId, ct).ConfigureAwait(false);
        nombre = animal?.Nombre ?? "su mascota";
        cache[animalId] = nombre;
        return nombre;
    }
}

/// <summary>
/// Caso de uso: enviar un recordatorio por correo al propietario del animal. Resuelve
/// animal → clienteId → email (vía <see cref="IConsultaAnimales"/> + <see cref="IConsultaClientes"/>),
/// compone un mensaje en español y lo envía por el puerto de correo del módulo Documentos
/// (<see cref="IServicioCorreo"/>), el mismo que usa Facturación. Después marca el recordatorio como
/// enviado. Si el cliente no tiene email, devuelve <c>recordatorio.sin_email</c>.
/// </summary>
public sealed class EnviarRecordatorio
{
    private readonly IRepositorioRecordatorios _recordatorios;
    private readonly IConsultaAnimales _animales;
    private readonly IConsultaClientes _clientes;
    private readonly IServicioCorreo _correo;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public EnviarRecordatorio(
        IRepositorioRecordatorios recordatorios,
        IConsultaAnimales animales,
        IConsultaClientes clientes,
        IServicioCorreo correo,
        IUnidadDeTrabajoClinica unidadDeTrabajo,
        IReloj reloj)
    {
        _recordatorios = recordatorios;
        _animales = animales;
        _clientes = clientes;
        _correo = correo;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid recordatorioId, CancellationToken ct = default)
    {
        var recordatorio = await _recordatorios.ObtenerPorIdAsync(recordatorioId, ct).ConfigureAwait(false);
        if (recordatorio is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("recordatorio.no_encontrado", "El recordatorio no existe."));
        }

        var animal = await _animales.ObtenerAsync(recordatorio.AnimalId, ct).ConfigureAwait(false);
        if (animal is null)
        {
            return Resultado.Fallo(Error.Validacion("recordatorio.animal_no_encontrado", "El animal del recordatorio no existe en esta empresa."));
        }

        var cliente = await _clientes.ObtenerAsync(animal.ClienteId, ct).ConfigureAwait(false);
        if (cliente is null || string.IsNullOrWhiteSpace(cliente.Email))
        {
            return Resultado.Fallo(Error.Validacion("recordatorio.sin_email", "El propietario del animal no tiene un correo electrónico configurado."));
        }

        // Se marca antes de enviar para no reenviar un recordatorio que no esté pendiente.
        var marcado = recordatorio.MarcarEnviado(_reloj);
        if (marcado.EsFallo)
        {
            return Resultado.Fallo(marcado.Error);
        }

        var mensaje = new MensajeCorreo(
            cliente.Email.Trim(),
            recordatorio.Titulo,
            ComponerCuerpo(recordatorio, animal.Nombre),
            Array.Empty<byte>(),
            string.Empty);

        await _correo.EnviarAsync(mensaje, ct).ConfigureAwait(false);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }

    private static string ComponerCuerpo(Recordatorio recordatorio, string nombreAnimal)
    {
        var motivo = recordatorio.Tipo switch
        {
            TipoRecordatorio.Vacuna => "una vacuna",
            TipoRecordatorio.Revision => "una revisión",
            TipoRecordatorio.Tratamiento => "un tratamiento",
            TipoRecordatorio.Cirugia => "un seguimiento",
            _ => "una cita",
        };

        var cuerpo =
            $"Hola,\n\n" +
            $"Le recordamos que su mascota {nombreAnimal} tiene {motivo} pendiente: {recordatorio.Titulo}.\n" +
            $"Fecha prevista: {recordatorio.FechaObjetivo:dd/MM/yyyy}.\n\n";

        if (!string.IsNullOrWhiteSpace(recordatorio.Notas))
        {
            cuerpo += $"{recordatorio.Notas}\n\n";
        }

        cuerpo += "Por favor, póngase en contacto con nosotros para concertar la cita. Un cordial saludo.";
        return cuerpo;
    }
}

/// <summary>
/// Caso de uso: enviar todos los recordatorios pendientes con fecha objetivo hasta la indicada.
/// Envía cada uno reutilizando <see cref="EnviarRecordatorio"/>; si alguno falla (por ejemplo, por
/// falta de email) no aborta el lote: lo salta y lo anota. Devuelve un resumen (enviados y fallidos).
/// </summary>
public sealed class EnviarRecordatoriosPendientes
{
    private readonly IConsultaRecordatorios _consulta;
    private readonly EnviarRecordatorio _enviar;
    private readonly IReloj _reloj;

    public EnviarRecordatoriosPendientes(IConsultaRecordatorios consulta, EnviarRecordatorio enviar, IReloj reloj)
    {
        _consulta = consulta;
        _enviar = enviar;
        _reloj = reloj;
    }

    public async Task<Resultado<ResumenEnvioRecordatorios>> EjecutarAsync(Guid empresaId, DateOnly? hasta = null, CancellationToken ct = default)
    {
        var limite = hasta ?? DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime).AddDays(30);
        var pendientes = await _consulta.ListarPendientesAsync(empresaId, limite, ct).ConfigureAwait(false);

        var enviados = 0;
        var fallidos = new List<FalloEnvioRecordatorio>();

        foreach (var pendiente in pendientes)
        {
            var resultado = await _enviar.EjecutarAsync(pendiente.Id, ct).ConfigureAwait(false);
            if (resultado.EsCorrecto)
            {
                enviados++;
            }
            else
            {
                fallidos.Add(new FalloEnvioRecordatorio(pendiente.Id, resultado.Error.Codigo, resultado.Error.Mensaje));
            }
        }

        return Resultado.Ok(new ResumenEnvioRecordatorios(enviados, fallidos));
    }
}

/// <summary>Caso de uso: actualizar el asunto, la fecha objetivo y las notas de un recordatorio.</summary>
public sealed class ActualizarRecordatorio
{
    private readonly IRepositorioRecordatorios _recordatorios;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarRecordatorio(IRepositorioRecordatorios recordatorios, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _recordatorios = recordatorios;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<RecordatorioDto>> EjecutarAsync(Guid recordatorioId, DatosActualizarRecordatorio datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var recordatorio = await _recordatorios.ObtenerPorIdAsync(recordatorioId, ct).ConfigureAwait(false);
        if (recordatorio is null)
        {
            return Resultado.Fallo<RecordatorioDto>(Error.NoEncontrado("recordatorio.no_encontrado", "El recordatorio no existe."));
        }

        var actualizado = recordatorio.Actualizar(datos.Titulo, datos.FechaObjetivo, datos.Notas, _reloj);
        if (actualizado.EsFallo)
        {
            return Resultado.Fallo<RecordatorioDto>(actualizado.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(RecordatorioDto.Desde(recordatorio));
    }
}

/// <summary>Caso de uso: marcar un recordatorio como completado (atendido).</summary>
public sealed class CompletarRecordatorio
{
    private readonly IRepositorioRecordatorios _recordatorios;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CompletarRecordatorio(IRepositorioRecordatorios recordatorios, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _recordatorios = recordatorios;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid recordatorioId, CancellationToken ct = default)
    {
        var recordatorio = await _recordatorios.ObtenerPorIdAsync(recordatorioId, ct).ConfigureAwait(false);
        if (recordatorio is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("recordatorio.no_encontrado", "El recordatorio no existe."));
        }

        var completado = recordatorio.MarcarCompletado(_reloj);
        if (completado.EsFallo)
        {
            return Resultado.Fallo(completado.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: cancelar un recordatorio (deja de proceder).</summary>
public sealed class CancelarRecordatorio
{
    private readonly IRepositorioRecordatorios _recordatorios;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CancelarRecordatorio(IRepositorioRecordatorios recordatorios, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _recordatorios = recordatorios;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid recordatorioId, CancellationToken ct = default)
    {
        var recordatorio = await _recordatorios.ObtenerPorIdAsync(recordatorioId, ct).ConfigureAwait(false);
        if (recordatorio is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("recordatorio.no_encontrado", "El recordatorio no existe."));
        }

        var cancelado = recordatorio.Cancelar(_reloj);
        if (cancelado.EsFallo)
        {
            return Resultado.Fallo(cancelado.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: obtener un recordatorio por su identificador.</summary>
public sealed class ObtenerRecordatorio
{
    private readonly IConsultaRecordatorios _consulta;

    public ObtenerRecordatorio(IConsultaRecordatorios consulta) => _consulta = consulta;

    public async Task<Resultado<RecordatorioDto>> EjecutarAsync(Guid recordatorioId, CancellationToken ct = default)
    {
        var recordatorio = await _consulta.ObtenerAsync(recordatorioId, ct).ConfigureAwait(false);
        return recordatorio is null
            ? Resultado.Fallo<RecordatorioDto>(Error.NoEncontrado("recordatorio.no_encontrado", "El recordatorio no existe."))
            : Resultado.Ok(recordatorio);
    }
}

/// <summary>Caso de uso: listar los recordatorios de la empresa, con filtros por estado y por ventana de días.</summary>
public sealed class ListarRecordatorios
{
    private readonly IConsultaRecordatorios _consulta;
    private readonly IReloj _reloj;

    public ListarRecordatorios(IConsultaRecordatorios consulta, IReloj reloj)
    {
        _consulta = consulta;
        _reloj = reloj;
    }

    public Task<IReadOnlyList<RecordatorioDto>> EjecutarAsync(Guid empresaId, EstadoRecordatorio? estado = null, int? dias = null, CancellationToken ct = default)
    {
        DateOnly? hasta = null;
        if (dias is { } d)
        {
            var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
            hasta = hoy.AddDays(d < 0 ? 0 : d);
        }

        return _consulta.ListarAsync(empresaId, estado, desde: null, hasta: hasta, ct);
    }
}

/// <summary>Datos de una cita para crear (una entrada de la agenda).</summary>
public sealed record DatosCita(
    Guid AnimalId,
    DateTimeOffset Inicio,
    int? DuracionMinutos = null,
    TipoCita? Tipo = null,
    string? Motivo = null,
    string? Veterinario = null,
    string? Notas = null);

/// <summary>Datos de una cita al actualizar (el animal no cambia; se toma de la cita existente). No altera el estado.</summary>
public sealed record DatosActualizarCita(
    DateTimeOffset Inicio,
    int? DuracionMinutos = null,
    TipoCita? Tipo = null,
    string? Motivo = null,
    string? Veterinario = null,
    string? Notas = null);

/// <summary>Datos para reprogramar una cita (nuevo inicio y, opcionalmente, nueva duración).</summary>
public sealed record DatosReprogramarCita(DateTimeOffset Inicio, int? DuracionMinutos = null);

/// <summary>
/// Caso de uso: crear una cita para un animal de la empresa activa. Verifica que el animal existe en
/// la empresa (a través de <see cref="IConsultaAnimales"/>).
/// </summary>
public sealed class CrearCita
{
    private readonly IRepositorioCitas _citas;
    private readonly IConsultaAnimales _animales;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearCita(IRepositorioCitas citas, IConsultaAnimales animales, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _citas = citas;
        _animales = animales;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CitaDto>> EjecutarAsync(Guid empresaId, DatosCita datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // El filtro multiempresa de EF Core garantiza que solo se encuentra el animal si pertenece a la empresa activa.
        var animal = await _animales.ObtenerAsync(datos.AnimalId, ct).ConfigureAwait(false);
        if (animal is null)
        {
            return Resultado.Fallo<CitaDto>(Error.Validacion("cita.animal_no_encontrado", "El animal no existe en esta empresa."));
        }

        var cita = Cita.Crear(
            empresaId, datos.AnimalId, datos.Inicio, _reloj,
            datos.DuracionMinutos ?? Cita.DuracionPorDefectoMinutos, datos.Tipo ?? TipoCita.Consulta,
            datos.Motivo, datos.Veterinario, datos.Notas);
        if (cita.EsFallo)
        {
            return Resultado.Fallo<CitaDto>(cita.Error);
        }

        _citas.Agregar(cita.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(CitaDto.Desde(cita.Valor));
    }
}

/// <summary>Caso de uso: actualizar los datos de una cita existente (el animal y el estado no cambian).</summary>
public sealed class ActualizarCita
{
    private readonly IRepositorioCitas _citas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarCita(IRepositorioCitas citas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _citas = citas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CitaDto>> EjecutarAsync(Guid citaId, DatosActualizarCita datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var cita = await _citas.ObtenerPorIdAsync(citaId, ct).ConfigureAwait(false);
        if (cita is null)
        {
            return Resultado.Fallo<CitaDto>(Error.NoEncontrado("cita.no_encontrada", "La cita no existe."));
        }

        var actualizada = cita.Actualizar(
            datos.Inicio, datos.DuracionMinutos ?? cita.DuracionMinutos, datos.Tipo ?? cita.Tipo, _reloj,
            datos.Motivo, datos.Veterinario, datos.Notas);
        if (actualizada.EsFallo)
        {
            return Resultado.Fallo<CitaDto>(actualizada.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(CitaDto.Desde(cita));
    }
}

/// <summary>Caso de uso: confirmar una cita (transición Solicitada → Confirmada).</summary>
public sealed class ConfirmarCita
{
    private readonly IRepositorioCitas _citas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ConfirmarCita(IRepositorioCitas citas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _citas = citas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CitaDto>> EjecutarAsync(Guid citaId, CancellationToken ct = default)
    {
        var cita = await _citas.ObtenerPorIdAsync(citaId, ct).ConfigureAwait(false);
        if (cita is null)
        {
            return Resultado.Fallo<CitaDto>(Error.NoEncontrado("cita.no_encontrada", "La cita no existe."));
        }

        var confirmada = cita.Confirmar(_reloj);
        if (confirmada.EsFallo)
        {
            return Resultado.Fallo<CitaDto>(confirmada.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(CitaDto.Desde(cita));
    }
}

/// <summary>Caso de uso: reprogramar una cita a un nuevo inicio (y, opcionalmente, nueva duración).</summary>
public sealed class ReprogramarCita
{
    private readonly IRepositorioCitas _citas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ReprogramarCita(IRepositorioCitas citas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _citas = citas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CitaDto>> EjecutarAsync(Guid citaId, DatosReprogramarCita datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var cita = await _citas.ObtenerPorIdAsync(citaId, ct).ConfigureAwait(false);
        if (cita is null)
        {
            return Resultado.Fallo<CitaDto>(Error.NoEncontrado("cita.no_encontrada", "La cita no existe."));
        }

        var reprogramada = cita.Reprogramar(datos.Inicio, datos.DuracionMinutos, _reloj);
        if (reprogramada.EsFallo)
        {
            return Resultado.Fallo<CitaDto>(reprogramada.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(CitaDto.Desde(cita));
    }
}

/// <summary>Caso de uso: marcar una cita como atendida.</summary>
public sealed class AtenderCita
{
    private readonly IRepositorioCitas _citas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AtenderCita(IRepositorioCitas citas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _citas = citas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CitaDto>> EjecutarAsync(Guid citaId, CancellationToken ct = default)
    {
        var cita = await _citas.ObtenerPorIdAsync(citaId, ct).ConfigureAwait(false);
        if (cita is null)
        {
            return Resultado.Fallo<CitaDto>(Error.NoEncontrado("cita.no_encontrada", "La cita no existe."));
        }

        var atendida = cita.Atender(_reloj);
        if (atendida.EsFallo)
        {
            return Resultado.Fallo<CitaDto>(atendida.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(CitaDto.Desde(cita));
    }
}

/// <summary>Caso de uso: marcar una cita como no presentado.</summary>
public sealed class MarcarNoPresentado
{
    private readonly IRepositorioCitas _citas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public MarcarNoPresentado(IRepositorioCitas citas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _citas = citas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CitaDto>> EjecutarAsync(Guid citaId, CancellationToken ct = default)
    {
        var cita = await _citas.ObtenerPorIdAsync(citaId, ct).ConfigureAwait(false);
        if (cita is null)
        {
            return Resultado.Fallo<CitaDto>(Error.NoEncontrado("cita.no_encontrada", "La cita no existe."));
        }

        var marcada = cita.MarcarNoPresentado(_reloj);
        if (marcada.EsFallo)
        {
            return Resultado.Fallo<CitaDto>(marcada.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(CitaDto.Desde(cita));
    }
}

/// <summary>Caso de uso: cancelar una cita.</summary>
public sealed class CancelarCita
{
    private readonly IRepositorioCitas _citas;
    private readonly IUnidadDeTrabajoClinica _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CancelarCita(IRepositorioCitas citas, IUnidadDeTrabajoClinica unidadDeTrabajo, IReloj reloj)
    {
        _citas = citas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid citaId, CancellationToken ct = default)
    {
        var cita = await _citas.ObtenerPorIdAsync(citaId, ct).ConfigureAwait(false);
        if (cita is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("cita.no_encontrada", "La cita no existe."));
        }

        var cancelada = cita.Cancelar(_reloj);
        if (cancelada.EsFallo)
        {
            return Resultado.Fallo(cancelada.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: obtener una cita por su identificador.</summary>
public sealed class ObtenerCita
{
    private readonly IConsultaCitas _consulta;

    public ObtenerCita(IConsultaCitas consulta) => _consulta = consulta;

    public async Task<Resultado<CitaDto>> EjecutarAsync(Guid citaId, CancellationToken ct = default)
    {
        var cita = await _consulta.ObtenerAsync(citaId, ct).ConfigureAwait(false);
        return cita is null
            ? Resultado.Fallo<CitaDto>(Error.NoEncontrado("cita.no_encontrada", "La cita no existe."))
            : Resultado.Ok(cita);
    }
}

/// <summary>Caso de uso: listar las citas de un animal (excluye las canceladas), de la más reciente a la más antigua.</summary>
public sealed class ListarCitasDeAnimal
{
    private readonly IConsultaCitas _consulta;

    public ListarCitasDeAnimal(IConsultaCitas consulta) => _consulta = consulta;

    public Task<IReadOnlyList<CitaDto>> EjecutarAsync(Guid animalId, bool incluirCanceladas = false, CancellationToken ct = default) =>
        _consulta.ListarPorAnimalAsync(animalId, incluirCanceladas, ct);
}

/// <summary>Caso de uso: la agenda de la empresa en un rango, con filtros por estado y veterinario.</summary>
public sealed class ListarAgenda
{
    private readonly IConsultaCitas _consulta;

    public ListarAgenda(IConsultaCitas consulta) => _consulta = consulta;

    public Task<IReadOnlyList<CitaDto>> EjecutarAsync(Guid empresaId, DateTimeOffset desde, DateTimeOffset hasta, EstadoCita? estado = null, string? veterinario = null, CancellationToken ct = default) =>
        _consulta.ListarAgendaAsync(empresaId, desde, hasta, estado, veterinario, ct);
}

/// <summary>Caso de uso: KPI de confirmación de citas de una ventana (resumen).</summary>
public sealed class ResumenCitas
{
    private readonly IConsultaCitas _consulta;

    public ResumenCitas(IConsultaCitas consulta) => _consulta = consulta;

    public Task<ResumenCitasDto> EjecutarAsync(Guid empresaId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct = default) =>
        _consulta.ResumenAsync(empresaId, desde, hasta, ct);
}

/// <summary>Caso de uso: serie mensual de confirmación de citas (para el gráfico del panel).</summary>
public sealed class ConfirmacionMensual
{
    private readonly IConsultaCitas _consulta;
    private readonly IReloj _reloj;

    public ConfirmacionMensual(IConsultaCitas consulta, IReloj reloj)
    {
        _consulta = consulta;
        _reloj = reloj;
    }

    public Task<IReadOnlyList<PuntoConfirmacionMensualDto>> EjecutarAsync(Guid empresaId, int meses = 6, CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        return _consulta.ConfirmacionMensualAsync(empresaId, meses < 1 ? 1 : meses, hoy, ct);
    }
}
