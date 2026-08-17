using AlxorCore.Api.Comun;
using AlxorCore.Clinica.Infraestructura;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Terceros.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Api.Endpoints;

/// <summary>
/// Buscador global de la clínica: una única consulta de solo lectura que reúne coincidencias de
/// clientes (por nombre o NIF) y animales (por nombre o microchip) de la empresa activa, con los
/// datos mínimos para navegar a su ficha. Reutiliza los DbContext existentes (sin agregado nuevo);
/// el filtro multiempresa global de cada contexto garantiza el aislamiento por empresa.
/// </summary>
public static class EndpointsBusqueda
{
    /// <summary>Número máximo de coincidencias devueltas por cada tipo (clientes y animales).</summary>
    private const int LimitePorTipo = 6;

    /// <summary>Longitud mínima del término de búsqueda para lanzar la consulta.</summary>
    private const int LongitudMinima = 2;

    public static IEndpointRouteBuilder MapearBusqueda(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        rutas.MapGet("/buscar", BuscarAsync)
            .WithTags("Búsqueda")
            .WithSummary("Buscador global: clientes (nombre/NIF) y animales (nombre/microchip) de la empresa activa.")
            .RequierePermiso(Permisos.AnimalLeer);

        return rutas;
    }

    private static async Task<IResult> BuscarAsync(
        string? q,
        IContextoEmpresa contexto,
        TercerosDbContext terceros,
        ClinicaDbContext clinica,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(terceros);
        ArgumentNullException.ThrowIfNull(clinica);

        if (contexto?.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var termino = (q ?? string.Empty).Trim();
        if (termino.Length < LongitudMinima)
        {
            return Results.Ok(Array.Empty<ResultadoBusqueda>());
        }

        var empresaId = contexto.EmpresaId.Value;
        var patron = "%" + EscaparLike(termino) + "%";

        // Clientes por nombre o NIF (el filtro global ya acota a la empresa activa; se reitera el
        // empresa_id de forma explícita para que el aislamiento no dependa de un único mecanismo).
        var clientes = await terceros.Clientes
            .Where(c => c.EmpresaId == empresaId && c.Activo)
            .Where(c => EF.Functions.ILike(c.Nombre, patron)
                || (c.NifFiscal != null && EF.Functions.ILike(c.NifFiscal, patron)))
            .OrderBy(c => c.Nombre)
            .Take(LimitePorTipo)
            .Select(c => new ResultadoBusqueda(
                "cliente",
                c.Id,
                null,
                c.Nombre,
                c.NifFiscal ?? c.Direccion.Poblacion ?? c.Email))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Animales por nombre o microchip.
        var animales = await clinica.Animales
            .Where(a => a.EmpresaId == empresaId && a.Activo)
            .Where(a => EF.Functions.ILike(a.Nombre, patron)
                || (a.Microchip != null && EF.Functions.ILike(a.Microchip, patron)))
            .OrderBy(a => a.Nombre)
            .Take(LimitePorTipo)
            .Select(a => new ResultadoBusqueda(
                "animal",
                a.Id,
                a.ClienteId,
                a.Nombre,
                a.Raza != null ? a.Especie.ToString() + " · " + a.Raza : a.Especie.ToString()))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var resultados = new List<ResultadoBusqueda>(clientes.Count + animales.Count);
        resultados.AddRange(clientes);
        resultados.AddRange(animales);
        return Results.Ok(resultados);
    }

    /// <summary>Escapa los comodines de ILike (%, _ y \) para que el término se busque literalmente.</summary>
    private static string EscaparLike(string valor) => valor
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}

/// <summary>
/// Coincidencia del buscador global. <see cref="Tipo"/> es «cliente» o «animal»; <see cref="ClienteId"/>
/// solo viene informado para los animales (permite abrir la ficha del animal con su propietario).
/// </summary>
public sealed record ResultadoBusqueda(string Tipo, Guid Id, Guid? ClienteId, string Etiqueta, string? Subetiqueta);
