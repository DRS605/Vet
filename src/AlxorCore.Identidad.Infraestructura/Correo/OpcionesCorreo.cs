namespace AlxorCore.Identidad.Infraestructura.Correo;

/// <summary>
/// Ajustes del envío de correo. El envío real por SMTP solo se activa cuando <see cref="Habilitado"/>
/// es <c>true</c> y hay un <see cref="Host"/> configurado; en cualquier otro caso se usa el <b>stub</b>
/// (registra el enlace en el log). Coincide con el gating del módulo Documentos.
/// </summary>
public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    /// <summary>Interruptor explícito del envío real. Por defecto <c>false</c> (modo stub).</summary>
    public bool Habilitado { get; set; }

    /// <summary>Servidor SMTP. Vacío = modo stub (no se envían correos reales).</summary>
    public string? Host { get; set; }

    public int Puerto { get; set; } = 587;

    public bool UsarStartTls { get; set; } = true;

    public string? Usuario { get; set; }

    public string? Clave { get; set; }

    /// <summary>Dirección del remitente (p. ej. no-responder@tudominio.com).</summary>
    public string Remitente { get; set; } = "no-responder@alxor.local";

    public string RemitenteNombre { get; set; } = "ALXOR Core";

    /// <summary>URL base de la aplicación para construir los enlaces de los correos.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>¿Debe usarse el envío real por SMTP? Requiere estar habilitado y tener un host.</summary>
    public bool Configurado => Habilitado && !string.IsNullOrWhiteSpace(Host);
}
