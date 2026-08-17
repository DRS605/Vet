namespace AlxorCore.Documentos.Infraestructura.Correo;

/// <summary>
/// Ajustes del envío de correo del módulo Documentos (facturas y presupuestos por email, y los
/// recordatorios de Clínica, que comparten el puerto <see cref="AlxorCore.Documentos.Aplicacion.IServicioCorreo"/>).
/// <para>
/// Se enlaza a la sección <c>Correo</c> de la configuración. El envío real por SMTP solo se activa
/// cuando <see cref="Habilitado"/> es <c>true</c> y hay un <see cref="Host"/> configurado; en
/// cualquier otro caso se mantiene el comportamiento por defecto (stub que registra en el log), de
/// modo que desarrollo, pruebas y demo no envían correos reales.
/// </para>
/// </summary>
public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    /// <summary>Interruptor explícito del envío real. Por defecto <c>false</c> (modo stub).</summary>
    public bool Habilitado { get; set; }

    /// <summary>Servidor SMTP (p. ej. <c>smtp.gmail.com</c> o el del proveedor de la clínica).</summary>
    public string? Host { get; set; }

    /// <summary>Puerto SMTP. 587 (STARTTLS) es lo habitual y recomendado.</summary>
    public int Puerto { get; set; } = 587;

    /// <summary>Usar STARTTLS al conectar (recomendado en el puerto 587).</summary>
    public bool UsarStartTls { get; set; } = true;

    /// <summary>Usuario de autenticación SMTP (a menudo la propia dirección de correo).</summary>
    public string? Usuario { get; set; }

    /// <summary>Contraseña o «contraseña de aplicación» de la cuenta SMTP.</summary>
    public string? Clave { get; set; }

    /// <summary>Dirección del remitente (p. ej. <c>clinica@tudominio.com</c>).</summary>
    public string Remitente { get; set; } = "no-responder@alxor.local";

    /// <summary>Nombre visible del remitente.</summary>
    public string RemitenteNombre { get; set; } = "ALXOR Core";

    /// <summary>¿Debe usarse el envío real por SMTP? Requiere estar habilitado y tener un host.</summary>
    public bool DebeEnviarPorSmtp => Habilitado && !string.IsNullOrWhiteSpace(Host);
}
