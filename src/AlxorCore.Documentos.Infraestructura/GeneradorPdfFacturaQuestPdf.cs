using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlxorCore.Documentos.Infraestructura;

/// <summary>Genera el PDF de una factura con QuestPDF. Diseño limpio con el logo y color de la clínica.</summary>
internal sealed class GeneradorPdfFacturaQuestPdf : IGeneradorPdfFactura
{
    // Color de acento de la marca (naranja Vetis). QuestPDF convierte el hex a Color.
    private const string Acento = "#E07E22";

    public byte[] Generar(FacturaDto factura, EmpresaDto emisor)
    {
        ArgumentNullException.ThrowIfNull(factura);
        ArgumentNullException.ThrowIfNull(emisor);

        return string.Equals(factura.Tipo, "Simplificada", StringComparison.OrdinalIgnoreCase)
            ? GenerarTicket(factura, emisor)
            : GenerarFacturaA4(factura, emisor);
    }

    /// <summary>Decodifica el logo (data URI base64) a bytes para incrustarlo. null si no hay o es SVG.</summary>
    private static byte[]? LogoBytes(EmpresaDto emisor)
    {
        var logo = emisor.Logo;
        if (string.IsNullOrWhiteSpace(logo)) { return null; }
        var coma = logo.IndexOf(',', StringComparison.Ordinal);
        if (coma < 0 || !logo.StartsWith("data:image", StringComparison.OrdinalIgnoreCase)) { return null; }
        var meta = logo[..coma];
        // QuestPDF.Image no dibuja SVG; solo incrustamos PNG/JPG.
        if (meta.Contains("svg", StringComparison.OrdinalIgnoreCase) || !meta.Contains("base64", StringComparison.OrdinalIgnoreCase)) { return null; }
        try { return Convert.FromBase64String(logo[(coma + 1)..]); } catch (FormatException) { return null; }
    }

