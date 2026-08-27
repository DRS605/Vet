using AlxorCore.Api.Comun;
using AlxorCore.Identidad.Aplicacion.CasosDeUso;
using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Organizacion.Aplicacion.CasosDeUso;

namespace AlxorCore.Api.Endpoints;

/// <summary>Cuerpo para invitar a un usuario a la empresa. Si viene <c>ContrasenaInicial</c>, el usuario puede entrar ya con ella (sin correo).</summary>
public sealed record InvitarPeticion(string Email, string? Nombre, string Rol, string? ContrasenaInicial = null);

/// <summary>Cuerpo para cambiar el rol de un miembro.</summary>
public sealed record CambiarRolPeticion(string Rol);

/// <summary>Cuerpo para marcar/desmarcar a un miembro como veterinario/a.</summary>
public sealed record MarcarVeterinarioPeticion(bool Es);

/// <summary>Vista de un miembro de la empresa (membresía + datos del usuario).</summary>
public sealed record MiembroDto(Guid UsuarioId, string Email, string Nombre, bool EmailVerificado, string Rol, string RolNombre, string Estado, bool EsYo, bool EsVeterinario);

/// <summary>Veterinario/a de la empresa (para elegir en consultas, vacunas, cirugías y actos).</summary>
public sealed record VeterinarioDto(Guid UsuarioId, string Nombre);

/// <summary>
/// Endpoints de <b>gestión de usuarios de la empresa</b>. Orquestan la identidad (usuarios) y la
/// organización (membresías): listar miembros, invitar (crear usuario si hace falta + membresía),
/// cambiar rol y revocar acceso. Requieren el permiso <c>usuario.gestionar</c> (rol Propietario).
/// </summary>
public static class EndpointsUsuarios
{
    public static IEndpointRouteBuilder MapearUsuarios(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var usuarios = rutas.MapGroup("/usuarios").WithTags("Usuarios de la empresa");

        usuarios.MapGet("", ListarAsync)
            .WithSummary("Lista los usuarios (miembros) de la empresa activa.")
            .RequierePermiso(Permisos.UsuarioGestionar);

        usuarios.MapPost("/invitar", InvitarAsync)
            .WithSummary("Invita a un usuario a la empresa con un rol (lo crea si no existe).")
            .RequierePermiso(Permisos.UsuarioGestionar);

        usuarios.MapPost("/{usuarioId:guid}/rol", CambiarRolAsync)
            .WithSummary("Cambia el rol de un miembro.")
            .RequierePermiso(Permisos.UsuarioGestionar);

        usuarios.MapPost("/{usuarioId:guid}/veterinario", MarcarVeterinarioAsync)
            .WithSummary("Marca o desmarca a un miembro como veterinario/a.")
            .RequierePermiso(Permisos.UsuarioGestionar);

        usuarios.MapPost("/{usuarioId:guid}/revocar", RevocarAsync)
            .WithSummary("Revoca el acceso de un miembro a la empresa.")
            .RequierePermiso(Permisos.UsuarioGestionar);

        // Lista de veterinarios/as: la puede leer CUALQUIER miembro (no solo el propietario),
        // para rellenar el desplegable de veterinario/a en las fichas clínicas.
        var vets = rutas.MapGroup("/veterinarios").WithTags("Veterinarios");
        vets.MapGet("", ListarVeterinariosAsync)
            .WithSummary("Lista los veterinarios/as de la empresa activa.")
            .RequireAuthorization();

        return rutas;
    }

    private static async Task<IResult> ListarAsync(
        System.Security.Claims.ClaimsPrincipal principal, IContextoEmpresa contexto,
        ListarMembresias membresias, IConsultaUsuarios usuarios, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var yo = principal.ObtenerUsuarioId();
        var lista = await membresias.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false);
        var resumenes = (await usuarios.ListarResumenesAsync(lista.Select(m => m.UsuarioId).ToList(), ct).ConfigureAwait(false))
            .ToDictionary(u => u.Id);

