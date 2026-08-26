using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Terceros.Dominio;

/// <summary>Se ha creado un cliente.</summary>
public sealed record ClienteCreado(Guid ClienteId, Guid EmpresaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Cliente de una empresa. Guarda los datos fiscales necesarios para facturarle. El identificador
/// fiscal (<see cref="NifFiscal"/>) es opcional y se acepta como texto: un cliente puede ser
/// extranjero y no tener NIF español.
/// </summary>
public sealed class Cliente : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 200;
    public const int LongitudMaximaTelefono = 30;
    public const decimal IrpfMaximo = 60m;

    private Cliente(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
        Direccion = Direccion.Vacia;
    }

    private Cliente(Guid id, Guid empresaId, string nombre, string? nifFiscal, string? email, string? telefono, Direccion direccion, decimal irpf, bool recargoEquivalencia, string? iban, string? mandatoReferencia, DateOnly? mandatoFecha, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Nombre = nombre;
        NifFiscal = nifFiscal;
        Email = email;
        Telefono = telefono;
        Direccion = direccion;
        PorcentajeIrpfDefecto = irpf;
        RecargoEquivalencia = recargoEquivalencia;
        Iban = NormalizarIban(iban);
        MandatoReferencia = Normalizar(mandatoReferencia);
        MandatoFecha = mandatoFecha;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    public string Nombre { get; private set; }

    public string? NifFiscal { get; private set; }

    public string? Email { get; private set; }

    /// <summary>Teléfono de contacto del cliente. Opcional. Se usa, p. ej., para avisos por WhatsApp.</summary>
    public string? Telefono { get; private set; }

    public Direccion Direccion { get; private set; }

    /// <summary>Retención de IRPF por defecto (0–60 %). Se prerrellena al facturar.</summary>
    public decimal PorcentajeIrpfDefecto { get; private set; }

    /// <summary>El cliente está en régimen de recargo de equivalencia (minorista): al facturarle se aplica por defecto.</summary>
    public bool RecargoEquivalencia { get; private set; }

    /// <summary>IBAN del cliente para domiciliar sus recibos (adeudos SEPA). Opcional.</summary>
    public string? Iban { get; private set; }

    /// <summary>Referencia única del mandato de domiciliación firmado por el cliente. Opcional.</summary>
    public string? MandatoReferencia { get; private set; }

    /// <summary>Fecha de firma del mandato de domiciliación. Opcional.</summary>
    public DateOnly? MandatoFecha { get; private set; }

    /// <summary>¿Tiene los datos necesarios para domiciliar (IBAN, mandato y fecha)?</summary>
    public bool DomiciliacionCompleta => !string.IsNullOrWhiteSpace(Iban) && !string.IsNullOrWhiteSpace(MandatoReferencia) && MandatoFecha is not null;

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Cliente> Crear(
        Guid empresaId,
        string? nombre,
        string? nifFiscal,
        string? email,
        Direccion direccion,
        decimal porcentajeIrpfDefecto,
        IReloj reloj,
        bool recargoEquivalencia = false,
        string? iban = null,
        string? mandatoReferencia = null,
        DateOnly? mandatoFecha = null,
        string? telefono = null)
    {
        ArgumentNullException.ThrowIfNull(direccion);
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, porcentajeIrpfDefecto, telefono);
        if (error is not null)
        {
            return Resultado.Fallo<Cliente>(error);
        }

        var cliente = new Cliente(
            Guid.NewGuid(), empresaId, nombre!.Trim(), Normalizar(nifFiscal), Normalizar(email), Normalizar(telefono), direccion, porcentajeIrpfDefecto, recargoEquivalencia, iban, mandatoReferencia, mandatoFecha, reloj.AhoraUtc);
        cliente.RegistrarEvento(new ClienteCreado(cliente.Id, empresaId, reloj.AhoraUtc));
        return Resultado.Ok(cliente);
    }

    public Resultado Actualizar(string? nombre, string? nifFiscal, string? email, Direccion direccion, decimal porcentajeIrpfDefecto, IReloj reloj, bool recargoEquivalencia = false, string? iban = null, string? mandatoReferencia = null, DateOnly? mandatoFecha = null, string? telefono = null)
    {
        ArgumentNullException.ThrowIfNull(direccion);
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, porcentajeIrpfDefecto, telefono);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Nombre = nombre!.Trim();
        NifFiscal = Normalizar(nifFiscal);
        Email = Normalizar(email);
        Telefono = Normalizar(telefono);
        Direccion = direccion;
        PorcentajeIrpfDefecto = porcentajeIrpfDefecto;
        RecargoEquivalencia = recargoEquivalencia;
        Iban = NormalizarIban(iban);
        MandatoReferencia = Normalizar(mandatoReferencia);
        MandatoFecha = mandatoFecha;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public void Desactivar(IReloj reloj)
    {
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static Error? Validar(string? nombre, decimal irpf, string? telefono)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("cliente.nombre_vacio", "El nombre del cliente es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("cliente.nombre_largo", "El nombre del cliente es demasiado largo.");
        }

        if (irpf is < 0 or > IrpfMaximo)
        {
            return Error.Validacion("cliente.irpf_invalido", "El porcentaje de IRPF no es válido.");
        }

        if (telefono is not null && telefono.Trim().Length > LongitudMaximaTelefono)
        {
            return Error.Validacion("cliente.telefono_largo", "El teléfono es demasiado largo.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? NormalizarIban(string? iban) =>
        string.IsNullOrWhiteSpace(iban) ? null : iban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
