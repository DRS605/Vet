using AlxorCore.Clinica.Aplicacion;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Clinica.Infraestructura;

/// <summary>Composición del módulo Clínica.</summary>
public static class RegistroServicios
{
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloClinica(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{CadenaConexion}».");

        servicios.AddScoped<InterceptorEmpresa>();
        servicios.AddDbContext<ClinicaDbContext>((sp, opciones) =>
            opciones
                .UseNpgsql(conexion, npgsql =>
                    npgsql.MigrationsHistoryTable("__historial_migraciones", ClinicaDbContext.Esquema))
                .AddInterceptors(sp.GetRequiredService<InterceptorEmpresa>()));

        servicios.AddScoped<IUnidadDeTrabajoClinica>(sp => sp.GetRequiredService<ClinicaDbContext>());
        servicios.AddScoped<RepositorioAnimales>();
        servicios.AddScoped<IRepositorioAnimales>(sp => sp.GetRequiredService<RepositorioAnimales>());
        servicios.AddScoped<IConsultaAnimales>(sp => sp.GetRequiredService<RepositorioAnimales>());

        servicios.AddScoped<RepositorioConsultas>();
        servicios.AddScoped<IRepositorioConsultas>(sp => sp.GetRequiredService<RepositorioConsultas>());
        servicios.AddScoped<IConsultaConsultas>(sp => sp.GetRequiredService<RepositorioConsultas>());

        servicios.AddScoped<CrearAnimal>();
        servicios.AddScoped<ActualizarAnimal>();
        servicios.AddScoped<ListarAnimales>();
        servicios.AddScoped<ListarAnimalesDeCliente>();
        servicios.AddScoped<ObtenerAnimal>();
        servicios.AddScoped<DarDeBajaAnimal>();

        servicios.AddScoped<RegistrarConsulta>();
        servicios.AddScoped<ActualizarConsulta>();
        servicios.AddScoped<ObtenerConsulta>();
        servicios.AddScoped<ListarConsultasDeAnimal>();
        servicios.AddScoped<AnularConsulta>();

        servicios.AddScoped<RepositorioPautasVacunales>();
        servicios.AddScoped<IRepositorioPautasVacunales>(sp => sp.GetRequiredService<RepositorioPautasVacunales>());
        servicios.AddScoped<IConsultaPautasVacunales>(sp => sp.GetRequiredService<RepositorioPautasVacunales>());

        servicios.AddScoped<RepositorioVacunaciones>();
        servicios.AddScoped<IRepositorioVacunaciones>(sp => sp.GetRequiredService<RepositorioVacunaciones>());
        servicios.AddScoped<IConsultaVacunaciones>(sp => sp.GetRequiredService<RepositorioVacunaciones>());

        servicios.AddScoped<CrearPautaVacunal>();
        servicios.AddScoped<ActualizarPautaVacunal>();
        servicios.AddScoped<ObtenerPautaVacunal>();
        servicios.AddScoped<ListarPautasVacunales>();
        servicios.AddScoped<DesactivarPautaVacunal>();

        servicios.AddScoped<RegistrarVacunacion>();
        servicios.AddScoped<ActualizarVacunacion>();
        servicios.AddScoped<ObtenerVacunacion>();
        servicios.AddScoped<ListarVacunacionesDeAnimal>();
        servicios.AddScoped<ListarProximasVacunas>();
        servicios.AddScoped<AnularVacunacion>();

        servicios.AddScoped<RepositorioCirugias>();
        servicios.AddScoped<IRepositorioCirugias>(sp => sp.GetRequiredService<RepositorioCirugias>());
        servicios.AddScoped<IConsultaCirugias>(sp => sp.GetRequiredService<RepositorioCirugias>());

        servicios.AddScoped<RegistrarCirugia>();
        servicios.AddScoped<ActualizarCirugia>();
        servicios.AddScoped<ObtenerCirugia>();
        servicios.AddScoped<ListarCirugiasDeAnimal>();
        servicios.AddScoped<ListarProximasRevisiones>();
        servicios.AddScoped<AnularCirugia>();

        servicios.AddScoped<RepositorioRecordatorios>();
        servicios.AddScoped<IRepositorioRecordatorios>(sp => sp.GetRequiredService<RepositorioRecordatorios>());
        servicios.AddScoped<IConsultaRecordatorios>(sp => sp.GetRequiredService<RepositorioRecordatorios>());

        // Los recordatorios se envían por correo reutilizando el puerto IServicioCorreo del módulo
        // Documentos (registrado por AgregarModuloDocumentos en la composición de la API), igual que
        // Facturación resuelve ahí su envío de facturas por email.
        servicios.AddScoped<CrearRecordatorio>();
        servicios.AddScoped<GenerarRecordatorios>();
        servicios.AddScoped<EnviarRecordatorio>();
        servicios.AddScoped<EnviarRecordatoriosPendientes>();
        servicios.AddScoped<ActualizarRecordatorio>();
        servicios.AddScoped<CompletarRecordatorio>();
        servicios.AddScoped<CancelarRecordatorio>();
        servicios.AddScoped<ObtenerRecordatorio>();
        servicios.AddScoped<ListarRecordatorios>();

        return servicios;
    }
}
