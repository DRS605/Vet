using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Clinica.Dominio;

/// <summary>
/// Acceso al portal del dueño (la <b>Cartilla Viva</b>): un enlace sin contraseña que la clínica
/// genera para un <see cref="ClienteId">cliente</see> de la empresa. El <see cref="Token"/> es la
/// única credencial —una cadena aleatoria criptográfica, URL-safe— y resuelve de forma inequívoca la
/// empresa y el cliente. Es una raíz de agregado por empresa (cuelga del cliente, sin FK entre
/// esquemas). Un cliente tiene como mucho un acceso <see cref="Activo"/>: regenerar revoca el anterior.
/// El token lo genera un servicio con aleatoriedad criptográfica (no el dominio); aquí solo se recibe,
/// se valida y se guarda.
/// </summary>
public sealed class AccesoPortal : RaizAgregadoEmpresa<Guid>
{
    /// <summary>Longitud máxima admitida para el token guardado (suficiente para ≥32 bytes en base64url).</summary>
    public const int LongitudMaximaToken = 200;

    /// <summary>Longitud mínima exigida para que el token tenga suficiente entropía.</summary>
    public const int LongitudMinimaToken = 32;

    private AccesoPortal(Guid id)
        : base(id, Guid.Empty)
    {
        Token = null!;
    }

    private AccesoPortal(Guid id, Guid empresaId, Guid clienteId, string token, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        ClienteId = clienteId;
        Token = token;
        Activo = true;
        CreadoEn = ahora;
        RevocadoEn = null;
    }

    /// <summary>Cliente (dueño) al que pertenece el acceso. Se guarda solo el identificador (sin FK entre esquemas).</summary>
    public Guid ClienteId { get; private set; }

    /// <summary>Token del enlace: única credencial del portal. Aleatorio, URL-safe y único en el sistema.</summary>
    public string Token { get; private set; }

    /// <summary>¿El acceso está activo? Un token revocado o inactivo no resuelve ninguna cartilla.</summary>
    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Momento de la revocación, si se revocó. <c>null</c> mientras el acceso está activo.</summary>
    public DateTimeOffset? RevocadoEn { get; private set; }

    /// <summary>
    /// Crea un acceso de portal activo para un cliente. El <paramref name="token"/> lo genera un
    /// servicio con aleatoriedad criptográfica; aquí solo se valida y se guarda.
    /// </summary>
    public static Resultado<AccesoPortal> Crear(Guid empresaId, Guid clienteId, string? token, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (clienteId == Guid.Empty)
        {
            return Resultado.Fallo<AccesoPortal>(Error.Validacion("acceso_portal.cliente_obligatorio", "El acceso de portal debe tener un cliente (dueño)."));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Resultado.Fallo<AccesoPortal>(Error.Validacion("acceso_portal.token_vacio", "El token del acceso de portal es obligatorio."));
        }

        var normalizado = token.Trim();
        if (normalizado.Length < LongitudMinimaToken)
        {
            return Resultado.Fallo<AccesoPortal>(Error.Validacion("acceso_portal.token_corto", "El token del acceso de portal no tiene suficiente entropía."));
        }

        if (normalizado.Length > LongitudMaximaToken)
        {
            return Resultado.Fallo<AccesoPortal>(Error.Validacion("acceso_portal.token_largo", "El token del acceso de portal es demasiado largo."));
        }

        return Resultado.Ok(new AccesoPortal(Guid.NewGuid(), empresaId, clienteId, normalizado, reloj.AhoraUtc));
    }

    /// <summary>Revoca el acceso (baja lógica): el token deja de resolver ninguna cartilla.</summary>
    public void Revocar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        if (!Activo)
        {
            return;
        }

        Activo = false;
        RevocadoEn = reloj.AhoraUtc;
    }
}
