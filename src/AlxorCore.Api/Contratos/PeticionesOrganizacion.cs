using AlxorCore.Organizacion.Dominio;

namespace AlxorCore.Api.Contratos;

/// <summary>Cuerpo para crear una empresa.</summary>
public sealed record CrearEmpresaPeticion(
    string Nif,
    string RazonSocial,
    string? Calle = null,
    string? CodigoPostal = null,
    string? Poblacion = null,
    string? Provincia = null,
    RegimenIva RegimenIva = RegimenIva.General);

/// <summary>Cuerpo para actualizar los datos maestros de la empresa activa.</summary>
public sealed record ActualizarEmpresaPeticion(
    string Nif,
    string RazonSocial,
    string? Calle = null,
    string? CodigoPostal = null,
    string? Poblacion = null,
    string? Provincia = null,
    RegimenIva RegimenIva = RegimenIva.General);

/// <summary>Cuerpo para establecer (o quitar, con null) el logo de la empresa activa (data URI).</summary>
public sealed record LogoPeticion(string? Logo);

/// <summary>Cuerpo para crear una serie de numeración.</summary>
public sealed record CrearSeriePeticion(TipoDocumento TipoDocumento, int Ejercicio, string Prefijo);
