using AlxorCore.Api.Comun;
using AlxorCore.Clinica.Aplicacion;
using AlxorCore.Clinica.Dominio;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Clínica (animales del producto veterinario).</summary>
public static class EndpointsClinica
{
    public static IEndpointRouteBuilder MapearClinica(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var animales = rutas.MapGroup("/animales").WithTags("Animales");

        animales.MapGet("", ListarAsync)
            .WithSummary("Lista los animales de la empresa activa.")
            .RequierePermiso(Permisos.AnimalLeer);

        animales.MapGet("/{id:guid}", ObtenerAsync)
            .WithSummary("Obtiene un animal.")
            .RequierePermiso(Permisos.AnimalLeer);

        animales.MapPost("", CrearAsync)
            .WithSummary("Crea un animal (mascota).")
            .RequierePermiso(Permisos.AnimalGestionar);

        animales.MapPut("/{id:guid}", ActualizarAsync)
            .WithSummary("Actualiza un animal.")
            .RequierePermiso(Permisos.AnimalGestionar);

        animales.MapDelete("/{id:guid}", DarDeBajaAsync)
            .WithSummary("Da de baja (baja lógica) un animal.")
            .RequierePermiso(Permisos.AnimalGestionar);

        var clientes = rutas.MapGroup("/clientes").WithTags("Animales");

        clientes.MapGet("/{clienteId:guid}/animales", ListarPorClienteAsync)
            .WithSummary("Lista los animales de un cliente.")
            .RequierePermiso(Permisos.AnimalLeer);

        animales.MapGet("/{animalId:guid}/consultas", ListarConsultasAsync)
            .WithSummary("Lista el historial clínico (consultas) de un animal.")
            .RequierePermiso(Permisos.ConsultaLeer);

        animales.MapPost("/{animalId:guid}/consultas", RegistrarConsultaAsync)
            .WithSummary("Registra una consulta en el historial de un animal.")
            .RequierePermiso(Permisos.ConsultaGestionar);

        var consultas = rutas.MapGroup("/consultas").WithTags("Consultas");

        consultas.MapGet("/{id:guid}", ObtenerConsultaAsync)
            .WithSummary("Obtiene una consulta.")
            .RequierePermiso(Permisos.ConsultaLeer);

        consultas.MapPut("/{id:guid}", ActualizarConsultaAsync)
            .WithSummary("Actualiza una consulta.")
            .RequierePermiso(Permisos.ConsultaGestionar);

        consultas.MapDelete("/{id:guid}", AnularConsultaAsync)
            .WithSummary("Anula (baja lógica) una consulta.")
            .RequierePermiso(Permisos.ConsultaGestionar);

        var vacunas = rutas.MapGroup("/vacunas").WithTags("Vacunas");

        vacunas.MapGet("/pautas", ListarPautasAsync)
            .WithSummary("Lista las pautas vacunales (cuadro maestro), con filtro opcional por especie.")
            .RequierePermiso(Permisos.VacunaLeer);

        vacunas.MapPost("/pautas", CrearPautaAsync)
            .WithSummary("Crea una pauta vacunal (cuadro maestro por especie).")
            .RequierePermiso(Permisos.VacunaGestionar);

        vacunas.MapGet("/pautas/{id:guid}", ObtenerPautaAsync)
            .WithSummary("Obtiene una pauta vacunal.")
            .RequierePermiso(Permisos.VacunaLeer);

        vacunas.MapPut("/pautas/{id:guid}", ActualizarPautaAsync)
            .WithSummary("Actualiza una pauta vacunal.")
            .RequierePermiso(Permisos.VacunaGestionar);

        vacunas.MapDelete("/pautas/{id:guid}", DesactivarPautaAsync)
            .WithSummary("Desactiva (baja lógica) una pauta vacunal.")
            .RequierePermiso(Permisos.VacunaGestionar);

        vacunas.MapGet("/proximas", ListarProximasVacunasAsync)
            .WithSummary("Lista las próximas dosis de vacuna de la empresa en la ventana indicada.")
            .RequierePermiso(Permisos.VacunaLeer);

        vacunas.MapGet("/{id:guid}", ObtenerVacunacionAsync)
            .WithSummary("Obtiene una vacunación.")
            .RequierePermiso(Permisos.VacunaLeer);

        vacunas.MapPut("/{id:guid}", ActualizarVacunacionAsync)
            .WithSummary("Actualiza una vacunación.")
            .RequierePermiso(Permisos.VacunaGestionar);

        vacunas.MapDelete("/{id:guid}", AnularVacunacionAsync)
            .WithSummary("Anula (baja lógica) una vacunación.")
            .RequierePermiso(Permisos.VacunaGestionar);

        animales.MapGet("/{animalId:guid}/vacunas", ListarVacunasAsync)
            .WithSummary("Lista las vacunaciones de un animal.")
            .RequierePermiso(Permisos.VacunaLeer);

        animales.MapPost("/{animalId:guid}/vacunas", RegistrarVacunacionAsync)
            .WithSummary("Registra una vacunación de un animal.")
            .RequierePermiso(Permisos.VacunaGestionar);

        var cirugias = rutas.MapGroup("/cirugias").WithTags("Cirugías");

        cirugias.MapGet("/proximas-revisiones", ListarProximasRevisionesAsync)
            .WithSummary("Lista las próximas revisiones quirúrgicas de la empresa en la ventana indicada.")
            .RequierePermiso(Permisos.CirugiaLeer);

        cirugias.MapGet("/{id:guid}", ObtenerCirugiaAsync)
            .WithSummary("Obtiene una cirugía.")
            .RequierePermiso(Permisos.CirugiaLeer);

        cirugias.MapPut("/{id:guid}", ActualizarCirugiaAsync)
            .WithSummary("Actualiza una cirugía.")
            .RequierePermiso(Permisos.CirugiaGestionar);

        cirugias.MapDelete("/{id:guid}", AnularCirugiaAsync)
            .WithSummary("Anula (baja lógica) una cirugía.")
            .RequierePermiso(Permisos.CirugiaGestionar);

        animales.MapGet("/{animalId:guid}/cirugias", ListarCirugiasAsync)
            .WithSummary("Lista el historial de cirugías de un animal.")
            .RequierePermiso(Permisos.CirugiaLeer);

        animales.MapPost("/{animalId:guid}/cirugias", RegistrarCirugiaAsync)
            .WithSummary("Registra una cirugía de un animal.")
            .RequierePermiso(Permisos.CirugiaGestionar);

        var recordatorios = rutas.MapGroup("/recordatorios").WithTags("Recordatorios");

        recordatorios.MapGet("", ListarRecordatoriosAsync)
            .WithSummary("Lista los recordatorios de la empresa, con filtros opcionales por estado y ventana de días.")
            .RequierePermiso(Permisos.RecordatorioLeer);

        recordatorios.MapPost("", CrearRecordatorioAsync)
            .WithSummary("Crea un recordatorio manual para un animal.")
            .RequierePermiso(Permisos.RecordatorioGestionar);

        recordatorios.MapPost("/generar", GenerarRecordatoriosAsync)
            .WithSummary("Genera recordatorios a partir de los vencimientos (vacunas y revisiones) de la ventana indicada.")
            .RequierePermiso(Permisos.RecordatorioGestionar);

        recordatorios.MapPost("/enviar-pendientes", EnviarPendientesAsync)
            .WithSummary("Envía por correo todos los recordatorios pendientes hasta la ventana indicada.")
            .RequierePermiso(Permisos.RecordatorioGestionar);

        recordatorios.MapGet("/{id:guid}", ObtenerRecordatorioAsync)
            .WithSummary("Obtiene un recordatorio.")
            .RequierePermiso(Permisos.RecordatorioLeer);

        recordatorios.MapPut("/{id:guid}", ActualizarRecordatorioAsync)
            .WithSummary("Actualiza el asunto, la fecha objetivo y las notas de un recordatorio.")
            .RequierePermiso(Permisos.RecordatorioGestionar);

        recordatorios.MapPost("/{id:guid}/enviar", EnviarRecordatorioAsync)
            .WithSummary("Envía por correo un recordatorio al propietario del animal.")
            .RequierePermiso(Permisos.RecordatorioGestionar);

        recordatorios.MapPost("/{id:guid}/completar", CompletarRecordatorioAsync)
            .WithSummary("Marca un recordatorio como completado (atendido).")
            .RequierePermiso(Permisos.RecordatorioGestionar);

        recordatorios.MapDelete("/{id:guid}", CancelarRecordatorioAsync)
            .WithSummary("Cancela un recordatorio.")
            .RequierePermiso(Permisos.RecordatorioGestionar);

        rutas.MapGet("/agenda", ListarAgendaAsync)
            .WithTags("Citas")
            .WithSummary("Agenda: citas de la empresa en un rango, con filtros por estado y veterinario.")
            .RequierePermiso(Permisos.CitaLeer);

        var citas = rutas.MapGroup("/citas").WithTags("Citas");

        citas.MapPost("", CrearCitaAsync)
            .WithSummary("Crea una cita (entrada de la agenda).")
            .RequierePermiso(Permisos.CitaGestionar);

        citas.MapGet("/kpi", ResumenCitasAsync)
            .WithSummary("KPI de confirmación de citas en un rango (resumen).")
            .RequierePermiso(Permisos.CitaLeer);

        citas.MapGet("/kpi/confirmacion-mensual", ConfirmacionMensualAsync)
            .WithSummary("Serie mensual de confirmación de citas (para el gráfico del panel).")
            .RequierePermiso(Permisos.CitaLeer);

        citas.MapGet("/{id:guid}", ObtenerCitaAsync)
            .WithSummary("Obtiene una cita.")
            .RequierePermiso(Permisos.CitaLeer);

        citas.MapPut("/{id:guid}", ActualizarCitaAsync)
            .WithSummary("Actualiza los datos de una cita (no altera el estado).")
            .RequierePermiso(Permisos.CitaGestionar);

        citas.MapPost("/{id:guid}/confirmar", ConfirmarCitaAsync)
            .WithSummary("Confirma una cita.")
            .RequierePermiso(Permisos.CitaGestionar);

        citas.MapPost("/{id:guid}/atender", AtenderCitaAsync)
            .WithSummary("Marca una cita como atendida.")
            .RequierePermiso(Permisos.CitaGestionar);

        citas.MapPost("/{id:guid}/no-presentado", MarcarNoPresentadoAsync)
            .WithSummary("Marca una cita como no presentado.")
            .RequierePermiso(Permisos.CitaGestionar);

        citas.MapPost("/{id:guid}/reprogramar", ReprogramarCitaAsync)
            .WithSummary("Reprograma una cita a un nuevo inicio (y, opcionalmente, nueva duración).")
            .RequierePermiso(Permisos.CitaGestionar);

        citas.MapDelete("/{id:guid}", CancelarCitaAsync)
            .WithSummary("Cancela una cita.")
            .RequierePermiso(Permisos.CitaGestionar);

        animales.MapGet("/{animalId:guid}/citas", ListarCitasAsync)
            .WithSummary("Lista las citas de un animal.")
            .RequierePermiso(Permisos.CitaLeer);

        var actos = rutas.MapGroup("/actos").WithTags("Actos clínicos");

        actos.MapGet("", ListarActosAsync)
            .WithSummary("Lista los actos clínicos de la empresa por estado (por defecto, los pendientes de facturar).")
            .RequierePermiso(Permisos.ActoLeer);

        actos.MapPost("/facturar", FacturarActosAsync)
            .WithSummary("Emite una factura VeriFactu a partir de varios actos del mismo cliente y los marca como facturados.")
            .RequierePermiso(Permisos.FacturaEmitir);

        actos.MapGet("/{id:guid}", ObtenerActoAsync)
            .WithSummary("Obtiene un acto clínico.")
            .RequierePermiso(Permisos.ActoLeer);

        actos.MapPut("/{id:guid}", ActualizarActoAsync)
            .WithSummary("Actualiza un acto clínico pendiente.")
            .RequierePermiso(Permisos.ActoGestionar);

        actos.MapPost("/{id:guid}/ticket", MarcarActoTicketAsync)
            .WithSummary("Cobra un acto clínico con ticket (fuera de la factura VeriFactu).")
            .RequierePermiso(Permisos.ActoGestionar);

        actos.MapDelete("/{id:guid}", AnularActoAsync)
            .WithSummary("Anula un acto clínico pendiente.")
            .RequierePermiso(Permisos.ActoGestionar);

        animales.MapGet("/{animalId:guid}/actos", ListarActosDeAnimalAsync)
            .WithSummary("Lista los actos clínicos de un animal.")
            .RequierePermiso(Permisos.ActoLeer);

        animales.MapPost("/{animalId:guid}/actos", RegistrarActoAsync)
            .WithSummary("Registra un acto clínico facturable de un animal.")
            .RequierePermiso(Permisos.ActoGestionar);

        return rutas;
    }

