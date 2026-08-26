namespace AlxorCore.Identidad.Dominio;

/// <summary>
/// Sexo del usuario. Es un dato de perfil opcional (por defecto <see cref="NoIndicado"/>) que la
/// interfaz utiliza, entre otras cosas, para elegir el avatar de veterinario/a. Se persiste como texto.
/// </summary>
public enum SexoUsuario
{
    /// <summary>El usuario prefiere no indicarlo (valor por defecto).</summary>
    NoIndicado = 0,

    /// <summary>Hombre.</summary>
    Hombre = 1,

    /// <summary>Mujer.</summary>
    Mujer = 2,
}
