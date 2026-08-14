using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Nucleo.Autorizacion;

/// <summary>
/// Rol de negocio dentro de una empresa. Un rol es un conjunto fijo de <see cref="Permisos"/>.
/// El MVP define tres roles; la asignación de un rol a un usuario dentro de una empresa
/// (la membresía) pertenece al módulo Organización.
/// </summary>
public sealed class Rol
{
    /// <summary>Acceso total: el dueño de la empresa.</summary>
    public static readonly Rol Propietario = new(
        "propietario",
        "Propietario",
        Permisos.Todos);

    /// <summary>Operativa diaria, sin gestión de usuarios ni ajustes sensibles.</summary>
    public static readonly Rol Usuario = new(
        "usuario",
        "Usuario",
        new HashSet<string>(StringComparer.Ordinal)
        {
            Permisos.FacturaLeer, Permisos.FacturaCrear, Permisos.FacturaEmitir,
            Permisos.GastoLeer, Permisos.GastoGestionar,
            Permisos.CobroRegistrar, Permisos.PagoRegistrar,
            Permisos.ClienteGestionar, Permisos.ProductoGestionar,
            Permisos.AnimalLeer, Permisos.AnimalGestionar,
            Permisos.ConsultaLeer, Permisos.ConsultaGestionar,
            Permisos.VacunaLeer, Permisos.VacunaGestionar,
            Permisos.CirugiaLeer, Permisos.CirugiaGestionar,
            Permisos.RecordatorioLeer, Permisos.RecordatorioGestionar,
            Permisos.CitaLeer, Permisos.CitaGestionar,
            Permisos.ActoLeer, Permisos.ActoGestionar,
            Permisos.InformeLeer, Permisos.DatosExportar,
        });

    /// <summary>Solo consulta y exportación (por ejemplo, la gestoría).</summary>
    public static readonly Rol SoloLectura = new(
        "solo_lectura",
        "Solo lectura",
        new HashSet<string>(StringComparer.Ordinal)
        {
            Permisos.FacturaLeer, Permisos.GastoLeer, Permisos.AnimalLeer, Permisos.ConsultaLeer, Permisos.VacunaLeer, Permisos.CirugiaLeer, Permisos.RecordatorioLeer, Permisos.CitaLeer, Permisos.ActoLeer, Permisos.InformeLeer, Permisos.DatosExportar,
        });

    private static readonly Dictionary<string, Rol> PorCodigo =
        new[] { Propietario, Usuario, SoloLectura }.ToDictionary(r => r.Codigo, StringComparer.Ordinal);

    private Rol(string codigo, string nombre, IReadOnlySet<string> permisos)
    {
        Codigo = codigo;
        Nombre = nombre;
        PermisosConcedidos = permisos;
    }

    /// <summary>Código estable del rol (persistible, apto para el token).</summary>
    public string Codigo { get; }

    /// <summary>Nombre legible en español.</summary>
    public string Nombre { get; }

    /// <summary>Permisos que otorga el rol.</summary>
    public IReadOnlySet<string> PermisosConcedidos { get; }

    /// <summary>Todos los roles disponibles.</summary>
    public static IReadOnlyCollection<Rol> Todos => PorCodigo.Values;

    /// <summary>Indica si el rol concede el permiso indicado.</summary>
    public bool Concede(string permiso) => PermisosConcedidos.Contains(permiso);

    /// <summary>Resuelve un rol por su código.</summary>
    public static Resultado<Rol> PorCodigoRol(string? codigo)
    {
        if (!string.IsNullOrWhiteSpace(codigo) && PorCodigo.TryGetValue(codigo, out var rol))
        {
            return Resultado.Ok(rol);
        }

        return Resultado.Fallo<Rol>(Error.Validacion("rol.desconocido", $"El rol «{codigo}» no existe."));
    }
}
