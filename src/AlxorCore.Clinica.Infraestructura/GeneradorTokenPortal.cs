using System.Security.Cryptography;
using AlxorCore.Clinica.Aplicacion;

namespace AlxorCore.Clinica.Infraestructura;

/// <summary>
/// Genera el token del acceso de portal con aleatoriedad <b>criptográfica</b>
/// (<see cref="RandomNumberGenerator"/>), URL-safe y de al menos 32 bytes de entropía. El resultado
/// es la codificación base64url (sin relleno) de los bytes aleatorios, apta para ir en una URL sin
/// escapado. Nunca se derivan tokens de datos predecibles.
/// </summary>
public sealed class GeneradorTokenPortal : IGeneradorTokenPortal
{
    /// <summary>Bytes de entropía del token (256 bits).</summary>
    public const int BytesEntropia = 32;

    public string Generar()
    {
        var bytes = RandomNumberGenerator.GetBytes(BytesEntropia);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
