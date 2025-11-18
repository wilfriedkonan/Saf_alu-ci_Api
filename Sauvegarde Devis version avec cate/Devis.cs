using Saf_alu_ci_Api.Controllers.Clients;

namespace Saf_alu_ci_Api.Controllers.Devis
{
    public class Devis
    {
        public int Id { get; set; }
        public string Numero { get; set; }
        public int ClientId { get; set; }
        public string Titre { get; set; }
        public string? Description { get; set; }
        public string Statut { get; set; } = "Brouillon"; // Brouillon, Envoye, EnNegociation, Valide, Refuse, Expire
        public decimal MontantHT { get; set; }
        public decimal TauxTVA { get; set; } = 20.00m;
        public decimal MontantTTC { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime? DateValidite { get; set; }
        public DateTime? DateEnvoi { get; set; }
        public DateTime? DateValidation { get; set; }
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public string? Conditions { get; set; }
        public string? Notes { get; set; }
        public string? CheminPDF { get; set; }
        public int UtilisateurCreation { get; set; }
        public int? UtilisateurValidation { get; set; }

        // Navigation properties
        public virtual Client? Client { get; set; }
        public virtual List<CategorieDevis>? categories { get; set; }
    }
    public class CategorieDevis
    {
        public int Id { get; set; }
        public bool Actif { get; set; } = true;
        public int Ordre { get; set; }
        public string? Designation { get; set; }
        public int DevisId { get; set; }
        public virtual LigneDevis? LigneDevis { get; set; }
    }
    public class LigneDevis
    {
        public int Id { get; set; }
        public int DevisId { get; set; }
        public int? CategorieDevisId { get; set; }
        public int Ordre { get; set; }
        public string Designation { get; set; }
        public string? Description { get; set; }
        public decimal Quantite { get; set; } = 1;
        public float? L { get; set; }
        public float? H { get; set; }
        public string? Unite { get; set; } = "U";
        public decimal PrixUnitaireHT { get; set; }
        public decimal TotalHT => Quantite * PrixUnitaireHT;
    }

    public class CreateDevisRequest
    {
        public int DevisId { get; set; }
        public int ClientId { get; set; }
        public string Titre { get; set; }
        public string? Description { get; set; }
        public string? Conditions { get; set; }
        public string? Notes { get; set; }
        public decimal MontantHT { get; set; }
        public decimal TauxTVA { get; set; } = 20.00m;
        public decimal MontantTTC { get; set; }


        public List<CreateCategorieDevisRequest>? Categorie { get; set; }
    }
    public class CreateCategorieDevisRequest
    {
        public string Designation { get; set; }
        public decimal Actif { get; set; } = 1;
        public int Ordre { get; set; }

        public List<CreateLigneDevisRequest>? Lignes { get; set; }
    }
    public class CreateLigneDevisRequest
    {
        public string Designation { get; set; }
        public string? Description { get; set; }
        public decimal Quantite { get; set; } = 1;
        public int Ordre { get; set; }
        public float? L { get; set; }
        public float? H { get; set; }
        public string? Unite { get; set; } = "U";
        public decimal PrixUnitaireHT { get; set; }
        public decimal TotalHT => Quantite * PrixUnitaireHT;

    }
}

