using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Dominio.Eventos;

namespace AlxorCore.Organizacion.Dominio;

/// <summary>
/// Membresía: relación de un usuario con una empresa y el rol que desempeña en ella. Es el puente
/// entre la identidad (global) y la empresa (tenant). Un usuario puede tener varias membresías.
/// </summary>
public sealed class Membresia : RaizAgregado<Guid>
{
    private Membresia(Guid id)
        : base(id)
    {
        RolCodigo = null!;
    }

    private Membresia(Guid id, Guid usuarioId, Guid empresaId, string rolCodigo, DateTimeOffset ahora)
        : base(id)
    {
        UsuarioId = usuarioId;
        EmpresaId = empresaId;
        RolCodigo = rolCodigo;
        Estado = EstadoMembresia.Activa;
        CreadoEn = ahora;
    }

    public Guid UsuarioId { get; private set; }

    public Guid EmpresaId { get; private set; }

    public string RolCodigo { get; private set; }

    public EstadoMembresia Estado { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Marca si este miembro es veterinario/a en la empresa (para elegirlo en actos clínicos).</summary>
    public bool EsVeterinario { get; private set; }

    public bool EstaActiva => Estado == EstadoMembresia.Activa;

    /// <summary>Crea la membresía del propietario al dar de alta una empresa.</summary>
    public static Membresia CrearPropietario(Guid usuarioId, Guid empresaId, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        return Crear(usuarioId, empresaId, Rol.Propietario, reloj);
    }

    /// <summary>Crea una membresía con un rol concreto.</summary>
    public static Membresia Crear(Guid usuarioId, Guid empresaId, Rol rol, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(rol);
        ArgumentNullException.ThrowIfNull(reloj);

        var membresia = new Membresia(Guid.NewGuid(), usuarioId, empresaId, rol.Codigo, reloj.AhoraUtc);
        membresia.RegistrarEvento(new MembresiaCreada(membresia.Id, usuarioId, empresaId, rol.Codigo, reloj.AhoraUtc));
        return membresia;
    }

    /// <summary>Revoca la membresía. Idempotente.</summary>
    public void Revocar() => Estado = EstadoMembresia.Revocada;

    /// <summary>Reactiva una membresía revocada.</summary>
    public void Reactivar() => Estado = EstadoMembresia.Activa;

    /// <summary>Cambia el rol de la membresía.</summary>
    public void CambiarRol(Rol rol)
    {
        ArgumentNullException.ThrowIfNull(rol);
        RolCodigo = rol.Codigo;
    }

    /// <summary>Marca o desmarca a este miembro como veterinario/a de la empresa.</summary>
    public void MarcarVeterinario(bool es) => EsVeterinario = es;
}
