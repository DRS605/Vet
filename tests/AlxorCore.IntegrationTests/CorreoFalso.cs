using System.Collections.Concurrent;
using AlxorCore.Documentos.Aplicacion;

namespace AlxorCore.IntegrationTests;

/// <summary>
/// Doble de pruebas del puerto de correo del módulo Documentos (<see cref="IServicioCorreo"/>): en
/// lugar de enviar, captura los mensajes en memoria para que las pruebas de integración puedan
/// comprobar el destinatario, el asunto y el cuerpo. Es el mismo puerto que usan Facturación y los
/// recordatorios de Clínica, así que sustituyéndolo aquí se prueba todo el flujo de envío.
/// </summary>
public sealed class CorreoFalso : IServicioCorreo
{
    private readonly ConcurrentQueue<MensajeCorreo> _mensajes = new();

    /// <summary>Todos los mensajes «enviados» hasta ahora.</summary>
    public IReadOnlyCollection<MensajeCorreo> Mensajes => _mensajes.ToArray();

    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        _mensajes.Enqueue(mensaje);
        return Task.CompletedTask;
    }

    /// <summary>Mensajes dirigidos a un destinatario concreto.</summary>
    public IReadOnlyList<MensajeCorreo> ParaDestinatario(string email) =>
        _mensajes.Where(m => string.Equals(m.Para, email, StringComparison.OrdinalIgnoreCase)).ToList();
}
