using AlxorCore.Identidad.Dominio;

namespace AlxorCore.Api.Contratos;

/// <summary>Cuerpo de la petición de registro.</summary>
public sealed record RegistroPeticion(string Email, string Nombre, string Contrasena);

/// <summary>Cuerpo para actualizar el perfil propio (nombre y sexo).</summary>
public sealed record ActualizarPerfilPeticion(string Nombre, SexoUsuario Sexo);

/// <summary>Cuerpo para cambiar la propia contraseña.</summary>
public sealed record CambiarClavePeticion(string ClaveActual, string NuevaClave);

/// <summary>Cuerpo de la petición de inicio de sesión.</summary>
public sealed record LoginPeticion(string Email, string Contrasena);

/// <summary>Cuerpo para verificar el correo con el token del enlace.</summary>
public sealed record VerificarEmailPeticion(string Token);

/// <summary>Cuerpo para solicitar el restablecimiento de contraseña.</summary>
public sealed record RecuperarPeticion(string Email);

/// <summary>Cuerpo para fijar la nueva contraseña con el token del enlace.</summary>
public sealed record RestablecerPeticion(string Token, string NuevaContrasena);
