// Controllers/Devis/DevisPDFService.cs
// ✅ VERSION AMÉLIORÉE - Remises + Détection Devis Classique
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace Saf_alu_ci_Api.Controllers.Devis
{
    public class DevisPDFService
    {
        private readonly string _logoPath;

        private NumberFormatInfo GetXOFFormat()
        {
            return new NumberFormatInfo
            {
                NumberDecimalSeparator = ".",
                NumberGroupSeparator = " ",
                NumberDecimalDigits = 0,
                CurrencyDecimalSeparator = ".",
                CurrencyGroupSeparator = " ",
                CurrencyDecimalDigits = 0
            };
        }

        public DevisPDFService()
        {
            try
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                _logoPath = Path.Combine(basePath, "wwwroot", "images", "logo.png");

                if (!File.Exists(_logoPath))
                {
                    var currentDir = Directory.GetCurrentDirectory();
                    _logoPath = Path.Combine(currentDir, "wwwroot", "images", "logo.png");
                }

                if (!File.Exists(_logoPath))
                {
                    var parentPath = Directory.GetParent(basePath)?.FullName;
                    if (parentPath != null)
                    {
                        _logoPath = Path.Combine(parentPath, "wwwroot", "images", "logo.png");
                    }
                }

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

        // ✅ NOUVEAU: Détecte si le devis est un "devis classique"
        private bool IsDevisClassique(DevisCompletResponse devis)
        {
            // Un devis classique a une seule section nommée "Devis Classique"
            // ET aucune ligne n'a de longueur/hauteur renseignée
            if (devis.Sections == null || devis.Sections.Count != 1)
                return false;

            var section = devis.Sections.First();
            if (section.Nom != "Devis Classique")
                return false;

            // Vérifier qu'aucune ligne n'a L ou H
            if (section.Lignes == null || !section.Lignes.Any())
                return false;

            var hasLongueurOuHauteur = section.Lignes.Any(l => l.Longueur.HasValue || l.Hauteur.HasValue);
            return !hasLongueurOuHauteur;
        }

        // ✅ NOUVEAU: Calcule le montant HT brut (avant remises)
        private decimal CalculerMontantHTBrut(DevisCompletResponse devis)
        {
            if (devis.Sections == null || !devis.Sections.Any())
                return devis.MontantHT;

            decimal total = 0;
            foreach (var section in devis.Sections)
            {
                if (section.Lignes != null)
                {
                    total += section.Lignes.Sum(l => l.TotalHT);
                }
            }
            return total;
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

        private string FormatMontant(decimal montant)
        {
            try
            {
                var montantArrondi = Math.Round(montant, 0);
                var xofFormat = GetXOFFormat();
                return montantArrondi.ToString("N0", xofFormat);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DevisPDFService] ERROR formatting amount {montant}: {ex.Message}");
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
                            row.AutoItem()
                                .Width(60)
                                .Height(60)
                                .Background(Colors.Blue.Darken2);
                        }
                    }
                    else
                    {
                        row.AutoItem()
                            .Width(60)
                            .Height(60)
                            .Background(Colors.Blue.Darken2);
                    }
                });

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
            // ✅ Déterminer le type de devis
            bool isClassique = IsDevisClassique(devis);

            // ✅ Calculer les montants avec remises
            bool hasRemise = (devis.RemiseValeur > 0 || devis.RemisePourcentage > 0);
            decimal montantHTBrut = hasRemise ? CalculerMontantHTBrut(devis) : devis.MontantHT;

            container.PaddingTop(15).Column(column =>
            {
                column.Item().Table(table =>
                {
                    // ✅ COLONNES CONDITIONNELLES
                    table.ColumnsDefinition(columns =>
                    {
                        if (isClassique)
                        {
                            // Devis Classique: DESIGNATION, QTE, P.UNITAIRE, P.TOTAL
                            columns.RelativeColumn(5);     // DESIGNATION (plus large)
                            columns.ConstantColumn(50);    //UNITÉ
                            columns.ConstantColumn(40);    // QTE
                            columns.ConstantColumn(80);    // P.UNITAIRE
                            columns.ConstantColumn(80);    // P.TOTAL
                        }
                        else
                        {
                            // Devis Technique: DESIGNATION, L, H, QTE, P.UNITAIRE, P.TOTAL
                            columns.RelativeColumn(4);     // DESIGNATION
                            columns.ConstantColumn(40);    // L
                            columns.ConstantColumn(40);    // H
                            columns.ConstantColumn(35);    // QTE
                            columns.ConstantColumn(70);    // P.UNITAIRE
                            columns.ConstantColumn(75);    // P.TOTAL
                        }
                    });

                    // ✅ HEADER CONDITIONNEL
                    // HEADER CONDITIONNEL
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Medium).Padding(5)
                            .Text("DESIGNATION").FontColor(Colors.White).Bold().FontSize(9);

                        if (!isClassique)
                        {
                            header.Cell().Background(Colors.Grey.Medium).Padding(5)
                                .AlignCenter().Text("L").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background(Colors.Grey.Medium).Padding(5)
                                .AlignCenter().Text("H").FontColor(Colors.White).Bold().FontSize(9);
                        }

                       

                        header.Cell().Background(Colors.Grey.Medium).Padding(5)
                            .AlignCenter().Text("QTE").FontColor(Colors.White).Bold().FontSize(9);
                        // 🆕 Colonne UNITE uniquement pour le classique
                        if (isClassique)
                        {
                            header.Cell().Background(Colors.Grey.Medium).Padding(5)
                                .AlignCenter().Text("UNITE").FontColor(Colors.White).Bold().FontSize(9);
                        }
                        header.Cell().Background(Colors.Grey.Medium).Padding(5)
                            .AlignRight().Text("P.UNITAIRE").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Medium).Padding(5)
                            .AlignRight().Text("P.TOTAL").FontColor(Colors.White).Bold().FontSize(9);
                    });
                    // ✅ LIGNES
                    if (devis.Sections != null && devis.Sections.Any())
                    {
                        foreach (var section in devis.Sections.OrderBy(s => s.Ordre))
                        {
                            // Titre de section (sauf si "Devis Classique")
                            if (!isClassique || section.Nom != "Devis Classique")
                            {
                                int colspan = isClassique ? 5 : 6;
                                table.Cell().ColumnSpan((uint)colspan).Background(Colors.Grey.Lighten3)
                                    .Padding(5).Text(section.Nom).FontSize(9).Bold().FontColor(Colors.Blue.Darken2);
                            }

                            if (section.Lignes != null && section.Lignes.Any())
                            {
                                foreach (var ligne in section.Lignes.OrderBy(l => l.Ordre))
                                {
                                    // DESIGNATION
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

                                    // ✅ L et H (uniquement si devis technique)
                                    if (!isClassique)
                                    {
                                        table.Cell().Padding(4).AlignCenter()
                                            .Text(ligne.Longueur.HasValue ? FormatMontant(ligne.Longueur.Value) : "-")
                                            .FontSize(8);

                                        table.Cell().Padding(4).AlignCenter()
                                            .Text(ligne.Hauteur.HasValue ? FormatMontant(ligne.Hauteur.Value) : "-")
                                            .FontSize(8);
                                    }

                                    // QTE
                                    table.Cell().Padding(4).AlignCenter()
                                        .Text(FormatMontant(ligne.Quantite)).FontSize(8);

                                    //UNITE (classique uniquement)
                                    if (isClassique)
                                    {
                                        table.Cell().Padding(4).AlignCenter()
                                            .Text(!string.IsNullOrEmpty(ligne.Unite) ? ligne.Unite : "—")
                                            .FontSize(8);
                                    }

                                    // P.UNITAIRE
                                    table.Cell().Padding(4).AlignRight()
                                        .Text(FormatMontant(ligne.PrixUnitaireHT)).FontSize(8);

                                    // P.TOTAL
                                    table.Cell().Padding(4).AlignRight()
                                        .Text(FormatMontant(ligne.TotalHT)).FontSize(8).Bold();
                                }
                            }
                        }
                    }
                });

                // ✅ TOTAUX AVEC REMISES
                column.Item().PaddingTop(15).AlignRight().Column(totalCol =>
                {
                    // Si remise, afficher le montant HT brut
                    if (hasRemise)
                    {
                        totalCol.Item().PaddingBottom(5).Row(row =>
                        {
                            row.ConstantItem(150).Text("MONTANT HT BRUT").FontSize(10);
                            row.ConstantItem(100).AlignRight()
                                .Text(FormatMontant(montantHTBrut))
                                .FontSize(10);
                        });

                        // Remise en pourcentage
                        if (devis.RemisePourcentage > 0)
                        {
                            decimal montantRemisePourcentage = montantHTBrut * (devis.RemisePourcentage / 100);
                            totalCol.Item().PaddingBottom(5).Row(row =>
                            {
                                row.ConstantItem(150).Text($"Remise {devis.RemisePourcentage:0.##}%").FontSize(10).FontColor(Colors.Green.Darken1);
                                row.ConstantItem(100).AlignRight()
                                    .Text($"- {FormatMontant(montantRemisePourcentage)}")
                                    .FontSize(10).FontColor(Colors.Green.Darken1);
                            });
                        }

                        // Remise en valeur
                        if (devis.RemiseValeur > 0)
                        {
                            totalCol.Item().PaddingBottom(5).Row(row =>
                            {
                                row.ConstantItem(150).Text("Remise forfaitaire").FontSize(10).FontColor(Colors.Green.Darken1);
                                row.ConstantItem(100).AlignRight()
                                    .Text($"- {FormatMontant(devis.RemiseValeur)}")
                                    .FontSize(10).FontColor(Colors.Green.Darken1);
                            });
                        }

                        // Remise totale
                        decimal remiseTotal = montantHTBrut - devis.MontantHT;
                        totalCol.Item().BorderTop(1).BorderColor(Colors.Green.Darken1)
                            .PaddingTop(5).PaddingBottom(5).Row(row =>
                            {
                                row.ConstantItem(150).Text("REMISE TOTALE").FontSize(10).Bold().FontColor(Colors.Green.Darken1);
                                row.ConstantItem(100).AlignRight()
                                    .Text($"- {FormatMontant(remiseTotal)}")
                                    .FontSize(10).Bold().FontColor(Colors.Green.Darken1);
                            });
                    }

                    // Montant HT Net (ou HT si pas de remise)
                    totalCol.Item().BorderTop(2).BorderColor(Colors.Black)
                        .PaddingTop(8).PaddingBottom(8)
                        .Row(row =>
                        {
                            row.ConstantItem(150).Text(hasRemise ? "MONTANT TOTAL HT NET" : "MONTANT TOTAL HT").FontSize(11).Bold();
                            row.ConstantItem(100).AlignRight()
                                .Text(FormatMontant(devis.MontantHT))
                                .FontSize(11).Bold();
                        });

                    // ✅ Badge économie (si remise)
                    if (hasRemise)
                    {
                        decimal pourcentageEconomie = montantHTBrut > 0
                            ? Math.Round((montantHTBrut - devis.MontantHT) / montantHTBrut * 100, 2)
                            : 0;

                        totalCol.Item().PaddingTop(5).Row(row =>
                        {
                            row.ConstantItem(250).AlignRight()
                                .Background(Colors.Green.Lighten3)
                                .Padding(5)
                                .Text($"Économie : {FormatMontant(montantHTBrut - devis.MontantHT)} FCFA ({pourcentageEconomie:0.##}%)")
                                .FontSize(9)
                                .Bold()
                                .FontColor(Colors.Green.Darken2);
                        });
                    }
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
                        text.Span("Email infos@safalu-ci.com - 01 01 02 00 81 / 08 BP 2932 Abidjan 08")
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