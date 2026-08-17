using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Documentos.Infraestructura.Correo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace AlxorCore.Documentos.Infraestructura;

/// <summary>Composición del módulo Documentos (PDF y correo). No tiene persistencia propia.</summary>
public static class RegistroServicios
{
    /// <summary>
    /// Registra el módulo Documentos. Si se aporta configuración y la sección <c>Correo</c> está
    /// habilitada con un host SMTP, el envío de correo usa <see cref="ServicioCorreoSmtp"/>; en
    /// cualquier otro caso (por defecto) usa el stub, que registra el envío en el log. Así,
    /// desarrollo, pruebas y demo no envían correos reales salvo que se active explícitamente.
    /// </summary>
    public static IServiceCollection AgregarModuloDocumentos(this IServiceCollection servicios, IConfiguration? configuracion = null)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        // Licencia Community de QuestPDF (gratuita para facturación de pequeño volumen).
        QuestPDF.Settings.License = LicenseType.Community;

        servicios.AddScoped<IGeneradorPdfFactura, GeneradorPdfFacturaQuestPdf>();
        servicios.AddScoped<IGeneradorPdfPresupuesto, GeneradorPdfPresupuestoQuestPdf>();

        // Correo: SMTP real si la sección «Correo» está habilitada y tiene host; si no, el stub.
        var opcionesCorreo = new OpcionesCorreo();
        configuracion?.GetSection(OpcionesCorreo.Seccion).Bind(opcionesCorreo);
        if (configuracion is not null)
        {
            servicios.AddOptions<OpcionesCorreo>().Bind(configuracion.GetSection(OpcionesCorreo.Seccion));
        }

        if (DebeUsarSmtp(opcionesCorreo))
        {
            servicios.AddScoped<IServicioCorreo, ServicioCorreoSmtp>();
        }
        else
        {
            servicios.AddScoped<IServicioCorreo, ServicioCorreoStub>();
        }

        servicios.AddScoped<GenerarPdfFactura>();
        servicios.AddScoped<EnviarFacturaPorEmail>();
        servicios.AddScoped<GenerarPdfPresupuesto>();
        servicios.AddScoped<EnviarPresupuestoPorEmail>();

        return servicios;
    }

    /// <summary>Regla de selección: usar SMTP real solo si el correo está habilitado y con host.</summary>
    internal static bool DebeUsarSmtp(OpcionesCorreo opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        return opciones.DebeEnviarPorSmtp;
    }
}
