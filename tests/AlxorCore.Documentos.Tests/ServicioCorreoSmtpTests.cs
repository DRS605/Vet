using System.Text;
using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Documentos.Infraestructura;
using AlxorCore.Documentos.Infraestructura.Correo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Documentos.Tests;

/// <summary>
/// Pruebas de la implementación real de correo por SMTP del módulo Documentos: verifican la
/// composición del mensaje a partir de <see cref="MensajeCorreo"/> (sin enviar) y la lógica de
/// selección stub/SMTP según la configuración.
/// </summary>
public sealed class ServicioCorreoSmtpTests
{
    private static readonly OpcionesCorreo Remite = new()
    {
        Remitente = "clinica@ejemplo.com",
        RemitenteNombre = "Clínica Veterinaria Demo",
    };

    [Fact]
    public void ConstruirMensaje_componeRemitenteDestinatarioAsuntoYCuerpo()
    {
        var mensaje = new MensajeCorreo("dueño@ejemplo.com", "Factura F-2026-001", "Adjuntamos su factura.",
            Encoding.UTF8.GetBytes("%PDF-1.7 contenido"), "F-2026-001.pdf");

        using var correo = ServicioCorreoSmtp.ConstruirMensaje(mensaje, Remite);

        correo.From!.Address.Should().Be("clinica@ejemplo.com");
        correo.From.DisplayName.Should().Be("Clínica Veterinaria Demo");
        correo.To.Should().ContainSingle().Which.Address.Should().Be("dueño@ejemplo.com");
        correo.Subject.Should().Be("Factura F-2026-001");
        correo.Body.Should().Be("Adjuntamos su factura.");
        correo.IsBodyHtml.Should().BeFalse();
    }

    [Fact]
    public void ConstruirMensaje_adjuntaElPdfConSuNombre()
    {
        var mensaje = new MensajeCorreo("dueño@ejemplo.com", "Presupuesto", "Cuerpo",
            Encoding.UTF8.GetBytes("%PDF-1.7"), "P-2026-007.pdf");

        using var correo = ServicioCorreoSmtp.ConstruirMensaje(mensaje, Remite);

        var adjunto = correo.Attachments.Should().ContainSingle().Subject;
        adjunto.Name.Should().Be("P-2026-007.pdf");
        adjunto.ContentType.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public void ConstruirMensaje_sinAdjunto_noAgregaAdjuntos()
    {
        var mensaje = new MensajeCorreo("dueño@ejemplo.com", "Aviso", "Cuerpo", Array.Empty<byte>(), "vacio.pdf");

        using var correo = ServicioCorreoSmtp.ConstruirMensaje(mensaje, Remite);

        correo.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void DebeUsarSmtp_falso_cuandoNoEstaHabilitado_aunqueHayaHost()
    {
        var opciones = new OpcionesCorreo { Habilitado = false, Host = "smtp.ejemplo.com" };

        RegistroServicios.DebeUsarSmtp(opciones).Should().BeFalse();
    }

    [Fact]
    public void DebeUsarSmtp_falso_cuandoHabilitadoPeroSinHost()
    {
        var opciones = new OpcionesCorreo { Habilitado = true, Host = "" };

        RegistroServicios.DebeUsarSmtp(opciones).Should().BeFalse();
    }

    [Fact]
    public void DebeUsarSmtp_verdadero_cuandoHabilitadoYConHost()
    {
        var opciones = new OpcionesCorreo { Habilitado = true, Host = "smtp.ejemplo.com" };

        RegistroServicios.DebeUsarSmtp(opciones).Should().BeTrue();
    }
}