        var miembros = lista.Select(m =>
        {
            resumenes.TryGetValue(m.UsuarioId, out var u);
            return new MiembroDto(
                m.UsuarioId, u?.Email ?? "—", u?.Nombre ?? "—", u?.EmailVerificado ?? false,
                m.RolCodigo, m.RolNombre, m.Estado, m.UsuarioId == yo, m.EsVeterinario);
        }).ToList();

        return Results.Ok(miembros);
    }

    private static async Task<IResult> MarcarVeterinarioAsync(
        Guid usuarioId, MarcarVeterinarioPeticion peticion, IContextoEmpresa contexto, MarcarVeterinarioMembresia caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, usuarioId, peticion?.Es ?? false, ct).ConfigureAwait(false)).ASinContenido();
    }

    private static async Task<IResult> ListarVeterinariosAsync(
        IContextoEmpresa contexto, ListarMembresias membresias, IConsultaUsuarios usuarios, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var lista = (await membresias.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false))
            .Where(m => m.EsVeterinario && m.Estado == "Activa").ToList();
        var resumenes = (await usuarios.ListarResumenesAsync(lista.Select(m => m.UsuarioId).ToList(), ct).ConfigureAwait(false))
            .ToDictionary(u => u.Id);
        var vets = lista.Select(m =>
        {
            resumenes.TryGetValue(m.UsuarioId, out var u);
            return new VeterinarioDto(m.UsuarioId, u?.Nombre ?? "—");
        }).OrderBy(v => v.Nombre, StringComparer.OrdinalIgnoreCase).ToList();

        return Results.Ok(vets);
    }

    private static async Task<IResult> InvitarAsync(
        InvitarPeticion peticion, IContextoEmpresa contexto, IHostEnvironment entorno,
        IConsultaUsuarios usuarios, CrearUsuarioInvitado crear, AgregarMembresia agregar, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        // Usuario existente → se reutiliza; si no existe → se crea con token para fijar su contraseña.
        var existente = await usuarios.ObtenerResumenPorEmailAsync(peticion.Email ?? string.Empty, ct).ConfigureAwait(false);
        Guid usuarioId;
        string? token = null;
        if (existente is not null)
        {
            usuarioId = existente.Id;
        }
        else
        {
            var creado = await crear.EjecutarAsync(peticion.Email ?? string.Empty, peticion.Nombre, peticion.ContrasenaInicial, ct).ConfigureAwait(false);
            if (creado.EsFallo)
            {
                return ResultadosHttp.AProblema(creado.Error);
            }

            usuarioId = creado.Valor.Usuario.Id;
            token = creado.Valor.TokenRestablecimiento;
        }

        var membresia = await agregar.EjecutarAsync(contexto.EmpresaId.Value, usuarioId, peticion.Rol, ct).ConfigureAwait(false);
        if (membresia.EsFallo)
        {
            return ResultadosHttp.AProblema(membresia.Error);
        }

        // Si el admin fijó contraseña inicial, el usuario puede entrar ya (sin enlace).
        var conClave = existente is null && !string.IsNullOrEmpty(peticion.ContrasenaInicial);
        var incluirToken = !conClave && !string.IsNullOrEmpty(token) && !entorno.IsProduction();
        return Results.Ok(new { usuarioId, creado = existente is null, accesoInmediato = conClave, enlaceContrasena = incluirToken ? token : null });
    }

    private static async Task<IResult> CambiarRolAsync(
        Guid usuarioId, CambiarRolPeticion peticion, IContextoEmpresa contexto, CambiarRolMembresia caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, usuarioId, peticion.Rol, ct).ConfigureAwait(false)).ASinContenido();
    }

    private static async Task<IResult> RevocarAsync(
        Guid usuarioId, System.Security.Claims.ClaimsPrincipal principal, IContextoEmpresa contexto, RevocarMembresia caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        if (principal.ObtenerUsuarioId() == usuarioId)
        {
            return ResultadosHttp.AProblema(Error.Validacion("membresia.no_te_revocas", "No puedes revocar tu propio acceso."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, usuarioId, ct).ConfigureAwait(false)).ASinContenido();
    }
}
