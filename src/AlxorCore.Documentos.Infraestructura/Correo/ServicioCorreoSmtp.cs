using System.Net;
using System.Net.Mail;
using AlxorCore.Documentos.Aplicacion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlxorCore.Documentos.Infraestructura.Correo;

/// <summary>
/// Implementación real del puerto de correo <see cref="IServicioCorreo"/> mediante <b>SMTP</b>
/// (<see cref="System.Net.Mail.SmtpClient"/>, integrado en .NET). Envía el mensaje de
/// <see cref="MensajeCorreo"/> con su PDF adjunto usando el servidor configurado en la sección
/// <c>Correo</c> (<see cref="OpcionesCorreo"/>).
/// <para>
/// Se usa <c>System.Net.Mail</c> en lugar de MailKit para mantener una única pila SMTP en todo el
/// producto (el módulo Identidad ya envía sus correos de cuenta con <c>System.Net.Mail</c>) y no
/// añadir dependencias justo antes de la instalación. Cubre el caso habitual de las clínicas:
/// puerto 587 con STARTTLS (Gmail, dominios propios). Para 465 (SSL implícito), no soportado de
/// forma fiable por <c>System.Net.Mail</c>, use 587.
/// </para>
/// </summary>
internal sealed class ServicioCorreoSmtp : IServicioCorreo
{
    private readonly OpcionesCorreo _opciones;
    private readonly ILogger<ServicioCorreoSmtp> _log;

    public ServicioCorreoSmtp(IOptions<OpcionesCorreo> opciones, ILogger<ServicioCorreoSmtp> log)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        _opciones = opciones.Value;
        _log = log;
    }

    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        using var correo = ConstruirMensaje(mensaje, _opciones);
        using var cliente = new SmtpClient(_opciones.Host, _opciones.Puerto) { EnableSsl = _opciones.UsarStartTls };
        if (!string.IsNullOrWhiteSpace(_opciones.Usuario))
        {
            cliente.Credentials = new NetworkCredential(_opciones.Usuario, _opciones.Clave);
        }

        await cliente.SendMailAsync(correo, ct).ConfigureAwait(false);
        _log.LogInformation("Correo «{Asunto}» enviado por SMTP a {Para} ({Bytes} bytes adjuntos).",
            mensaje.Asunto, mensaje.Para, mensaje.Adjunto.Length);
    }

    /// <summary>
    /// Compone el <see cref="MailMessage"/> a partir del <see cref="MensajeCorreo"/> y de los ajustes
    /// del remitente. Expuesto a nivel de ensamblado para poder verificar la composición en pruebas
    /// sin llegar a enviar. El llamante es responsable de liberar (<c>Dispose</c>) el mensaje.
    /// </summary>
    internal static MailMessage ConstruirMensaje(MensajeCorreo mensaje, OpcionesCorreo opciones)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        ArgumentNullException.ThrowIfNull(opciones);

        var correo = new MailMessage
        {
            From = new MailAddress(opciones.Remitente, opciones.RemitenteNombre),
            Subject = mensaje.Asunto,
            Body = mensaje.Cuerpo,
            IsBodyHtml = false,
        };
        correo.To.Add(mensaje.Para);

        if (mensaje.Adjunto is { Length: > 0 })
        {
            // El flujo lo gestiona el propio MailMessage al liberarse (Attachment es dueño del stream).
            var flujo = new MemoryStream(mensaje.Adjunto, writable: false);
            correo.Attachments.Add(new Attachment(flujo, mensaje.NombreAdjunto, "application/pdf"));
        }

        return correo;
    }
}
