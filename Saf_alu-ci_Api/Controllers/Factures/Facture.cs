using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.SousTraitants;

namespace Saf_alu_ci_Api.Controllers.Factures
{
    public class Facture
    {
        public int Id { get; set; }
        public string Numero { get; set; }
        public string TypeFacture { get; set; } = "Devis"; // Devis, SousTraitant, Avoir, Acompte
        public int? ClientId { get; set; }
        public int? SousTraitantId { get; set; }
        public int? DevisId { get; set; }
        public int? ProjetId { get; set; }
        public string Titre { get; set; }
        public string? Description { get; set; }
        public string Statut { get; set; } = "Brouillon"; // Brouillon, Envoyee, Payee, EnRetard, Annulee
        public decimal MontantHT { get; set; }
        public decimal TauxTVA { get; set; } = 20.00m;
        public decimal MontantTVA { get; set; }
        public decimal MontantTTC { get; set; }
        public decimal MontantPaye { get; set; } = 0;
        public decimal MontantRestant => MontantTTC - MontantPaye;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateFacture { get; set; }
        public DateTime DateEcheance { get; set; }
        public DateTime? DateEnvoi { get; set; }
        public DateTime? DatePaiement { get; set; }
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public string ConditionsPaiement { get; set; } = "30 jours";
        public string? ModePaiement { get; set; }
        public string? ReferenceClient { get; set; }
        public string? CheminPDF { get; set; }
        public int UtilisateurCreation { get; set; }

        // Navigation properties
        public virtual Client? Client { get; set; }
        public virtual SousTraitant? SousTraitant { get; set; }
        public virtual List<LigneFacture>? Lignes { get; set; }
        public virtual List<Echeancier>? Echeanciers { get; set; }
    }

    public class LigneFacture
    {
        public int Id { get; set; }
        public int FactureId { get; set; }
        public int Ordre { get; set; }
        public string Designation { get; set; }
        public string? Description { get; set; }
        public decimal Quantite { get; set; } = 1;
        public string Unite { get; set; } = "U";
        public decimal PrixUnitaireHT { get; set; }
        public decimal TotalHT => Quantite * PrixUnitaireHT;
    }

    public class Echeancier
    {
        public int Id { get; set; }
        public int FactureId { get; set; }
        public int Ordre { get; set; }
        public string? Description { get; set; }
        public decimal MontantTTC { get; set; }
        public DateTime DateEcheance { get; set; }
        public string Statut { get; set; } = "EnAttente"; // EnAttente, Paye, EnRetard
        public DateTime? DatePaiement { get; set; }
        public string? ModePaiement { get; set; }
        public string? ReferencePaiement { get; set; }
    }

    public class CreateFactureRequest
    {
        public string TypeFacture { get; set; } = "Devis";
        public int? ClientId { get; set; }
        public int? SousTraitantId { get; set; }
        public int? DevisId { get; set; }
        public int? ProjetId { get; set; }
        public string Titre { get; set; }
        public string? Description { get; set; }
        public DateTime DateFacture { get; set; }
        public DateTime DateEcheance { get; set; }
        public string? ConditionsPaiement { get; set; }
        public string? ReferenceClient { get; set; }
        public List<CreateLigneFactureRequest>? Lignes { get; set; }
        public List<CreateEcheancierRequest>? Echeanciers { get; set; }
    }

    public class CreateLigneFactureRequest
    {
        public string Designation { get; set; }
        public string? Description { get; set; }
        public decimal Quantite { get; set; } = 1;
        public string Unite { get; set; } = "U";
        public decimal PrixUnitaireHT { get; set; }
    }

    public class CreateEcheancierRequest
    {
        public string? Description { get; set; }
        public decimal MontantTTC { get; set; }
        public DateTime DateEcheance { get; set; }
    }

    public class MarquerPayeRequest
    {
        public decimal MontantPaye { get; set; }
        public string? ModePaiement { get; set; }
        public string? ReferencePaiement { get; set; }
        public DateTime DatePaiement { get; set; } = DateTime.UtcNow;
    }
}