// Controllers/Devis/DevisPDFService.cs
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Saf_alu_ci_Api.Controllers.Devis
{
    public class DevisPDFService
    {
        public byte[] GeneratePDF(DevisCompletResponse devis)
        {
            // Configuration de la licence (Community pour usage non commercial)
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

        void ComposeHeader(IContainer container, DevisCompletResponse devis)
        {
            container.Column(column =>
            {
                // Logo et informations entreprise
                column.Item().Row(row =>
                {
                    // Logo + Nom entreprise
                    row.RelativeItem().Column(logoCol =>
                    {
                        logoCol.Item().Text("☐ SAF ALU-CI").FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                        logoCol.Item().Text("BTP - MENUISERIE ALUMINIUM - DIVERS").FontSize(8).Italic();
                        logoCol.Item().PaddingTop(3).Text("+225 27 22 23 39 64 / 07 07 08 08 36").FontSize(8);
                    });
                });

                // Séparateur
                column.Item()
                    .PaddingBottom(5)
                    .LineHorizontal(1.5f)
                    .LineColor(Colors.Red.Medium);

                // Informations du proformat/devis
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
                // Table principale avec toutes les sections
                column.Item().Table(table =>
                {
                    // Définir les colonnes selon le PDF
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4);   // DESIGNATION
                        columns.ConstantColumn(40);  // L
                        columns.ConstantColumn(40);  // H
                        columns.ConstantColumn(35);  // QTE
                        columns.ConstantColumn(70);  // P.UNITAIRE
                        columns.ConstantColumn(75);  // P.TOTAL
                    });

                    // En-tête du tableau
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

                    // Parcourir toutes les sections
                    if (devis.Sections != null && devis.Sections.Any())
                    {
                        foreach (var section in devis.Sections.OrderBy(s => s.Ordre))
                        {
                            // Ligne de section (en-tête de catégorie)
                            table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten3)
                                .Padding(5).Text(section.Nom).FontSize(9).Bold().FontColor(Colors.Blue.Darken2);

                            // Lignes de la section
                            if (section.Lignes != null && section.Lignes.Any())
                            {
                                foreach (var ligne in section.Lignes.OrderBy(l => l.Ordre))
                                {
                                    // Désignation (TypeElement + Designation si différent)
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

                                    // Longueur
                                    table.Cell().Padding(4).AlignCenter()
                                        .Text(ligne.Longueur.HasValue ? ligne.Longueur.Value.ToString("N0") : "-")
                                        .FontSize(8);

                                    // Hauteur
                                    table.Cell().Padding(4).AlignCenter()
                                        .Text(ligne.Hauteur.HasValue ? ligne.Hauteur.Value.ToString("N0") : "-")
                                        .FontSize(8);

                                    // Quantité
                                    table.Cell().Padding(4).AlignCenter()
                                        .Text(ligne.Quantite.ToString("N0")).FontSize(8);

                                    // Prix unitaire
                                    table.Cell().Padding(4).AlignRight()
                                        .Text($"{ligne.PrixUnitaireHT:N0}").FontSize(8);

                                    // Total
                                    table.Cell().Padding(4).AlignRight()
                                        .Text($"{ligne.TotalHT:N0}").FontSize(8).Bold();
                                }
                            }
                        }
                    }
                });

                // Total HT
                column.Item().PaddingTop(15).AlignRight().Column(totalCol =>
                {
                    totalCol.Item().BorderTop(2).BorderColor(Colors.Black)
                        .PaddingTop(8).PaddingBottom(8)
                        .Row(row =>
                        {
                            row.ConstantItem(150).Text("MONTANT TOTAL HT").FontSize(11).Bold();
                            row.ConstantItem(100).AlignRight().Text($"{devis.MontantHT:N0}").FontSize(11).Bold();
                        });
                });

                // Espace pour les signatures
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
                // 🔴 Ligne de séparation rouge
                column.Item()
                    .PaddingBottom(5)
                    .LineHorizontal(1.5f)
                    .LineColor(Colors.Red.Medium);

                // Contenu du footer
                column.Item()
                    .AlignCenter()
                    .PaddingTop(5)
                    .Text(text =>
                    {
                        // Ligne 1
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

                // Ligne 2
                column.Item()
                    .AlignCenter()
                    .Text("RC N°: CI ABJ-2018-B-29139 / CCN° 1858272P centre des impôts Abidjan Cocody - Bridge Bank 01105110006 27")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);
            });
        }

    }
}