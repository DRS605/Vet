using AlxorCore.Organizacion.Dominio;

namespace AlxorCore.Organizacion.Aplicacion.Modelos;

/// <summary>Vista completa de una empresa.</summary>
public sealed record EmpresaDto(
    Guid Id,
    string Nif,
    string RazonSocial,
    RegimenIva RegimenIva,
    string Moneda,
    string Pais,
    string? Iban,
    string? IdentificadorAcreedor,
    string Calle,
    string CodigoPostal,
    string Poblacion,
    string Provincia,
    string? Logo)
{
    public static EmpresaDto Desde(Empresa empresa) =>
        new(
            empresa.Id,
            empresa.Nif.Valor,
            empresa.RazonSocial,
            empresa.RegimenIva,
            empresa.Moneda,
            empresa.Pais,
            empresa.Iban,
            empresa.IdentificadorAcreedor,
            empresa.Direccion.Calle,
            empresa.Direccion.CodigoPostal,
            empresa.Direccion.Poblacion,
            empresa.Direccion.Provincia,
            empresa.Logo);
}

/// <summary>Resumen de una empresa a la que pertenece un usuario, con su rol.</summary>
public sealed record EmpresaResumen(Guid Id, string Nif, string RazonSocial, string RolCodigo);

/// <summary>Vista de una serie de numeración.</summary>
public sealed record SerieDto(Guid Id, TipoDocumento TipoDocumento, int Ejercicio, string Prefijo, long SiguienteNumero)
{
    public static SerieDto Desde(SerieNumeracion serie) =>
        new(serie.Id, serie.TipoDocumento, serie.Ejercicio, serie.Prefijo, serie.SiguienteNumero);
}

/// <summary>Resultado de seleccionar una empresa: un token con el alcance de esa empresa.</summary>
public sealed record ResultadoSeleccionEmpresa(
    string Token,
    DateTimeOffset ExpiraEn,
    Guid EmpresaId,
    string RolCodigo,
    IReadOnlyCollection<string> Permisos);
