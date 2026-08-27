using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Dominio.Eventos;

namespace AlxorCore.Organizacion.Dominio;

/// <summary>
/// Empresa: la entidad tenant de ALXOR Core. No hereda de la base multiempresa porque ella misma
/// define el tenant; el acceso a una empresa se controla por las membresías del usuario.
/// </summary>
public sealed class Empresa : RaizAgregado<Guid>
{
    public const int LongitudMaximaRazonSocial = 200;

    private Empresa(Guid id)
        : base(id)
    {
        Nif = null!;
        RazonSocial = null!;
        Direccion = null!;
    }

    private Empresa(Guid id, Nif nif, string razonSocial, Direccion direccion, RegimenIva regimenIva, DateTimeOffset ahora)
        : base(id)
    {
        Nif = nif;
        RazonSocial = razonSocial;
        Direccion = direccion;
        RegimenIva = regimenIva;
        Moneda = "EUR";
        Pais = "ES";
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    public Nif Nif { get; private set; }

    public string RazonSocial { get; private set; }

    public Direccion Direccion { get; private set; }

    public RegimenIva RegimenIva { get; private set; }

    public string Moneda { get; private set; } = "EUR";

    public string Pais { get; private set; } = "ES";

    /// <summary>IBAN de la empresa donde ingresar los adeudos domiciliados (remesas SEPA). Opcional.</summary>
    public string? Iban { get; private set; }

    /// <summary>Identificador del acreedor SEPA (lo asigna el banco). Necesario para emitir remesas. Opcional.</summary>
    public string? IdentificadorAcreedor { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>Logo de la clínica como data URI (opcional). Se muestra en la cabecera de la app.</summary>
    public string? Logo { get; private set; }

    public static Resultado<Empresa> Crear(Nif nif, string? razonSocial, Direccion direccion, RegimenIva regimenIva, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(nif);
        ArgumentNullException.ThrowIfNull(direccion);
        ArgumentNullException.ThrowIfNull(reloj);

        var nombre = (razonSocial ?? string.Empty).Trim();
        if (nombre.Length == 0)
        {
            return Resultado.Fallo<Empresa>(Error.Validacion("empresa.razon_social_vacia", "La razón social es obligatoria."));
        }

        if (nombre.Length > LongitudMaximaRazonSocial)
        {
            return Resultado.Fallo<Empresa>(Error.Validacion("empresa.razon_social_larga", "La razón social es demasiado larga."));
        }

        var empresa = new Empresa(Guid.NewGuid(), nif, nombre, direccion, regimenIva, reloj.AhoraUtc);
        empresa.RegistrarEvento(new EmpresaCreada(empresa.Id, nif.Valor, reloj.AhoraUtc));
        return Resultado.Ok(empresa);
    }

    /// <summary>
    /// Actualiza los datos maestros de la empresa (NIF, razón social, dirección y régimen de IVA).
    /// No afecta a documentos ya congelados (facturas): solo cambia los datos de la empresa para el futuro.
    /// </summary>
    public Resultado ActualizarDatos(Nif nif, string? razonSocial, Direccion direccion, RegimenIva regimenIva, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(nif);
        ArgumentNullException.ThrowIfNull(direccion);
        ArgumentNullException.ThrowIfNull(reloj);

        var nombre = (razonSocial ?? string.Empty).Trim();
        if (nombre.Length == 0)
        {
            return Resultado.Fallo(Error.Validacion("empresa.razon_social_vacia", "La razón social es obligatoria."));
        }

        if (nombre.Length > LongitudMaximaRazonSocial)
        {
            return Resultado.Fallo(Error.Validacion("empresa.razon_social_larga", "La razón social es demasiado larga."));
        }

        Nif = nif;
        RazonSocial = nombre;
        Direccion = direccion;
        RegimenIva = regimenIva;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Fija los datos de cobro por domiciliación (IBAN de ingreso e identificador del acreedor SEPA).</summary>
    public void EstablecerDatosCobro(string? iban, string? identificadorAcreedor, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        Iban = string.IsNullOrWhiteSpace(iban) ? null : iban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        IdentificadorAcreedor = string.IsNullOrWhiteSpace(identificadorAcreedor) ? null : identificadorAcreedor.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        ActualizadoEn = reloj.AhoraUtc;
    }

    /// <summary>Establece (o quita, con <c>null</c>) el logo de la clínica.</summary>
    public void EstablecerLogo(string? logo, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Logo = string.IsNullOrWhiteSpace(logo) ? null : logo;
        ActualizadoEn = reloj.AhoraUtc;
    }
}
