// Controllers/Devis/DevisPDFService.cs
// ✅ VERSION CORRIGÉE - Initialisation sécurisée
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace Saf_alu_ci_Api.Controllers.Devis
{
    public class DevisPDFService
    {
        private readonly string _logoPath;

        // ✅ CORRECTION: Initialisation dans une méthode au lieu de statique
        private NumberFormatInfo GetXOFFormat()
        {
            return new NumberFormatInfo
            {
                NumberDecimalSeparator = ".",      // Point (requis par .NET)
                NumberGroupSeparator = " ",        // Espace
                NumberDecimalDigits = 0,           // Pas de décimales
                CurrencyDecimalSeparator = ".",
                CurrencyGroupSeparator = " ",
                CurrencyDecimalDigits = 0
            };
        }

        public DevisPDFService()
        {
            try
            {
                // Obtenir le dossier de base de l'application
                var basePath = AppDomain.CurrentDomain.BaseDirectory;

                // Construire le chemin vers wwwroot/images/logo.png
                _logoPath = Path.Combine(basePath, "wwwroot", "images", "logo.png");

                // Si le logo n'existe pas, essayer le répertoire de travail actuel
                if (!File.Exists(_logoPath))
                {
                    var currentDir = Directory.GetCurrentDirectory();
                    _logoPath = Path.Combine(currentDir, "wwwroot", "images", "logo.png");
                }

                // Si toujours pas trouvé, essayer le dossier parent
                if (!File.Exists(_logoPath))
                {
                    var parentPath = Directory.GetParent(basePath)?.FullName;
                    if (parentPath != null)
                    {
                        _logoPath = Path.Combine(parentPath, "wwwroot", "images", "logo.png");
                    }
                }

                // Log pour debug
                Console.WriteLine($"[DevisPDFService] Initialized successfully");
                Console.WriteLine($"[DevisPDFService] Logo path: {_logoPath}");
                Console.WriteLine($"[DevisPDFService] Logo exists: {File.Exists(_logoPath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DevisPDFService] ERROR during initialization: {ex.Message}");
                throw;
            }
        }

        public byte[] GeneratePDF(DevisCompletResponse devis)
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                        page.Header().Element(c => ComposeHeader(c, devis));
                        page.Content().Element(c => ComposeContent(c, devis));
                        page.Footer().Element(ComposeFooter);
                    });
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DevisPDFService] ERROR generating PDF: {ex.Message}");
                Console.WriteLine($"[DevisPDFService] Stack trace: {ex.StackTrace}");
                throw new Exception($"Erreur lors de la génération du PDF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Formate un montant au format XOF/FCFA (espace comme séparateur de milliers)
        /// </summary>
        private string FormatMontant(decimal montant)
        {
            try
            {
                // Arrondir à l'entier le plus proche
                var montantArrondi = Math.Round(montant, 0);

                // Formater avec le format XOF
                var xofFormat = GetXOFFormat();
                return montantArrondi.ToString("N0", xofFormat);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DevisPDFService] ERROR formatting amount {montant}: {ex.Message}");
                // Fallback: formater simplement sans séparateur
                return Math.Round(montant, 0).ToString("0");
            }
        }

        void ComposeHeader(IContainer container, DevisCompletResponse devis)
        {
            container.Column(column =>
            {
                // Logo
                column.Item().Row(row =>
                {
                    if (File.Exists(_logoPath))
                    {
                        try
                        {
                            row.AutoItem()
                                .Height(60)
                                .Image(_logoPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DevisPDFService] ERROR loading logo: {ex.Message}");
                            // Fallback si erreur de chargement du logo
                            row.AutoItem()
                                .Width(60)
                                .Height(60)
                                .Background(Colors.Blue.Darken2);
                        }
                    }
                    else
                    {
                        // Fallback: carré bleu
                        row.AutoItem()
                            .Width(60)
                            .Height(60)
                            .Background(Colors.Blue.Darken2);
                    }
                });

                // Séparateur
                //column.Item()
                //    .PaddingTop(5)
                //    .PaddingBottom(5)
                //    .LineHorizontal(1.5f)
                //    .LineColor(Colors.Red.Medium);

                // Informations du proformat/devis
                //Espacement
                column.Item().Height(10);

                column.Item().Background(Colors.Grey.Lighten4).Padding(8).Column(infoCol =>
                {
                    infoCol.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"Proformat N° {devis.Numero}").FontSize(11).Bold();
                        r.RelativeItem().AlignRight().Text($"Date : {devis.DateCreation:dd/MM/yyyy}").FontSize(9);
                    });

                    if (!string.IsNullOrEmpty(devis.Client?.Nom))
                    {
                        infoCol.Item().PaddingTop(3).Text(text =>
                        {
                            text.Span("Client : ").FontSize(9);
                            text.Span(devis.Client.Nom).FontSize(9).Bold();
                        });
                    }

                    if (!string.IsNullOrEmpty(devis.Contact))
                    {
                        infoCol.Item().Text(text =>
                        {
                            text.Span("Contact : ").FontSize(9);
                            text.Span(devis.Contact).FontSize(9);
                        });
                    }

                    if (!string.IsNullOrEmpty(devis.Chantier))
                    {
                        infoCol.Item().Text(text =>
                        {
                            text.Span("Chantier : ").FontSize(9);
                            text.Span(devis.Chantier).FontSize(9).Bold();
                        });
                    }
                });

                // Qualité matériel et vitrage
                if (!string.IsNullOrEmpty(devis.QualiteMateriel) || !string.IsNullOrEmpty(devis.TypeVitrage))
                {
                    column.Item().PaddingTop(10).Column(qualiteCol =>
                    {
                        qualiteCol.Item().Text("QUALITE MATERIEL").FontSize(10).Bold().FontColor(Colors.Red.Darken1);

                        if (!string.IsNullOrEmpty(devis.QualiteMateriel))
                        {
                            qualiteCol.Item().Text(devis.QualiteMateriel).FontSize(9).Bold();
                        }

                        if (!string.IsNullOrEmpty(devis.TypeVitrage))
                        {
                            qualiteCol.Item().Text(devis.TypeVitrage).FontSize(9);
                        }
                    });
                }
            });
        }

        void ComposeContent(IContainer container, DevisCompletResponse devis)
        {
            container.PaddingTop(15).Column(column =>
            {
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4);
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(35);
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(75);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Medium).Padding(5)
                            .Text("DESIGNATION").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Medium).Padding(5)
                            .AlignCenter().Text("L").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Medium).Padding(5)
                            .AlignCenter().Text("H").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Medium).Padding(5)
                            .AlignCenter().Text("QTE").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Medium).Padding(5)
                            .AlignRight().Text("P.UNITAIRE").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Medium).Padding(5)
                            .AlignRight().Text("P.TOTAL").FontColor(Colors.White).Bold().FontSize(9);
                    });

                    if (devis.Sections != null && devis.Sections.Any())
                    {
                        foreach (var section in devis.Sections.OrderBy(s => s.Ordre))
                        {
                            table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten3)
                                .Padding(5).Text(section.Nom).FontSize(9).Bold().FontColor(Colors.Blue.Darken2);

                            if (section.Lignes != null && section.Lignes.Any())
                            {
                                foreach (var ligne in section.Lignes.OrderBy(l => l.Ordre))
                                {
                                    table.Cell().Padding(4).Column(col =>
                                    {
                                        if (!string.IsNullOrEmpty(ligne.TypeElement))
                                        {
                                            col.Item().Text(ligne.TypeElement).FontSize(8).Bold();
                                        }
                                        else if (!string.IsNullOrEmpty(ligne.Designation))
                                        {
                                            col.Item().Text(ligne.Designation).FontSize(8).Bold();
                                        }
                                    });

                                    table.Cell().Padding(4).AlignCenter()
                                        .Text(ligne.Longueur.HasValue ? FormatMontant(ligne.Longueur.Value) : "-")
                                        .FontSize(8);

                                    table.Cell().Padding(4).AlignCenter()
                                        .Text(ligne.Hauteur.HasValue ? FormatMontant(ligne.Hauteur.Value) : "-")
                                        .FontSize(8);

                                    table.Cell().Padding(4).AlignCenter()
                                        .Text(FormatMontant(ligne.Quantite)).FontSize(8);

                                    table.Cell().Padding(4).AlignRight()
                                        .Text(FormatMontant(ligne.PrixUnitaireHT)).FontSize(8);

                                    table.Cell().Padding(4).AlignRight()
                                        .Text(FormatMontant(ligne.TotalHT)).FontSize(8).Bold();
                                }
                            }
                        }
                    }
                });

                column.Item().PaddingTop(15).AlignRight().Column(totalCol =>
                {
                    totalCol.Item().BorderTop(2).BorderColor(Colors.Black)
                        .PaddingTop(8).PaddingBottom(8)
                        .Row(row =>
                        {
                            row.ConstantItem(150).Text("MONTANT TOTAL HT").FontSize(11).Bold();
                            row.ConstantItem(100).AlignRight()
                                .Text(FormatMontant(devis.MontantHT))
                                .FontSize(11).Bold();
                        });
                });

                column.Item().PaddingTop(40).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Signature Client").FontSize(9).Bold();
                        col.Item().PaddingTop(40).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    row.ConstantItem(50);

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Signature et cachet").FontSize(9).Bold();
                        col.Item().PaddingTop(40).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });
                });
            });
        }

        void ComposeFooter(IContainer container)
        {
            container.Column(column =>
            {
                column.Item()
                    .PaddingBottom(5)
                    .LineHorizontal(1.5f)
                    .LineColor(Colors.Red.Medium);

                column.Item()
                    .AlignCenter()
                    .PaddingTop(5)
                    .Text(text =>
                    {
                        text.Span("Abidjan, Akouedo route de Bingerville ")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                        text.Span(" | ")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Medium);
                        text.Span("Email infos@safalu-ci.com - 27 22 23 29 64 / 08 BP 2932 Abidjan 08")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });

                column.Item()
                    .AlignCenter()
                    .Text("RC N°: CI ABJ-2018-B-29139 / CCN° 1858272P centre des impôts Abidjan Cocody - Bridge Bank 01105110006 27")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);
            });
        }
    }
}