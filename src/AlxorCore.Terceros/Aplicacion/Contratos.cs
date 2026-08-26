using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Terceros.Dominio;

namespace AlxorCore.Terceros.Aplicacion;

/// <summary>Vista de un cliente.</summary>
public sealed record ClienteDto(
    Guid Id,
    string Nombre,
    string? NifFiscal,
    string? Email,
    string Calle,
    string CodigoPostal,
    string Poblacion,
    string Provincia,
    string Pais,
    decimal PorcentajeIrpfDefecto,
    bool Activo,
    bool RecargoEquivalencia,
    string? Iban,
    string? MandatoReferencia,
    DateOnly? MandatoFecha,
    string? Telefono)
{
    public static ClienteDto Desde(Cliente c) => new(
        c.Id, c.Nombre, c.NifFiscal, c.Email,
        c.Direccion.Calle, c.Direccion.CodigoPostal, c.Direccion.Poblacion, c.Direccion.Provincia, c.Direccion.Pais,
        c.PorcentajeIrpfDefecto, c.Activo, c.RecargoEquivalencia, c.Iban, c.MandatoReferencia, c.MandatoFecha, c.Telefono);
}

/// <summary>Repositorio de clientes (escritura).</summary>
public interface IRepositorioClientes
{
    Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Cliente cliente);
}

/// <summary>Consultas de lectura de clientes (las usan la propia API y otros módulos como Facturación).</summary>
public interface IConsultaClientes
{
    Task<ClienteDto?> ObtenerAsync(Guid clienteId, CancellationToken ct = default);

    Task<IReadOnlyList<ClienteDto>> ListarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default);
}

/// <summary>Unidad de trabajo del módulo Terceros.</summary>
public interface IUnidadDeTrabajoTerceros : IUnidadDeTrabajo;
