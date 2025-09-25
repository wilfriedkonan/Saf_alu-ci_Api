using System.ComponentModel.DataAnnotations;

namespace Saf_alu_ci_Api.Controllers.Tresorerie
{
    public class Compte
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nom { get; set; }

        [Required]
        [StringLength(20)]
        public string TypeCompte { get; set; } // Courant, Epargne, Caisse

        [StringLength(50)]
        public string? Numero { get; set; }

        [StringLength(100)]
        public string? Banque { get; set; }

        public decimal SoldeInitial { get; set; }
        public decimal SoldeActuel { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public bool Actif { get; set; } = true;

        // Navigation properties (optionnelles)
        public virtual List<MouvementFinancier>? Mouvements { get; set; }
        public virtual List<MouvementFinancier>? MouvementsDestination { get; set; }
    }
    public class MouvementFinancier
    {
        public int Id { get; set; }
        public int CompteId { get; set; }

        [Required]
        [StringLength(20)]
        public string TypeMouvement { get; set; } // Entree, Sortie, Virement

        [StringLength(50)]
        public string? Categorie { get; set; }

        // Relations optionnelles vers autres entités
        public int? FactureId { get; set; }
        public int? ProjetId { get; set; }
        public int? SousTraitantId { get; set; }

        [Required]
        [StringLength(200)]
        public string Libelle { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public decimal Montant { get; set; }

        public DateTime DateMouvement { get; set; }
        public DateTime DateSaisie { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? ModePaiement { get; set; }

        [StringLength(100)]
        public string? Reference { get; set; }

        // Pour les virements
        public int? CompteDestinationId { get; set; }

        public int UtilisateurSaisie { get; set; }

        // Navigation properties (optionnelles)
        public virtual Compte? Compte { get; set; }
        public virtual Compte? CompteDestination { get; set; }
        // public virtual Facture? Facture { get; set; }
        // public virtual Projet? Projet { get; set; }
        // public virtual SousTraitant? SousTraitant { get; set; }
        // public virtual Utilisateur? UtilisateurSaisieProp { get; set; }
    }

    // =============================================
    // DTOs POUR CRÉATION
    // =============================================
    public class CreateCompteRequest
    {
        [Required(ErrorMessage = "Le nom du compte est obligatoire")]
        [StringLength(100, ErrorMessage = "Le nom ne peut pas dépasser 100 caractères")]
        public string Nom { get; set; }

        [Required(ErrorMessage = "Le type de compte est obligatoire")]
        public string TypeCompte { get; set; } // Courant, Epargne, Caisse

        [StringLength(50, ErrorMessage = "Le numéro ne peut pas dépasser 50 caractères")]
        public string? Numero { get; set; }

        [StringLength(100, ErrorMessage = "Le nom de la banque ne peut pas dépasser 100 caractères")]
        public string? Banque { get; set; }

        [Required(ErrorMessage = "Le solde initial est obligatoire")]
        [Range(0, double.MaxValue, ErrorMessage = "Le solde initial doit être positif ou nul")]
        public decimal SoldeInitial { get; set; }
    }

    public class UpdateCompteRequest
    {
        [Required(ErrorMessage = "Le nom du compte est obligatoire")]
        [StringLength(100, ErrorMessage = "Le nom ne peut pas dépasser 100 caractères")]
        public string Nom { get; set; }

        [StringLength(50, ErrorMessage = "Le numéro ne peut pas dépasser 50 caractères")]
        public string? Numero { get; set; }

        [StringLength(100, ErrorMessage = "Le nom de la banque ne peut pas dépasser 100 caractères")]
        public string? Banque { get; set; }
    }

    public class CreateMouvementRequest
    {
        [Required(ErrorMessage = "Le compte est obligatoire")]
        public int CompteId { get; set; }

        [Required(ErrorMessage = "Le type de mouvement est obligatoire")]
        public string TypeMouvement { get; set; } // Entree, Sortie, Virement

        [StringLength(50)]
        public string? Categorie { get; set; }

        // Relations optionnelles
        public int? FactureId { get; set; }
        public int? ProjetId { get; set; }
        public int? SousTraitantId { get; set; }

        [Required(ErrorMessage = "Le libellé est obligatoire")]
        [StringLength(200, ErrorMessage = "Le libellé ne peut pas dépasser 200 caractères")]
        public string Libelle { get; set; }

        [StringLength(500, ErrorMessage = "La description ne peut pas dépasser 500 caractères")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Le montant est obligatoire")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Le montant doit être supérieur à 0")]
        public decimal Montant { get; set; }

        public DateTime DateMouvement { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? ModePaiement { get; set; }

        [StringLength(100)]
        public string? Reference { get; set; }

        // Pour les virements uniquement
        public int? CompteDestinationId { get; set; }
    }

    public class VirementRequest
    {
        [Required(ErrorMessage = "Le compte source est obligatoire")]
        public int CompteSourceId { get; set; }

        [Required(ErrorMessage = "Le compte de destination est obligatoire")]
        public int CompteDestinationId { get; set; }

        [Required(ErrorMessage = "Le montant est obligatoire")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Le montant doit être supérieur à 0")]
        public decimal Montant { get; set; }

        [Required(ErrorMessage = "Le libellé est obligatoire")]
        [StringLength(200, ErrorMessage = "Le libellé ne peut pas dépasser 200 caractères")]
        public string Libelle { get; set; }

        [StringLength(500, ErrorMessage = "La description ne peut pas dépasser 500 caractères")]
        public string? Description { get; set; }

        public DateTime DateMouvement { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? Reference { get; set; }
    }

    public class CorrectionSoldeRequest
    {
        [Required(ErrorMessage = "Le nouveau solde est obligatoire")]
        public decimal NouveauSolde { get; set; }

        [Required(ErrorMessage = "Le motif de correction est obligatoire")]
        [StringLength(500, ErrorMessage = "Le motif ne peut pas dépasser 500 caractères")]
        public string MotifCorrection { get; set; }

        [StringLength(100)]
        public string? Reference { get; set; }
    }

    // =============================================
    // DTOs POUR STATISTIQUES ET REPORTING
    // =============================================
    public class TresorerieStats
    {
        public decimal SoldeTotal { get; set; }
        public decimal EntreesMois { get; set; }
        public decimal SortiesMois { get; set; }
        public decimal BeneficeMois { get; set; }
        public decimal EntreesAnnee { get; set; }
        public decimal SortiesAnnee { get; set; }
        public decimal BeneficeAnnee { get; set; }

        public List<ChartData> FluxMensuels { get; set; } = new();
        public List<ChartData> BeneficesParProjet { get; set; } = new();
        public List<ChartData> RepartitionParCategorie { get; set; } = new();
        public List<ChartData> EvolutionSoldes { get; set; } = new();

        public TresorerieIndicateurs Indicateurs { get; set; } = new();
    }

    public class TresorerieIndicateurs
    {
        public decimal TauxCroissanceMensuel { get; set; }
        public decimal MoyenneMouvementsParJour { get; set; }
        public decimal PlusGrosseEntree { get; set; }
        public decimal PlusGrosseSortie { get; set; }
        public string? CompteLesPlusUtilise { get; set; }
        public int NombreMouvementsMois { get; set; }
    }

    public class ChartData
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string? Color { get; set; }
        public Dictionary<string, object>? MetaDonnees { get; set; }
    }

    public class RapportTresorerie
    {
        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }
        public List<Compte> ComptesAnalyses { get; set; } = new();
        public List<MouvementFinancier> Mouvements { get; set; } = new();
        public TresorerieStats Statistiques { get; set; } = new();
        public List<AlerteTresorerie> Alertes { get; set; } = new();
    }

    public class AlerteTresorerie
    {
        public string Type { get; set; } = string.Empty; // SoldeFaible, MouvementImportant, etc.
        public string Message { get; set; } = string.Empty;
        public string Niveau { get; set; } = string.Empty; // Info, Attention, Critique
        public DateTime DateDetection { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object>? Contexte { get; set; }
    }

    // =============================================
    // ENUMS ET CONSTANTES
    // =============================================
    public static class TypesCompte
    {
        public const string Courant = "Courant";
        public const string Epargne = "Epargne";
        public const string Caisse = "Caisse";

        public static List<string> GetAll() => new() { Courant, Epargne, Caisse };
    }

    public static class TypesMouvement
    {
        public const string Entree = "Entree";
        public const string Sortie = "Sortie";
        public const string Virement = "Virement";

        public static List<string> GetAll() => new() { Entree, Sortie, Virement };
    }

    public static class CategoriesMouvement
    {
        public const string FactureClient = "Facture client";
        public const string PaiementSousTraitant = "Paiement sous-traitant";
        public const string ChargesSociales = "Charges sociales";
        public const string Assurances = "Assurances";
        public const string Location = "Location";
        public const string Fournitures = "Fournitures";
        public const string Carburant = "Carburant";
        public const string Maintenance = "Maintenance";
        public const string Banque = "Frais bancaires";
        public const string Autre = "Autre";

        public static List<string> GetAll() => new()
        {
            FactureClient, PaiementSousTraitant, ChargesSociales, Assurances,
            Location, Fournitures, Carburant, Maintenance, Banque, Autre
        };
    }
}