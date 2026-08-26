using AlxorCore.Identidad.Dominio;

namespace AlxorCore.Identidad.Aplicacion.Modelos;

/// <summary>Vista de solo lectura del perfil de un usuario, para devolver por la API.</summary>
public sealed record PerfilUsuario(Guid Id, string Email, string Nombre, bool EmailVerificado, SexoUsuario Sexo)
{
    public static PerfilUsuario Desde(Usuario usuario) =>
        new(usuario.Id, usuario.Email.Valor, usuario.Nombre, usuario.EmailVerificado, usuario.Sexo);
}
