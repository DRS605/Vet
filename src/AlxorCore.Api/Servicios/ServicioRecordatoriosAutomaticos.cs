using System.Globalization;
using AlxorCore.Clinica.Aplicacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Api.Servicios;

/// <summary>Ajustes del envío automático de recordatorios clínicos (vacunas y revisiones).</summary>
public sealed class OpcionesRecordatoriosAutomaticos
{
    public const string Seccion = "Recordatorios";

    /// <summary>Si está activo el proceso automático. <b>Desactivado por defecto.</b></summary>
    public bool AutomaticoHabilitado { get; set; }

    /// <summary>Hora local a la que se ejecuta cada día (formato <c>HH:mm</c>).</summary>
    public string HoraEjecucion { get; set; } = "08:00";

    /// <summary>Ventana en días para generar y enviar los recordatorios que vencen próximamente.</summary>
    public int DiasAntelacion { get; set; } = 30;

    /// <summary>Convierte <see cref="HoraEjecucion"/> a <see cref="TimeOnly"/>; 08:00 si no es válida.</summary>
    public TimeOnly HoraDelDia()
    {
        return TimeOnly.TryParseExact(HoraEjecucion, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora)
            ? hora
            : new TimeOnly(8, 0);
    }
}

/// <summary>
/// Proceso en segundo plano que, una vez al día a la hora configurada, recorre <b>todas las
/// empresas</b> y, para cada una, genera los recordatorios de los vencimientos próximos (vacunas y
/// revisiones) y envía por correo los que estén pendientes. Igual que la facturación recurrente,
/// abre un ámbito por empresa con su contexto fijado (aislamiento multiempresa) y es tolerante a
/// fallos: un error en una empresa no detiene al resto.
/// <para>
/// Está <b>desactivado por defecto</b> (<see cref="OpcionesRecordatoriosAutomaticos.AutomaticoHabilitado"/>),
/// de modo que no afecta a pruebas, demo ni al arranque salvo que se active explícitamente.
/// </para>
/// </summary>
public sealed class ServicioRecordatoriosAutomaticos : BackgroundService
{
    private readonly IServiceScopeFactory _ambitos;
    private readonly IReloj _reloj;
    private readonly ILogger<ServicioRecordatoriosAutomaticos> _log;
    private readonly OpcionesRecordatoriosAutomaticos _opciones;

    public ServicioRecordatoriosAutomaticos(
        IServiceScopeFactory ambitos,
        IReloj reloj,
        ILogger<ServicioRecordatoriosAutomaticos> log,
        Microsoft.Extensions.Options.IOptions<OpcionesRecordatoriosAutomaticos> opciones)
    {
        _ambitos = ambitos;
        _reloj = reloj;
        _log = log;
        _opciones = opciones.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.AutomaticoHabilitado)
        {
            return;
        }

        _log.LogInformation(
            "Recordatorios automáticos activados: se ejecutarán a las {Hora} cada día (ventana {Dias} días).",
            _opciones.HoraDelDia(), _opciones.DiasAntelacion);

        try
        {
            // Espera hasta la próxima hora de ejecución y, a partir de ahí, una pasada cada 24 h.
            await Task.Delay(RetardoHastaProximaEjecucion(), stoppingToken).ConfigureAwait(false);
            using var temporizador = new PeriodicTimer(TimeSpan.FromHours(24));
            do
            {
                await ProcesarTodasLasEmpresasAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await temporizador.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Apagado normal.
        }
    }

    private TimeSpan RetardoHastaProximaEjecucion()
    {
        var ahora = _reloj.AhoraUtc.ToLocalTime().DateTime;
        var objetivoHoy = ahora.Date.Add(_opciones.HoraDelDia().ToTimeSpan());
        var objetivo = objetivoHoy > ahora ? objetivoHoy : objetivoHoy.AddDays(1);
        return objetivo - ahora;
    }

    private async Task ProcesarTodasLasEmpresasAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> empresas;
        using (var ambito = _ambitos.CreateScope())
        {
            // La tabla de empresas no está filtrada por empresa: se listan todas para recorrerlas.
            var contexto = ambito.ServiceProvider.GetRequiredService<OrganizacionDbContext>();
            empresas = await contexto.Empresas.Select(e => e.Id).ToListAsync(ct).ConfigureAwait(false);
        }

        if (empresas.Count == 0)
        {
            return;
        }

        var ventana = _opciones.DiasAntelacion < 0 ? 0 : _opciones.DiasAntelacion;
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var hasta = hoy.AddDays(ventana);

        var totalEnviados = 0;
        foreach (var empresaId in empresas)
        {
            try
            {
                using var ambito = _ambitos.CreateScope();
                ambito.ServiceProvider.GetRequiredService<IContextoEmpresaMutable>().Fijar(empresaId);

                var generar = ambito.ServiceProvider.GetRequiredService<GenerarRecordatorios>();
                await generar.EjecutarAsync(empresaId, ventana, ct).ConfigureAwait(false);

                var enviar = ambito.ServiceProvider.GetRequiredService<EnviarRecordatoriosPendientes>();
                var resultado = await enviar.EjecutarAsync(empresaId, hasta, ct).ConfigureAwait(false);
                if (resultado.EsCorrecto)
                {
                    totalEnviados += resultado.Valor.Enviados;
                }
            }
#pragma warning disable CA1031 // Un fallo en una empresa no debe tumbar el proceso ni afectar a las demás.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _log.LogError(ex, "Fallo al procesar recordatorios de la empresa {EmpresaId}.", empresaId);
            }
        }

        if (totalEnviados > 0)
        {
            _log.LogInformation("Recordatorios automáticos: {Total} correo(s) enviado(s) en {Empresas} empresa(s).",
                totalEnviados, empresas.Count);
        }
    }
}