    private static byte[] GenerarFacturaA4(FacturaDto factura, EmpresaDto emisor)
    {
        var logo = LogoBytes(emisor);
        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(40);
                pagina.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                pagina.Header().Column(cab =>
                {
                    cab.Item().Row(fila =>
                    {
                        fila.RelativeItem().Row(izq =>
                        {
                            if (logo is not null)
                            {
                                izq.ConstantItem(56).PaddingRight(12).AlignMiddle().Image(logo);
                            }

                            izq.RelativeItem().AlignMiddle().Column(col =>
                            {
                                col.Item().Text(emisor.RazonSocial).Bold().FontSize(16);
                                col.Item().Text($"NIF: {emisor.Nif}").FontColor(Colors.Grey.Darken2);
                                var dir = DireccionEmisor(emisor);
                                if (!string.IsNullOrWhiteSpace(dir))
                                {
                                    col.Item().Text(dir).FontSize(9).FontColor(Colors.Grey.Darken1);
                                }
                            });
                        });
                        fila.ConstantItem(200).AlignRight().Column(col =>
                        {
                            col.Item().Text("FACTURA").Bold().FontSize(18).FontColor(Acento);
                            col.Item().Text(factura.NumeroCompleto).Bold();
                            col.Item().Text($"Fecha: {factura.FechaEmision:dd/MM/yyyy}").FontColor(Colors.Grey.Darken2);
                        });
                    });
                    cab.Item().PaddingTop(10).LineHorizontal(2).LineColor(Acento);
                });

                pagina.Content().PaddingVertical(18).Column(col =>
                {
                    col.Item().PaddingBottom(12).Column(cliente =>
                    {
                        cliente.Item().Text("CLIENTE").Bold().FontSize(9).FontColor(Acento);
                        cliente.Item().PaddingTop(2).Text(factura.ClienteNombre).Bold();
                        if (!string.IsNullOrWhiteSpace(factura.ClienteNif))
                        {
                            cliente.Item().Text($"NIF: {factura.ClienteNif}").FontColor(Colors.Grey.Darken2);
                        }
                    });

                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(columnas =>
                        {
                            columnas.RelativeColumn(4);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                        });

                        tabla.Header(encabezado =>
                        {
                            static IContainer Th(IContainer c) => c.Background(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(4);
                            encabezado.Cell().Element(Th).Text("Descripción").Bold();
                            encabezado.Cell().Element(Th).AlignRight().Text("Cantidad").Bold();
                            encabezado.Cell().Element(Th).AlignRight().Text("Precio").Bold();
                            encabezado.Cell().Element(Th).AlignRight().Text("IVA").Bold();
                            encabezado.Cell().Element(Th).AlignRight().Text("Total").Bold();
                        });

                        foreach (var linea in factura.Lineas)
                        {
                            static IContainer Td(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(4);
                            tabla.Cell().Element(Td).Text(linea.Descripcion);
                            tabla.Cell().Element(Td).AlignRight().Text(Redondeo.Formatear(linea.Cantidad));
                            tabla.Cell().Element(Td).AlignRight().Text($"{Redondeo.Formatear(linea.PrecioUnitario)} €");
                            tabla.Cell().Element(Td).AlignRight().Text($"{linea.PorcentajeIva:0}%");
                            tabla.Cell().Element(Td).AlignRight().Text($"{Redondeo.Formatear(linea.Base + linea.CuotaIva)} €");
                        }
                    });

                    col.Item().AlignRight().PaddingTop(16).Width(230).Column(totales =>
                    {
                        totales.Item().Row(f => { f.RelativeItem().Text("Base imponible"); f.ConstantItem(90).AlignRight().Text($"{Redondeo.Formatear(factura.BaseImponible)} €"); });
                        totales.Item().Row(f => { f.RelativeItem().Text("IVA"); f.ConstantItem(90).AlignRight().Text($"{Redondeo.Formatear(factura.CuotaIva)} €"); });
                        if (factura.RecargoTotal > 0)
                        {
                            totales.Item().Row(f => { f.RelativeItem().Text("Recargo equiv."); f.ConstantItem(90).AlignRight().Text($"{Redondeo.Formatear(factura.RecargoTotal)} €"); });
                        }

                        if (factura.RetencionIrpf > 0)
                        {
                            totales.Item().Row(f => { f.RelativeItem().Text($"Retención IRPF ({factura.PorcentajeIrpf:0}%)"); f.ConstantItem(90).AlignRight().Text($"-{Redondeo.Formatear(factura.RetencionIrpf)} €"); });
                        }

                        totales.Item().PaddingTop(6).BorderTop(1.5f).BorderColor(Acento).PaddingTop(6).Row(f =>
                        {
                            f.RelativeItem().Text("TOTAL").Bold().FontSize(14).FontColor(Acento);
                            f.ConstantItem(100).AlignRight().Text($"{Redondeo.Formatear(factura.Total)} €").Bold().FontSize(14).FontColor(Acento);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(factura.Observaciones))
                    {
                        col.Item().PaddingTop(22).Column(obs =>
                        {
                            obs.Item().Text("OBSERVACIONES").Bold().FontSize(9).FontColor(Acento);
                            obs.Item().PaddingTop(3).Text(factura.Observaciones).FontColor(Colors.Grey.Darken2);
                        });
                    }
                });

                pagina.Footer().AlignCenter().Text(texto =>
                {
                    texto.Span(emisor.RazonSocial).FontColor(Colors.Grey.Medium).FontSize(9);
                    if (!string.IsNullOrWhiteSpace(emisor.Nif))
                    {
                        texto.Span($"  ·  NIF {emisor.Nif}").FontColor(Colors.Grey.Medium).FontSize(9);
                    }
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static string DireccionEmisor(EmpresaDto emisor)
    {
        var partes = new[] { emisor.Calle, emisor.CodigoPostal, emisor.Poblacion, emisor.Provincia }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" · ", partes);
    }

    /// <summary>Genera el PDF de un ticket (factura simplificada) en formato rollo de 80 mm.</summary>
    private static byte[] GenerarTicket(FacturaDto factura, EmpresaDto emisor)
    {
        var logo = LogoBytes(emisor);
        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.ContinuousSize(72, Unit.Millimetre);
                pagina.Margin(6, Unit.Millimetre);
                pagina.DefaultTextStyle(x => x.FontSize(8).FontFamily(Fonts.Calibri));

                pagina.Content().Column(col =>
                {
                    col.Spacing(2);

                    if (logo is not null)
                    {
                        col.Item().AlignCenter().Width(90).Image(logo);
                    }

                    col.Item().AlignCenter().Text(emisor.RazonSocial).Bold().FontSize(11);
                    col.Item().AlignCenter().Text($"NIF: {emisor.Nif}");
                    col.Item().PaddingVertical(3).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                    col.Item().AlignCenter().Text("TICKET · FACTURA SIMPLIFICADA").Bold();
                    col.Item().AlignCenter().Text(factura.NumeroCompleto);
                    col.Item().AlignCenter().Text($"{factura.FechaEmision:dd/MM/yyyy}");
                    col.Item().PaddingVertical(3).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                    foreach (var linea in factura.Lineas)
                    {
                        col.Item().Text(linea.Descripcion);
                        col.Item().Row(fila =>
                        {
                            fila.RelativeItem().Text($"{Redondeo.Formatear(linea.Cantidad)} × {Redondeo.Formatear(linea.PrecioUnitario)} €  (IVA {linea.PorcentajeIva:0}%)").FontColor(Colors.Grey.Darken1);
                            fila.ConstantItem(70).AlignRight().Text($"{Redondeo.Formatear(linea.Base + linea.CuotaIva)} €");
                        });
                    }

                    col.Item().PaddingVertical(3).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                    col.Item().Row(f => { f.RelativeItem().Text("Base"); f.ConstantItem(70).AlignRight().Text($"{Redondeo.Formatear(factura.BaseImponible)} €"); });
                    col.Item().Row(f => { f.RelativeItem().Text("IVA"); f.ConstantItem(70).AlignRight().Text($"{Redondeo.Formatear(factura.CuotaIva)} €"); });
                    col.Item().PaddingTop(2).Row(f =>
                    {
                        f.RelativeItem().Text("TOTAL").Bold().FontSize(11).FontColor(Acento);
                        f.ConstantItem(80).AlignRight().Text($"{Redondeo.Formatear(factura.Total)} €").Bold().FontSize(11).FontColor(Acento);
                    });
                    col.Item().AlignCenter().PaddingTop(2).Text("IVA incluido").FontColor(Colors.Grey.Darken1);

                    col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    col.Item().AlignCenter().Text("¡Gracias por su visita!").Bold();
                });
            });
        });

        return documento.GeneratePdf();
    }
}