    private static async Task<IResult> ListarAsync(IContextoEmpresa contexto, ListarAnimales caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ObtenerAnimal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CrearAsync(DatosAnimal datos, IContextoEmpresa contexto, CrearAnimal caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/animales/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarAsync(Guid id, DatosActualizarAnimal datos, ActualizarAnimal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> DarDeBajaAsync(Guid id, DarDeBajaAnimal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarPorClienteAsync(Guid clienteId, ListarAnimalesDeCliente caso, CancellationToken ct) =>
        Results.Ok(await caso.EjecutarAsync(clienteId, ct).ConfigureAwait(false));

    private static async Task<IResult> ListarConsultasAsync(Guid animalId, ListarConsultasDeAnimal caso, CancellationToken ct) =>
        Results.Ok(await caso.EjecutarAsync(animalId, ct).ConfigureAwait(false));

    private static async Task<IResult> RegistrarConsultaAsync(Guid animalId, DatosConsulta datos, IContextoEmpresa contexto, RegistrarConsulta caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        // La ruta es la fuente de verdad del animal atendido.
        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos with { AnimalId = animalId }, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/consultas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ObtenerConsultaAsync(Guid id, ObtenerConsulta caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ActualizarConsultaAsync(Guid id, DatosActualizarConsulta datos, ActualizarConsulta caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> AnularConsultaAsync(Guid id, AnularConsulta caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarPautasAsync(EspecieAnimal? especie, IContextoEmpresa contexto, ListarPautasVacunales caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, especie, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CrearPautaAsync(DatosPautaVacunal datos, IContextoEmpresa contexto, CrearPautaVacunal caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/vacunas/pautas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ObtenerPautaAsync(Guid id, ObtenerPautaVacunal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ActualizarPautaAsync(Guid id, DatosPautaVacunal datos, ActualizarPautaVacunal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> DesactivarPautaAsync(Guid id, DesactivarPautaVacunal caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarProximasVacunasAsync(int? dias, IContextoEmpresa contexto, ListarProximasVacunas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, dias ?? 30, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ListarVacunasAsync(Guid animalId, ListarVacunacionesDeAnimal caso, CancellationToken ct) =>
        Results.Ok(await caso.EjecutarAsync(animalId, ct).ConfigureAwait(false));

    private static async Task<IResult> RegistrarVacunacionAsync(Guid animalId, DatosVacunacion datos, IContextoEmpresa contexto, RegistrarVacunacion caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        // La ruta es la fuente de verdad del animal vacunado.
        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos with { AnimalId = animalId }, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/vacunas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ObtenerVacunacionAsync(Guid id, ObtenerVacunacion caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ActualizarVacunacionAsync(Guid id, DatosActualizarVacunacion datos, ActualizarVacunacion caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> AnularVacunacionAsync(Guid id, AnularVacunacion caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarCirugiasAsync(Guid animalId, ListarCirugiasDeAnimal caso, CancellationToken ct) =>
        Results.Ok(await caso.EjecutarAsync(animalId, ct).ConfigureAwait(false));

    private static async Task<IResult> RegistrarCirugiaAsync(Guid animalId, DatosCirugia datos, IContextoEmpresa contexto, RegistrarCirugia caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        // La ruta es la fuente de verdad del animal intervenido.
        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos with { AnimalId = animalId }, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/cirugias/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ObtenerCirugiaAsync(Guid id, ObtenerCirugia caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ActualizarCirugiaAsync(Guid id, DatosActualizarCirugia datos, ActualizarCirugia caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> AnularCirugiaAsync(Guid id, AnularCirugia caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarProximasRevisionesAsync(int? dias, IContextoEmpresa contexto, ListarProximasRevisiones caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, dias ?? 30, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ListarRecordatoriosAsync(EstadoRecordatorio? estado, int? dias, IContextoEmpresa contexto, ListarRecordatorios caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, estado, dias, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CrearRecordatorioAsync(DatosRecordatorio datos, IContextoEmpresa contexto, CrearRecordatorio caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/recordatorios/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> GenerarRecordatoriosAsync(int? dias, IContextoEmpresa contexto, GenerarRecordatorios caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, dias ?? 30, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> EnviarPendientesAsync(int? dias, IContextoEmpresa contexto, EnviarRecordatoriosPendientes caso, IReloj reloj, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var hoy = DateOnly.FromDateTime(reloj.AhoraUtc.UtcDateTime);
        var hasta = hoy.AddDays(dias is { } d && d >= 0 ? d : 30);
        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, hasta, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> ObtenerRecordatorioAsync(Guid id, ObtenerRecordatorio caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ActualizarRecordatorioAsync(Guid id, DatosActualizarRecordatorio datos, ActualizarRecordatorio caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> EnviarRecordatorioAsync(Guid id, EnviarRecordatorio caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> CompletarRecordatorioAsync(Guid id, CompletarRecordatorio caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> CancelarRecordatorioAsync(Guid id, CancelarRecordatorio caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarAgendaAsync(
        DateTimeOffset desde,
        DateTimeOffset hasta,
        EstadoCita? estado,
        string? veterinario,
        IContextoEmpresa contexto,
        ListarAgenda caso,
        CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, desde, hasta, estado, veterinario, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CrearCitaAsync(DatosCita datos, IContextoEmpresa contexto, CrearCita caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/citas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ResumenCitasAsync(DateTimeOffset desde, DateTimeOffset hasta, IContextoEmpresa contexto, ResumenCitas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, desde, hasta, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ConfirmacionMensualAsync(int? meses, IContextoEmpresa contexto, ConfirmacionMensual caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, meses ?? 6, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerCitaAsync(Guid id, ObtenerCita caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ActualizarCitaAsync(Guid id, DatosActualizarCita datos, ActualizarCita caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ConfirmarCitaAsync(Guid id, ConfirmarCita caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> AtenderCitaAsync(Guid id, AtenderCita caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> MarcarNoPresentadoAsync(Guid id, MarcarNoPresentado caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ReprogramarCitaAsync(Guid id, DatosReprogramarCita datos, ReprogramarCita caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CancelarCitaAsync(Guid id, CancelarCita caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarCitasAsync(Guid animalId, bool? incluirCanceladas, ListarCitasDeAnimal caso, CancellationToken ct) =>
        Results.Ok(await caso.EjecutarAsync(animalId, incluirCanceladas ?? false, ct).ConfigureAwait(false));

    private static async Task<IResult> ListarActosAsync(EstadoActo? estado, IContextoEmpresa contexto, ListarActosClinicos caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, estado, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ListarActosDeAnimalAsync(Guid animalId, ListarActosDeAnimal caso, CancellationToken ct) =>
        Results.Ok(await caso.EjecutarAsync(animalId, ct).ConfigureAwait(false));

    private static async Task<IResult> RegistrarActoAsync(Guid animalId, DatosActoClinico datos, IContextoEmpresa contexto, RegistrarActoClinico caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        // La ruta es la fuente de verdad del animal atendido.
        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos with { AnimalId = animalId }, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/actos/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ObtenerActoAsync(Guid id, ObtenerActoClinico caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ActualizarActoAsync(Guid id, DatosActoClinico datos, ActualizarActoClinico caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> MarcarActoTicketAsync(Guid id, MarcarActoTicket caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> AnularActoAsync(Guid id, AnularActoClinico caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> FacturarActosAsync(FacturarActosCuerpo cuerpo, IContextoEmpresa contexto, FacturarActos caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var actoIds = cuerpo?.ActoIds ?? new List<Guid>();
        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, actoIds, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/facturas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }
}

/// <summary>Cuerpo para facturar varios actos clínicos: la lista de identificadores de acto.</summary>
public sealed record FacturarActosCuerpo(IReadOnlyList<Guid> ActoIds);
