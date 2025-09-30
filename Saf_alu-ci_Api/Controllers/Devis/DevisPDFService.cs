// Controllers/Devis/DevisPDFService.cs
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Saf_alu_ci_Api.Controllers.Devis
{
    public class DevisPDFService
    {
        public byte[] GeneratePDF(Devis devis)
        {
            // Configuration de la licence (Community pour usage non commercial)
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(c => ComposeHeader(c, devis));
                    page.Content().Element(c => ComposeContent(c, devis));
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        void ComposeHeader(IContainer container, Devis devis)
        {
            container.Row(row =>
            {
                // Logo et informations entreprise
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("SAF ALU-CI").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                    column.Item().Text("Spécialiste en Aluminium et Menuiserie").FontSize(10).Italic();
                    column.Item().PaddingTop(5).Text("Abidjan, Côte d'Ivoire").FontSize(9);
                    column.Item().Text("Tél: +225 XX XX XX XX XX").FontSize(9);
                    column.Item().Text("Email: contact@safalu.ci").FontSize(9);
                });

                // Espace
                row.RelativeItem().Column(column => { });

                // Informations devis
                row.RelativeItem().Column(column =>
                {
                    column.Item().AlignRight().Text("DEVIS").FontSize(24).Bold().FontColor(Colors.Blue.Darken2);
                    column.Item().AlignRight().PaddingTop(5).Text($"N° {devis.Numero}").FontSize(11).Bold();
                    column.Item().AlignRight().Text($"Date: {devis.DateCreation:dd/MM/yyyy}").FontSize(9);
                    if (devis.DateValidite.HasValue)
                    {
                        column.Item().AlignRight().Text($"Valide jusqu'au: {devis.DateValidite.Value:dd/MM/yyyy}")
                            .FontSize(9).FontColor(Colors.Orange.Darken1);
                    }
                });
            });
        }

        void ComposeContent(IContainer container, Devis devis)
        {
            container.PaddingVertical(20).Column(column =>
            {
                column.Spacing(15);

                // Informations client
                column.Item().Element(c => ComposeClientInfo(c, devis));

                // Titre et description du projet
                column.Item().Element(c => ComposeProjectInfo(c, devis));

                // Tableau des lignes
                column.Item().Element(c => ComposeLignesTable(c, devis));

                // Totaux
                column.Item().Element(c => ComposeTotaux(c, devis));

                // Conditions
                if (!string.IsNullOrWhiteSpace(devis.Conditions))
                {
                    column.Item().Element(c => ComposeConditions(c, devis));
                }

                // Notes internes (optionnel - peut être commenté pour ne pas afficher au client)
                // if (!string.IsNullOrWhiteSpace(devis.Notes))
                // {
                //     column.Item().Element(c => ComposeNotes(c, devis));
                // }
            });
        }

        void ComposeClientInfo(IContainer container, Devis devis)
        {
            container.Background(Colors.Grey.Lighten3).Padding(10).Column(column =>
            {
                column.Item().Text("INFORMATIONS CLIENT").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                column.Item().PaddingTop(5).Text(text =>
                {
                    text.Span("Nom: ").Bold();
                    if (!string.IsNullOrEmpty(devis.Client?.RaisonSociale))
                    {
                        text.Span(devis.Client.RaisonSociale);
                    }
                    else
                    {
                        text.Span($"{devis.Client?.Prenom} {devis.Client?.Nom}".Trim());
                    }
                });

                if (!string.IsNullOrEmpty(devis.Client?.Email))
                {
                    column.Item().Text(text =>
                    {
                        text.Span("Email: ").Bold();
                        text.Span(devis.Client.Email);
                    });
                }

                if (!string.IsNullOrEmpty(devis.Client?.Telephone))
                {
                    column.Item().Text(text =>
                    {
                        text.Span("Téléphone: ").Bold();
                        text.Span(devis.Client.Telephone);
                    });
                }

                if (!string.IsNullOrEmpty(devis.Client?.Adresse))
                {
                    column.Item().Text(text =>
                    {
                        text.Span("Adresse: ").Bold();
                        text.Span(devis.Client.Adresse);
                    });
                }
            });
        }

        void ComposeProjectInfo(IContainer container, Devis devis)
        {
            container.Column(column =>
            {
                column.Item().Text("PROJET").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                column.Item().PaddingTop(5).Text(devis.Titre).FontSize(13).Bold();

                if (!string.IsNullOrWhiteSpace(devis.Description))
                {
                    column.Item().PaddingTop(5).Text(devis.Description).FontSize(10).Italic();
                }
            });
        }

        void ComposeLignesTable(IContainer container, Devis devis)
        {
            container.Column(column =>
            {
                column.Item().Text("DÉTAIL DES PRESTATIONS").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                column.Item().PaddingTop(10).Table(table =>
                {
                    // Définir les colonnes
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);  // N°
                        columns.RelativeColumn(4);   // Désignation
                        columns.ConstantColumn(50);  // Qté
                        columns.ConstantColumn(50);  // Unité
                        columns.ConstantColumn(80);  // P.U. HT
                        columns.ConstantColumn(90);  // Total HT
                    });

                    // En-tête
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("N°").FontColor(Colors.White).Bold().FontSize(9);

                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Désignation").FontColor(Colors.White).Bold().FontSize(9);

                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .AlignCenter().Text("Qté").FontColor(Colors.White).Bold().FontSize(9);

                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .AlignCenter().Text("Unité").FontColor(Colors.White).Bold().FontSize(9);

                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .AlignRight().Text("P.U. HT").FontColor(Colors.White).Bold().FontSize(9);

                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .AlignRight().Text("Total HT").FontColor(Colors.White).Bold().FontSize(9);
                    });

                    // Lignes
                    if (devis.Lignes != null)
                    {
                        int index = 1;
                        foreach (var ligne in devis.Lignes.OrderBy(l => l.Ordre))
                        {
                            var bgColor = index % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                            table.Cell().Background(bgColor).Padding(5)
                                .Text(index.ToString()).FontSize(9);

                            table.Cell().Background(bgColor).Padding(5).Column(c =>
                            {
                                c.Item().Text(ligne.Designation).FontSize(9).Bold();
                                if (!string.IsNullOrWhiteSpace(ligne.Description))
                                {
                                    c.Item().Text(ligne.Description).FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                                }
                            });

                            table.Cell().Background(bgColor).Padding(5)
                                .AlignCenter().Text(ligne.Quantite.ToString("N2")).FontSize(9);

                            table.Cell().Background(bgColor).Padding(5)
                                .AlignCenter().Text(ligne.Unite).FontSize(9);

                            table.Cell().Background(bgColor).Padding(5)
                                .AlignRight().Text($"{ligne.PrixUnitaireHT:N0} FCFA").FontSize(9);

                            table.Cell().Background(bgColor).Padding(5)
                                .AlignRight().Text($"{ligne.TotalHT:N0} FCFA").FontSize(9).Bold();

                            index++;
                        }
                    }
                });
            });
        }

        void ComposeTotaux(IContainer container, Devis devis)
        {
            container.AlignRight().Width(250).Column(column =>
            {
                column.Spacing(3);

                // Sous-total HT
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                    .Row(row =>
                    {
                        row.RelativeItem().Text("Sous-total HT:").FontSize(10);
                        row.ConstantItem(100).AlignRight().Text($"{devis.MontantHT:N0} FCFA").FontSize(10);
                    });

                // TVA
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5)
                    .Row(row =>
                    {
                        row.RelativeItem().Text($"TVA ({devis.TauxTVA}%):").FontSize(10);
                        var montantTVA = devis.MontantHT * (devis.TauxTVA / 100);
                        row.ConstantItem(100).AlignRight().Text($"{montantTVA:N0} FCFA").FontSize(10);
                    });

                // Total TTC
                column.Item().Background(Colors.Blue.Lighten4).Padding(8).PaddingVertical(10)
                    .Row(row =>
                    {
                        row.RelativeItem().Text("TOTAL TTC:").FontSize(12).Bold();
                        row.ConstantItem(100).AlignRight().Text($"{devis.MontantTTC:N0} FCFA")
                            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                    });

                // Montant en lettres
                column.Item().PaddingTop(5).Text(text =>
                {
                    text.Span("Arrêté à: ").FontSize(9).Italic();
                    text.Span(ConvertirMontantEnLettres(devis.MontantTTC)).FontSize(9).Bold().Italic();
                });
            });
        }

        void ComposeConditions(IContainer container, Devis devis)
        {
            container.PaddingTop(10).Column(column =>
            {
                column.Item().Text("CONDITIONS").FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
                column.Item().PaddingTop(5).Text(devis.Conditions).FontSize(9).LineHeight(1.3f);
            });
        }

        void ComposeNotes(IContainer container, Devis devis)
        {
            container.PaddingTop(10).Background(Colors.Yellow.Lighten3).Padding(10).Column(column =>
            {
                column.Item().Text("NOTES INTERNES").FontSize(10).Bold().FontColor(Colors.Red.Darken1);
                column.Item().PaddingTop(5).Text(devis.Notes).FontSize(9);
            });
        }

        void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Column(column =>
            {
                column.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(10);
                column.Item().Text("SAF ALU-CI - Votre partenaire de confiance en aluminium et menuiserie")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                column.Item().Text("Ce devis est valable pour la durée mentionnée ci-dessus")
                    .FontSize(7).FontColor(Colors.Grey.Medium).Italic();
            });
        }

        string ConvertirMontantEnLettres(decimal montant)
        {
            // Implémentation simplifiée
            // Pour une implémentation complète, utilisez une bibliothèque comme Humanizer
            var montantEntier = (long)Math.Floor(montant);

            if (montantEntier == 0) return "Zéro franc CFA";

            // Version simplifiée - ajoutez une logique complète si nécessaire
            return $"{montantEntier:N0} francs CFA";
        }
    }
}