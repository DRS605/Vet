using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlxorCore.Documentos.Infraestructura;

/// <summary>Genera el PDF de una factura con QuestPDF. Diseño limpio y sobrio (español).</summary>
internal sealed class GeneradorPdfFacturaQuestPdf : IGeneradorPdfFactura
{
    public byte[] Generar(FacturaDto factura, EmpresaDto emisor)
    {
        ArgumentNullException.ThrowIfNull(factura);
        ArgumentNullException.ThrowIfNull(emisor);

        // Un ticket (factura simplificada) se imprime en formato rollo de 80 mm; el resto en A4.
        return string.Equals(factura.Tipo, "Simplificada", StringComparison.OrdinalIgnoreCase)
            ? GenerarTicket(factura, emisor)
            : GenerarFacturaA4(factura, emisor);
    }

    /// <summary>Genera el PNG del QR de cotejo VeriFactu, o null si la factura aún no tiene huella.</summary>
    private static byte[]? GenerarQr(FacturaDto factura, EmpresaDto emisor)
    {
        if (string.IsNullOrEmpty(factura.Huella))
        {
            return null;
        }

        var url = Verifactu.UrlCotejo(emisor.Nif, factura.NumeroCompleto, factura.FechaEmision, factura.Total);
        using var generador = new QRCodeGenerator();
        var datos = generador.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(datos).GetGraphic(12);
    }

    private static byte[] GenerarFacturaA4(FacturaDto factura, EmpresaDto emisor)
    {
        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(40);
                pagina.DefaultTextStyle(x => x.FontSize(10));

                pagina.Header().Row(fila =>
                {
                    fila.RelativeItem().Column(col =>
                    {
                        col.Item().Text(emisor.RazonSocial).Bold().FontSize(16);
                        col.Item().Text($"NIF: {emisor.Nif}");
                    });
                    fila.ConstantItem(200).AlignRight().Column(col =>
                    {
                        col.Item().Text("FACTURA").Bold().FontSize(16);
                        col.Item().Text(factura.NumeroCompleto);
                        col.Item().Text($"Fecha: {factura.FechaEmision:dd/MM/yyyy}");
                    });
                });

                pagina.Content().PaddingVertical(15).Column(col =>
                {
                    col.Item().PaddingBottom(10).Column(cliente =>
                    {
                        cliente.Item().Text("Cliente").Bold();
                        cliente.Item().Text(factura.ClienteNombre);
                        if (!string.IsNullOrWhiteSpace(factura.ClienteNif))
                        {
                            cliente.Item().Text($"NIF: {factura.ClienteNif}");
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
                            encabezado.Cell().Text("Descripción").Bold();
                            encabezado.Cell().AlignRight().Text("Cantidad").Bold();
                            encabezado.Cell().AlignRight().Text("Precio").Bold();
                            encabezado.Cell().AlignRight().Text("IVA").Bold();
                            encabezado.Cell().AlignRight().Text("Base").Bold();
                        });

                        foreach (var linea in factura.Lineas)
                        {
                            tabla.Cell().Text(linea.Descripcion);
                            tabla.Cell().AlignRight().Text(Redondeo.Formatear(linea.Cantidad));
                            tabla.Cell().AlignRight().Text(Redondeo.Formatear(linea.PrecioUnitario));
                            tabla.Cell().AlignRight().Text($"{linea.PorcentajeIva:0}%");
                            tabla.Cell().AlignRight().Text(Redondeo.Formatear(linea.Base));
                        }
                    });

                    col.Item().AlignRight().PaddingTop(15).Column(totales =>
                    {
                        totales.Item().Text($"Base imponible: {Redondeo.Formatear(factura.BaseImponible)} €");
                        totales.Item().Text($"IVA: {Redondeo.Formatear(factura.CuotaIva)} €");
                        if (factura.RecargoTotal > 0)
                        {
                            totales.Item().Text($"Recargo de equivalencia: {Redondeo.Formatear(factura.RecargoTotal)} €");
                        }

                        if (factura.RetencionIrpf > 0)
                        {
                            totales.Item().Text($"Retención IRPF ({factura.PorcentajeIrpf:0}%): -{Redondeo.Formatear(factura.RetencionIrpf)} €");
                        }

                        totales.Item().Text($"TOTAL: {Redondeo.Formatear(factura.Total)} €").Bold().FontSize(13);
                    });

                    if (!string.IsNullOrWhiteSpace(factura.Observaciones))
                    {
                        col.Item().PaddingTop(18).Column(obs =>
                        {
                            obs.Item().Text("Observaciones").Bold();
                            obs.Item().PaddingTop(2).Text(factura.Observaciones).FontColor(Colors.Grey.Darken2);
                        });
                    }

                    var qr = GenerarQr(factura, emisor);
                    if (qr is not null)
                    {
                        col.Item().PaddingTop(20).Row(fila =>
                        {
                            fila.ConstantItem(90).Image(qr);
                            fila.RelativeItem().PaddingLeft(12).AlignBottom().Column(vf =>
                            {
                                vf.Item().Text("Factura verificable en la sede electrónica de la AEAT").FontSize(8).FontColor(Colors.Grey.Darken1);
                                vf.Item().Text("VERI*FACTU").Bold().FontSize(9);
                                vf.Item().Text($"Huella: {factura.Huella![..16]}…").FontSize(7).FontColor(Colors.Grey.Medium);
                            });
                        });
                    }
                });

                pagina.Footer().AlignCenter().Text(texto =>
                {
                    texto.Span("ALXOR Core · ").FontColor(Colors.Grey.Medium);
                    texto.Span(emisor.RazonSocial).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return documento.GeneratePdf();
    }

    /// <summary>Genera el PDF de un ticket (factura simplificada) en formato rollo de 80 mm.</summary>
    private static byte[] GenerarTicket(FacturaDto factura, EmpresaDto emisor)
    {
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
                        f.RelativeItem().Text("TOTAL").Bold().FontSize(11);
                        f.ConstantItem(80).AlignRight().Text($"{Redondeo.Formatear(factura.Total)} €").Bold().FontSize(11);
                    });
                    col.Item().AlignCenter().PaddingTop(2).Text("IVA incluido").FontColor(Colors.Grey.Darken1);

                    var qr = GenerarQr(factura, emisor);
                    if (qr is not null)
                    {
                        col.Item().PaddingTop(6).AlignCenter().Width(90).Image(qr);
                        col.Item().AlignCenter().Text("VERI*FACTU").Bold().FontSize(8);
                        col.Item().AlignCenter().Text("Verificable en la sede de la AEAT").FontSize(7).FontColor(Colors.Grey.Darken1);
                    }

                    col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    col.Item().AlignCenter().Text("¡Gracias por su compra!").Bold();
                    col.Item().AlignCenter().PaddingTop(4).Text("ALXOR Core").FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return documento.GeneratePdf();
    }
}
