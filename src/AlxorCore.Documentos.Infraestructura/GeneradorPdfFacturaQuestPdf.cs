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
    // Colores de la marca Vetis. QuestPDF convierte el hex a Color de forma implícita.
    private const string Acento = "#E07E22";   // naranja
    private const string Marino = "#0E2036";   // azul marino (cabecera/pie)
    private const string Crema = "#EFEEE6";    // crema (fondos suaves)
    private const string Tarjeta = "#F5F6F8";  // gris muy claro (tarjetas)

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
        var dir = DireccionEmisor(emisor);
        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(0);
                pagina.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken4));

                // Banda de cabecera a todo el ancho, en azul marino de la marca.
                pagina.Header().Background(Marino).PaddingVertical(28).PaddingHorizontal(40).Row(fila =>
                {
                    fila.RelativeItem().Row(izq =>
                    {
                        if (logo is not null)
                        {
                            izq.ConstantItem(72).PaddingRight(16).AlignMiddle()
                                .Background(Colors.White).Padding(6).Height(72).Image(logo).FitArea();
                        }

                        izq.RelativeItem().AlignMiddle().Column(col =>
                        {
                            col.Item().Text(emisor.RazonSocial).Bold().FontSize(21).FontColor(Colors.White);
                            col.Item().PaddingTop(2).Text($"NIF: {emisor.Nif}").FontSize(10).FontColor("#B9C2CE");
                            if (!string.IsNullOrWhiteSpace(dir))
                            {
                                col.Item().Text(dir).FontSize(9).FontColor("#B9C2CE");
                            }
                        });
                    });
                    fila.ConstantItem(190).AlignMiddle().Column(col =>
                    {
                        col.Item().AlignRight().Text("FACTURA").Bold().FontSize(28).FontColor(Acento);
                        col.Item().AlignRight().PaddingTop(2).Text(factura.NumeroCompleto).Bold().FontSize(12).FontColor(Colors.White);
                        col.Item().AlignRight().Text($"{factura.FechaEmision:dd/MM/yyyy}").FontSize(10).FontColor("#B9C2CE");
                    });
                });

                pagina.Content().PaddingHorizontal(40).PaddingTop(26).Column(col =>
                {
                    // Tarjeta de cliente con fondo suave, para dar peso visual.
                    col.Item().Background(Tarjeta).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(14).Row(cli =>
                    {
                        cli.RelativeItem().Column(c =>
                        {
                            c.Item().Text("FACTURAR A").Bold().FontSize(9).FontColor(Acento).LineHeight(1f);
                            c.Item().PaddingTop(4).Text(factura.ClienteNombre).Bold().FontSize(13);
                            if (!string.IsNullOrWhiteSpace(factura.ClienteNif))
                            {
                                c.Item().Text($"NIF: {factura.ClienteNif}").FontColor(Colors.Grey.Darken2);
                            }
                        });
                        cli.ConstantItem(170).AlignRight().Column(c =>
                        {
                            c.Item().Text("FECHA DE EMISIÓN").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{factura.FechaEmision:dd/MM/yyyy}").Bold();
                            if (factura.FechaVencimiento != factura.FechaEmision)
                            {
                                c.Item().PaddingTop(4).Text("VENCIMIENTO").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
                                c.Item().Text($"{factura.FechaVencimiento:dd/MM/yyyy}").Bold();
                            }
                        });
                    });

                    col.Item().PaddingTop(20).Table(tabla =>
                    {
                        tabla.ColumnsDefinition(columnas =>
                        {
                            columnas.RelativeColumn(4);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1.3f);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1.3f);
                        });

                        tabla.Header(encabezado =>
                        {
                            static IContainer Th(IContainer c) => c.Background(Marino).PaddingVertical(8).PaddingHorizontal(8);
                            encabezado.Cell().Element(Th).Text("Descripción").Bold().FontColor(Colors.White);
                            encabezado.Cell().Element(Th).AlignRight().Text("Cant.").Bold().FontColor(Colors.White);
                            encabezado.Cell().Element(Th).AlignRight().Text("Precio").Bold().FontColor(Colors.White);
                            encabezado.Cell().Element(Th).AlignRight().Text("IVA").Bold().FontColor(Colors.White);
                            encabezado.Cell().Element(Th).AlignRight().Text("Total").Bold().FontColor(Colors.White);
                        });

                        var fila = 0;
                        foreach (var linea in factura.Lineas)
                        {
                            var fondo = fila++ % 2 == 0 ? "#FFFFFF" : Crema;
                            IContainer Td(IContainer c) => c.Background(fondo).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(7).PaddingHorizontal(8);
                            tabla.Cell().Element(Td).Text(linea.Descripcion);
                            tabla.Cell().Element(Td).AlignRight().Text(Redondeo.Formatear(linea.Cantidad));
                            tabla.Cell().Element(Td).AlignRight().Text($"{Redondeo.Formatear(linea.PrecioUnitario)} €");
                            tabla.Cell().Element(Td).AlignRight().Text(linea.PorcentajeIva == 0 ? "Sin IVA" : $"{linea.PorcentajeIva:0}%");
                            tabla.Cell().Element(Td).AlignRight().Text($"{Redondeo.Formatear(linea.Base + linea.CuotaIva)} €").SemiBold();
                        }
                    });

                    col.Item().AlignRight().PaddingTop(18).Width(260).Column(totales =>
                    {
                        totales.Item().PaddingHorizontal(4).Row(f => { f.RelativeItem().Text("Base imponible").FontColor(Colors.Grey.Darken2); f.ConstantItem(110).AlignRight().Text($"{Redondeo.Formatear(factura.BaseImponible)} €"); });
                        totales.Item().PaddingHorizontal(4).PaddingTop(3).Row(f => { f.RelativeItem().Text("IVA").FontColor(Colors.Grey.Darken2); f.ConstantItem(110).AlignRight().Text($"{Redondeo.Formatear(factura.CuotaIva)} €"); });
                        if (factura.RecargoTotal > 0)
                        {
                            totales.Item().PaddingHorizontal(4).PaddingTop(3).Row(f => { f.RelativeItem().Text("Recargo equiv.").FontColor(Colors.Grey.Darken2); f.ConstantItem(110).AlignRight().Text($"{Redondeo.Formatear(factura.RecargoTotal)} €"); });
                        }

                        if (factura.RetencionIrpf > 0)
                        {
                            totales.Item().PaddingHorizontal(4).PaddingTop(3).Row(f => { f.RelativeItem().Text($"Retención IRPF ({factura.PorcentajeIrpf:0}%)").FontColor(Colors.Grey.Darken2); f.ConstantItem(110).AlignRight().Text($"-{Redondeo.Formatear(factura.RetencionIrpf)} €"); });
                        }

                        // Caja de TOTAL destacada (fondo naranja, texto blanco) para que salte a la vista.
                        totales.Item().PaddingTop(10).Background(Acento).PaddingVertical(11).PaddingHorizontal(14).Row(f =>
                        {
                            f.RelativeItem().AlignMiddle().Text("TOTAL").Bold().FontSize(15).FontColor(Colors.White);
                            f.ConstantItem(130).AlignRight().AlignMiddle().Text($"{Redondeo.Formatear(factura.Total)} €").Bold().FontSize(17).FontColor(Colors.White);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(factura.Observaciones))
                    {
                        col.Item().PaddingTop(24).Background(Tarjeta).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(obs =>
                        {
                            obs.Item().Text("OBSERVACIONES").Bold().FontSize(9).FontColor(Acento);
                            obs.Item().PaddingTop(3).Text(factura.Observaciones).FontColor(Colors.Grey.Darken2);
                        });
                    }
                });

                // Pie a todo el ancho, en azul marino, con nota de agradecimiento.
                pagina.Footer().Background(Marino).PaddingVertical(14).PaddingHorizontal(40).Column(pie =>
                {
                    pie.Item().AlignCenter().Text("Gracias por confiar el cuidado de tu mascota en nosotros").FontColor(Colors.White).FontSize(10);
                    pie.Item().AlignCenter().PaddingTop(2).Text(texto =>
                    {
                        texto.Span(emisor.RazonSocial).FontColor("#B9C2CE").FontSize(9);
                        if (!string.IsNullOrWhiteSpace(emisor.Nif))
                        {
                            texto.Span($"  ·  NIF {emisor.Nif}").FontColor("#B9C2CE").FontSize(9);
                        }

                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            texto.Span($"  ·  {dir}").FontColor("#B9C2CE").FontSize(9);
                        }
                    });
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
