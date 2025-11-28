using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace Saf_alu_ci_Api.Controllers.Dqe
{
    public class DQEExportService
    {
        private readonly DQEService _dqeService;
        private readonly CultureInfo _cultureInfo;

        public DQEExportService(DQEService dqeService)
        {
            _dqeService = dqeService;
            _cultureInfo = new CultureInfo("fr-FR");
        }

        // ========================================
        // EXPORT EXCEL
        // ========================================

        /// <summary>
        /// Exporte un DQE vers Excel avec formatage professionnel
        /// Structure : En-tête | Lots | Chapitres | Items | Totaux
        /// </summary>
        public async Task<byte[]> ExportToExcelAsync(int dqeId)
        {
            // Récupérer le DQE complet
            var dqe = await _dqeService.GetByIdAsync(dqeId);
            if (dqe == null)
            {
                throw new InvalidOperationException("DQE introuvable");
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"DQE-{dqe.Reference}");

            int currentRow = 1;

            // 1. EN-TÊTE DU DQE
            currentRow = CreateExcelHeader(worksheet, dqe, currentRow);

            // 2. TABLEAU DES LOTS, CHAPITRES ET ITEMS
            currentRow = CreateExcelDataTable(worksheet, dqe, currentRow);

            // 3. TOTAUX ET RÉCAPITULATIF
            currentRow = CreateExcelSummary(worksheet, dqe, currentRow);

            // 4. FORMATAGE ET MISE EN PAGE
            FormatExcelWorksheet(worksheet);

            // Retourner le fichier en mémoire
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        /// <summary>
        /// Crée l'en-tête du document Excel
        /// </summary>
        private int CreateExcelHeader(IXLWorksheet ws, DQEDetailDTO dqe, int startRow)
        {
            int row = startRow;

            // Logo ou titre entreprise
            ws.Cell(row, 1).Value = "SAF ALU CI";
            ws.Range(row, 1, row, 8).Merge();
            ws.Cell(row, 1).Style.Font.FontSize = 18;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0066CC");
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
            row += 2;

            // Titre du document
            ws.Cell(row, 1).Value = "DÉCOMPOSITION QUANTITATIVE ESTIMATIVE (DQE)";
            ws.Range(row, 1, row, 8).Merge();
            ws.Cell(row, 1).Style.Font.FontSize = 14;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row += 2;

            // Informations générales
            ws.Cell(row, 1).Value = "Référence :";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = dqe.Reference;
            ws.Range(row, 2, row, 4).Merge();

            ws.Cell(row, 6).Value = "Date :";
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 7).Value = dqe.DateCreation.ToString("dd/MM/yyyy");
            ws.Range(row, 7, row, 8).Merge();
            row++;

            ws.Cell(row, 1).Value = "Nom du projet :";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = dqe.Nom;
            ws.Range(row, 2, row, 8).Merge();
            row++;

            ws.Cell(row, 1).Value = "Client :";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = dqe.Client?.Nom ?? "N/A";
            ws.Range(row, 2, row, 4).Merge();

            ws.Cell(row, 6).Value = "Statut :";
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 7).Value = dqe.Statut.ToUpper();
            ws.Range(row, 7, row, 8).Merge();
            row++;

            if (!string.IsNullOrEmpty(dqe.Description))
            {
                ws.Cell(row, 1).Value = "Description :";
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 2).Value = dqe.Description;
                ws.Range(row, 2, row, 8).Merge();
                row++;
            }

            row++; // Ligne vide
            return row;
        }

        /// <summary>
        /// Crée le tableau des données (Lots, Chapitres, Items)
        /// </summary>
        private int CreateExcelDataTable(IXLWorksheet ws, DQEDetailDTO dqe, int startRow)
        {
            int row = startRow;

            // EN-TÊTES DE COLONNES
            var headerRow = ws.Row(row);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            headerRow.Style.Font.FontColor = XLColor.White;
            headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRow.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            ws.Cell(row, 1).Value = "CODE";
            ws.Cell(row, 2).Value = "DÉSIGNATION";
            ws.Cell(row, 3).Value = "UNITÉ";
            ws.Cell(row, 4).Value = "QUANTITÉ";
            ws.Cell(row, 5).Value = "PRIX UNITAIRE HT";
            ws.Cell(row, 6).Value = "MONTANT HT";
            ws.Cell(row, 7).Value = "% TOTAL";
            ws.Cell(row, 8).Value = "REMARQUES";

            row++;

            // PARCOURIR LES LOTS
            if (dqe.Lots != null && dqe.Lots.Any())
            {
                foreach (var lot in dqe.Lots.OrderBy(l => l.Ordre))
                {
                    // LIGNE LOT (en gras, fond gris clair)
                    var lotRow = ws.Row(row);
                    lotRow.Style.Font.Bold = true;
                    lotRow.Style.Font.FontSize = 12;
                    lotRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");

                    ws.Cell(row, 1).Value = lot.Code;
                    ws.Cell(row, 2).Value = lot.Nom.ToUpper();
                    ws.Cell(row, 3).Value = "";
                    ws.Cell(row, 4).Value = "";
                    ws.Cell(row, 5).Value = "";
                    ws.Cell(row, 6).Value = lot.TotalRevenueHT;
                    ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0 F";
                    ws.Cell(row, 7).Value = lot.PourcentageTotal / 100;
                    ws.Cell(row, 7).Style.NumberFormat.Format = "0.00%";
                    ws.Cell(row, 8).Value = lot.Description ?? "";

                    row++;

                    // PARCOURIR LES CHAPITRES
                    if (lot.Chapters != null && lot.Chapters.Any())
                    {
                        foreach (var chapter in lot.Chapters.OrderBy(c => c.Ordre))
                        {
                            // LIGNE CHAPITRE (en italique, légèrement indenté)
                            var chapterRow = ws.Row(row);
                            chapterRow.Style.Font.Italic = true;
                            chapterRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7E6E6");

                            ws.Cell(row, 1).Value = $"  {chapter.Code}";
                            ws.Cell(row, 2).Value = $"  {chapter.Nom}";
                            ws.Cell(row, 3).Value = "";
                            ws.Cell(row, 4).Value = "";
                            ws.Cell(row, 5).Value = "";
                            ws.Cell(row, 6).Value = chapter.TotalRevenueHT;
                            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0 F";
                            ws.Cell(row, 7).Value = "";
                            ws.Cell(row, 8).Value = chapter.Description ?? "";

                            row++;

                            // PARCOURIR LES ITEMS
                            if (chapter.Items != null && chapter.Items.Any())
                            {
                                foreach (var item in chapter.Items.OrderBy(i => i.Ordre))
                                {
                                    // LIGNE ITEM (normal, indenté)
                                    ws.Cell(row, 1).Value = $"    {item.Code}";
                                    ws.Cell(row, 2).Value = $"    {item.Designation}";
                                    ws.Cell(row, 3).Value = item.Unite;
                                    ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                    ws.Cell(row, 4).Value = item.Quantite;
                                    ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                                    ws.Cell(row, 5).Value = item.PrixUnitaireHT;
                                    ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0 F";
                                    ws.Cell(row, 6).Value = item.TotalRevenueHT;
                                    ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0 F";
                                    ws.Cell(row, 7).Value = "";
                                    ws.Cell(row, 8).Value = item.Description ?? "";

                                    row++;
                                }
                            }
                        }
                    }

                    // Ligne vide après chaque lot
                    row++;
                }
            }

            return row;
        }

        /// <summary>
        /// Crée le récapitulatif et les totaux
        /// </summary>
        private int CreateExcelSummary(IXLWorksheet ws, DQEDetailDTO dqe, int startRow)
        {
            int row = startRow + 1;

            // Bordure supérieure du récapitulatif
            ws.Range(row, 5, row, 8).Style.Border.TopBorder = XLBorderStyleValues.Thick;
            row++;

            // TOTAL HT
            ws.Cell(row, 5).Value = "TOTAL HT :";
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 6).Value = dqe.TotalRevenueHT;
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0 F";
            ws.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
            row++;

            // TVA
            ws.Cell(row, 5).Value = $"TVA ({dqe.TauxTVA}%) :";
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 6).Value = dqe.MontantTVA;
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0 F";
            row++;

            // TOTAL TTC
            ws.Cell(row, 5).Value = "TOTAL TTC :";
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 5).Style.Font.FontSize = 14;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 6).Value = dqe.TotalTTC;
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 6).Style.Font.FontSize = 14;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0 F";
            ws.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#C6E0B4");
            ws.Cell(row, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
            row++;

            row += 2;

            // Informations complémentaires
            if (dqe.IsConverted && dqe.LinkedProject != null)
            {
                ws.Cell(row, 1).Value = "⚠️ Ce DQE a été converti en projet";
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontColor = XLColor.OrangeRed;
                ws.Range(row, 1, row, 8).Merge();
                row++;

                ws.Cell(row, 1).Value = $"Projet : {dqe.LinkedProject.Numero} - {dqe.LinkedProject.Nom}";
                ws.Range(row, 1, row, 8).Merge();
                row++;

                ws.Cell(row, 1).Value = $"Date de conversion : {dqe.LinkedProject.ConvertedAt:dd/MM/yyyy HH:mm}";
                ws.Range(row, 1, row, 8).Merge();
                row++;
            }

            row += 2;

            // Pied de page
            ws.Cell(row, 1).Value = $"Document généré le {DateTime.Now:dd/MM/yyyy à HH:mm}";
            ws.Cell(row, 1).Style.Font.FontSize = 9;
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Range(row, 1, row, 4).Merge();

            ws.Cell(row, 6).Value = "SAF ALU CI - DQE";
            ws.Cell(row, 6).Style.Font.FontSize = 9;
            ws.Cell(row, 6).Style.Font.Italic = true;
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Range(row, 6, row, 8).Merge();

            return row;
        }

        /// <summary>
        /// Applique le formatage global du worksheet
        /// </summary>
        private void FormatExcelWorksheet(IXLWorksheet ws)
        {
            // Largeur des colonnes
            ws.Column(1).Width = 12;  // CODE
            ws.Column(2).Width = 45;  // DÉSIGNATION
            ws.Column(3).Width = 10;  // UNITÉ
            ws.Column(4).Width = 12;  // QUANTITÉ
            ws.Column(5).Width = 18;  // PRIX UNITAIRE
            ws.Column(6).Width = 18;  // MONTANT HT
            ws.Column(7).Width = 10;  // % TOTAL
            ws.Column(8).Width = 30;  // REMARQUES

            // Bordures pour toutes les cellules utilisées
            var usedRange = ws.RangeUsed();
            if (usedRange != null)
            {
                usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            // Alignement par défaut
            ws.Column(4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Column(5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Column(6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Column(7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Figer les volets (en-tête)
            ws.SheetView.FreezeRows(1);

            // Mise en page impression
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.Margins.Left = 0.5;
            ws.PageSetup.Margins.Right = 0.5;
            ws.PageSetup.Margins.Top = 0.75;
            ws.PageSetup.Margins.Bottom = 0.75;
        }

        // ========================================
        // EXPORT PDF
        // ========================================

        /// <summary>
        /// Exporte un DQE vers PDF avec formatage professionnel
        /// </summary>
        public async Task<byte[]> ExportToPdfAsync(int dqeId)
        {
            var dqe = await _dqeService.GetByIdAsync(dqeId);
            if (dqe == null)
            {
                throw new InvalidOperationException("DQE introuvable");
            }

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Calibri"));

                    // EN-TÊTE
                    page.Header().Element(c => CreatePdfHeader(c, dqe));

                    // CONTENU
                    page.Content().Element(c => CreatePdfContent(c, dqe));

                    // PIED DE PAGE
                    page.Footer().Element(c => CreatePdfFooter(c, dqe));
                });
            });

            return document.GeneratePdf();
        }

        private void CreatePdfHeader(IContainer container, DQEDetailDTO dqe)
        {
            container.Column(column =>
            {
                // Logo et titre
                column.Item().Background("#0066CC").Padding(10).Row(row =>
                {
                    row.RelativeItem().AlignLeft().Text("SAF ALU CI")
                        .FontSize(20).Bold().FontColor("#FFFFFF");

                    row.RelativeItem().AlignRight().Text("DQE")
                        .FontSize(20).Bold().FontColor("#FFFFFF");
                });

                column.Item().PaddingVertical(5);

                // Titre du document
                column.Item().Background("#E7E6E6").Padding(8).Text(text =>
                {
                    text.Span("DÉCOMPOSITION QUANTITATIVE ESTIMATIVE\n").FontSize(14).Bold();
                    text.Span(dqe.Nom).FontSize(12).SemiBold();
                });

                column.Item().PaddingVertical(5);

                // Informations générales
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(t =>
                        {
                            t.Span("Référence : ").Bold();
                            t.Span(dqe.Reference);
                        });
                        col.Item().Text(t =>
                        {
                            t.Span("Client : ").Bold();
                            t.Span(dqe.Client?.Nom ?? "N/A");
                        });
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(t =>
                        {
                            t.Span("Date : ").Bold();
                            t.Span(dqe.DateCreation.ToString("dd/MM/yyyy"));
                        });
                        col.Item().Text(t =>
                        {
                            t.Span("Statut : ").Bold();
                            t.Span(dqe.Statut.ToUpper());
                        });
                    });
                });

                column.Item().PaddingVertical(3).LineHorizontal(2).LineColor("#0066CC");
            });
        }

        private void CreatePdfContent(IContainer container, DQEDetailDTO dqe)
        {
            container.Table(table =>
            {
                // Colonnes
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(60);  // Code
                    columns.RelativeColumn(4);   // Désignation
                    columns.ConstantColumn(40);  // Unité
                    columns.ConstantColumn(60);  // Quantité
                    columns.ConstantColumn(80);  // Prix unitaire
                    columns.ConstantColumn(90);  // Montant HT
                    columns.ConstantColumn(50);  // %
                });

                // EN-TÊTE
                table.Header(header =>
                {
                    header.Cell().Background("#4472C4").Padding(5).Text("CODE").FontColor("#FFFFFF").Bold();
                    header.Cell().Background("#4472C4").Padding(5).Text("DÉSIGNATION").FontColor("#FFFFFF").Bold();
                    header.Cell().Background("#4472C4").Padding(5).Text("UNITÉ").FontColor("#FFFFFF").Bold();
                    header.Cell().Background("#4472C4").Padding(5).Text("QUANTITÉ").FontColor("#FFFFFF").Bold();
                    header.Cell().Background("#4472C4").Padding(5).Text("PRIX UNIT. HT").FontColor("#FFFFFF").Bold();
                    header.Cell().Background("#4472C4").Padding(5).Text("MONTANT HT").FontColor("#FFFFFF").Bold();
                    header.Cell().Background("#4472C4").Padding(5).Text("% TOTAL").FontColor("#FFFFFF").Bold();
                });

                // DONNÉES
                if (dqe.Lots != null && dqe.Lots.Any())
                {
                    foreach (var lot in dqe.Lots.OrderBy(l => l.Ordre))
                    {
                        // LIGNE LOT
                        table.Cell().Background("#D9E1F2").Padding(5).Text(lot.Code).Bold();
                        table.Cell().ColumnSpan(3).Background("#D9E1F2").Padding(5).Text(lot.Nom.ToUpper()).Bold();
                        table.Cell().Background("#D9E1F2").Padding(5).Text("");
                        table.Cell().Background("#D9E1F2").Padding(5).AlignRight().Text(FormatCurrency(lot.TotalRevenueHT)).Bold();
                        table.Cell().Background("#D9E1F2").Padding(5).AlignCenter().Text($"{lot.PourcentageTotal:F2}%").Bold();

                        // CHAPITRES ET ITEMS
                        if (lot.Chapters != null && lot.Chapters.Any())
                        {
                            foreach (var chapter in lot.Chapters.OrderBy(c => c.Ordre))
                            {
                                // LIGNE CHAPITRE
                                table.Cell().Background("#F2F2F2").Padding(5).Text($"  {chapter.Code}").Italic();
                                table.Cell().ColumnSpan(3).Background("#F2F2F2").Padding(5).Text($"  {chapter.Nom}").Italic();
                                table.Cell().Background("#F2F2F2").Padding(5).Text("");
                                table.Cell().Background("#F2F2F2").Padding(5).AlignRight().Text(FormatCurrency(chapter.TotalRevenueHT)).Italic();
                                table.Cell().Background("#F2F2F2").Padding(5).Text("");

                                // ITEMS
                                if (chapter.Items != null && chapter.Items.Any())
                                {
                                    foreach (var item in chapter.Items.OrderBy(i => i.Ordre))
                                    {
                                        table.Cell().Padding(5).Text($"    {item.Code}");
                                        table.Cell().Padding(5).Text($"    {item.Designation}");
                                        table.Cell().Padding(5).AlignCenter().Text(item.Unite);
                                        table.Cell().Padding(5).AlignRight().Text($"{item.Quantite:N2}");
                                        table.Cell().Padding(5).AlignRight().Text(FormatCurrency(item.PrixUnitaireHT));
                                        table.Cell().Padding(5).AlignRight().Text(FormatCurrency(item.TotalRevenueHT));
                                        table.Cell().Padding(5).Text("");
                                    }
                                }
                            }
                        }
                    }
                }

                // TOTAUX
                table.Cell().ColumnSpan(5).BorderTop(2).BorderColor("#0066CC").Padding(5).AlignRight().Text("TOTAL HT :").Bold().FontSize(11);
                table.Cell().BorderTop(2).BorderColor("#0066CC").Background("#FFF2CC").Padding(5).AlignRight().Text(FormatCurrency(dqe.TotalRevenueHT)).Bold().FontSize(11);
                table.Cell().BorderTop(2).BorderColor("#0066CC").Padding(5).Text("");

                table.Cell().ColumnSpan(5).Padding(5).AlignRight().Text($"TVA ({dqe.TauxTVA}%) :").Bold();
                table.Cell().Padding(5).AlignRight().Text(FormatCurrency(dqe.MontantTVA)).Bold();
                table.Cell().Padding(5).Text("");

                table.Cell().ColumnSpan(5).Padding(5).AlignRight().Text("TOTAL TTC :").Bold().FontSize(12);
                table.Cell().Background("#C6E0B4").Padding(5).AlignRight().Text(FormatCurrency(dqe.TotalTTC)).Bold().FontSize(12);
                table.Cell().Padding(5).Text("");
            });
        }

        private void CreatePdfFooter(IContainer container, DQEDetailDTO dqe)
        {
            container.Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(text =>
                {
                    text.Span($"Document généré le {DateTime.Now:dd/MM/yyyy à HH:mm}")
                        .FontSize(8).Italic().FontColor("#666666");
                });

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
                //.FontSize(8)
                //.Italic()
                //.FontColor("#666666");

            });
        }

        private string FormatCurrency(decimal amount)
        {
            return $"{amount:N0} F";
        }
    }
}
