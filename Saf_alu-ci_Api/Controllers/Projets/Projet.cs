using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.SousTraitants;
using Saf_alu_ci_Api.Controllers.Tresorerie;
using Saf_alu_ci_Api.Controllers.Utilisateurs;
using System.ComponentModel.DataAnnotations;

namespace Saf_alu_ci_Api.Controllers.Projets
{
    public class Projet
    {
        public int Id { get; set; }
        public string Numero { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int ClientId { get; set; }
        public int? TypeProjetId { get; set; }
        public int? DevisId { get; set; }
        public string Statut { get; set; } = "Planification"; // Planification, EnCours, Suspendu, Termine, Annule
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinRelle { get; set; }
        public decimal BudgetInitial { get; set; }
        public decimal BudgetRevise { get; set; }
        public decimal CoutReel { get; set; }
        public decimal DepenseGlobale { get; set; } = 0; // Somme des dépenses de toutes les étapes
        public string? AdresseChantier { get; set; }
        public string? CodePostalChantier { get; set; }
        public string? VilleChantier { get; set; }
        public int PourcentageAvancement { get; set; } = 0;
        public int? ChefProjetId { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public int UtilisateurCreation { get; set; }
        public bool Actif { get; set; } = true;

        // ========================================
        // NOUVELLES COLONNES - LIEN DQE
        // ========================================

        // Propriétés de lien DQE
        public bool IsFromDqeConversion { get; set; }
        public int? LinkedDqeId { get; set; }
        public string? LinkedDqeReference { get; set; }
        public string? LinkedDqeName { get; set; }
        public decimal? LinkedDqeBudgetHT { get; set; }
        public DateTime? DqeConvertedAt { get; set; }
        public int? DqeConvertedById { get; set; }

        // Navigation properties
        public virtual Client? Client { get; set; }
        public virtual TypeProjet? TypeProjet { get; set; }
        public virtual Utilisateur? ChefProjet { get; set; }
        public virtual Utilisateur? DqeConvertedBy { get; set; }
        public virtual List<EtapeProjet>? Etapes { get; set; }
        public List<MouvementFinancier>? DepenseProjet { get; set; }
    }

    public class TypeProjet
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public string Couleur { get; set; } = "#2563eb";
        public bool Actif { get; set; } = true;
    }

    public class EtapeProjet
    {
        public int Id { get; set; }
        public int ProjetId { get; set; }

        [Required]
        [StringLength(200)]
        public string Nom { get; set; } = string.Empty;

        public string? Description { get; set; }
        public int Ordre { get; set; }

        // ============================================
        // 🆕 HIÉRARCHIE : Support des sous-étapes
        // ============================================

        /// <summary>
        /// ID de l'étape parent (NULL si étape principale)
        /// </summary>
        public int? EtapeParentId { get; set; }

        /// <summary>
        /// Niveau hiérarchique : 1 = Étape principale (Lot), 2 = Sous-étape (Item)
        /// </summary>
        public int Niveau { get; set; } = 1;

        /// <summary>
        /// Type d'étape : "Lot" (depuis DQE Lot) ou "Item" (depuis DQE Item)
        /// </summary>
        [StringLength(20)]
        public string TypeEtape { get; set; } = "Lot"; // "Lot" ou "Item"

        // ============================================
        // DATES
        // ============================================

        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }

        // ============================================
        // STATUT ET AVANCEMENT
        // ============================================

        [Required]
        [StringLength(20)]
        public string Statut { get; set; } = "NonCommence"; // NonCommence, EnCours, Termine, Suspendu

        public int PourcentageAvancement { get; set; } = 0;

        // ============================================
        // BUDGET ET COÛTS
        // ============================================

        public decimal BudgetPrevu { get; set; }
        public decimal CoutReel { get; set; }
        public decimal Depense { get; set; } = 0;

        // ============================================
        // 🆕 QUANTITÉS (pour les sous-étapes/Items)
        // ============================================

        /// <summary>
        /// Unité de mesure (pour les étapes de type "Item")
        /// </summary>
        [StringLength(20)]
        public string? Unite { get; set; }

        /// <summary>
        /// Quantité prévue (pour les étapes de type "Item")
        /// </summary>
        public decimal? QuantitePrevue { get; set; }

        /// <summary>
        /// Quantité réalisée (pour les étapes de type "Item")
        /// </summary>
        public decimal? QuantiteRealisee { get; set; }

        /// <summary>
        /// Prix unitaire prévu (pour les étapes de type "Item")
        /// </summary>
        public decimal? PrixUnitairePrevu { get; set; }

        // ============================================
        // RESPONSABLE
        // ============================================

        public int? ResponsableId { get; set; }

        [Required]
        [StringLength(20)]
        public string TypeResponsable { get; set; } = "Interne"; // Interne, SousTraitant

        public int? IdSousTraitant { get; set; }

        // ============================================
        // 🔗 TRAÇABILITÉ DQE
        // ============================================

        // Lien vers Lot DQE (pour étapes principales)
        public int? LinkedDqeLotId { get; set; }
        public string? LinkedDqeLotCode { get; set; }
        public string? LinkedDqeLotName { get; set; }

        // 🆕 Lien vers Item DQE (pour sous-étapes)
        public int? LinkedDqeItemId { get; set; }
        public string? LinkedDqeItemCode { get; set; }

        // 🆕 Lien vers Chapter DQE (pour sous-étapes)
        public int? LinkedDqeChapterId { get; set; }
        public string? LinkedDqeChapterCode { get; set; }

        // Référence DQE commune
        public string? LinkedDqeReference { get; set; }

        // ============================================
        // MÉTADONNÉES
        // ============================================

        public bool EstActif { get; set; } = true;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;

        // ============================================
        // NAVIGATION PROPERTIES
        // ============================================

        public virtual Projet? Projet { get; set; }
        public virtual EtapeProjet? EtapeParent { get; set; }
        public virtual List<EtapeProjet>? SousEtapes { get; set; }
        public virtual SousTraitant? SousTraitant { get; set; }
        public virtual Utilisateur? Responsable { get; set; }
        public List<MouvementFinancier>? DepenseProjet { get; set; }


    }

    /// <summary>
    /// DTO pour afficher une étape avec ses sous-étapes
    /// </summary>
    public class EtapeProjetDTO
    {
        public int Id { get; set; }
        public int ProjetId { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Ordre { get; set; }

        // Hiérarchie
        public int? EtapeParentId { get; set; }
        public int Niveau { get; set; }
        public string TypeEtape { get; set; } = "Lot";

        // Dates
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }

        // Statut
        public string Statut { get; set; } = "NonCommence";
        public int PourcentageAvancement { get; set; }

        // Budget
        public decimal BudgetPrevu { get; set; }
        public decimal CoutReel { get; set; }
        public decimal Depense { get; set; }

        // Écarts calculés
        public decimal EcartBudget => CoutReel - BudgetPrevu;
        public decimal EcartBudgetPourcentage => BudgetPrevu > 0 ?
            ((CoutReel - BudgetPrevu) / BudgetPrevu) * 100 : 0;

        // Quantités (pour Items)
        public string? Unite { get; set; }
        public decimal? QuantitePrevue { get; set; }
        public decimal? QuantiteRealisee { get; set; }
        public decimal? PrixUnitairePrevu { get; set; }
        public decimal? EcartQuantite => QuantiteRealisee.HasValue && QuantitePrevue.HasValue
            ? QuantiteRealisee - QuantitePrevue
            : null;

        // Responsable
        public int? ResponsableId { get; set; }
        public string TypeResponsable { get; set; } = "Interne";
        public int? IdSousTraitant { get; set; }
        public ResponsableDTO? Responsable { get; set; }
        public SousTraitantDTO? SousTraitant { get; set; }

        // Traçabilité DQE
        public int? LinkedDqeLotId { get; set; }
        public string? LinkedDqeLotCode { get; set; }
        public string? LinkedDqeLotName { get; set; }
        public int? LinkedDqeItemId { get; set; }
        public string? LinkedDqeItemCode { get; set; }
        public int? LinkedDqeChapterId { get; set; }
        public string? LinkedDqeChapterCode { get; set; }
        public string? LinkedDqeReference { get; set; }

        // 🆕 Sous-étapes (si étape principale)
        public List<EtapeProjetDTO>? SousEtapes { get; set; }

        // 🆕 Statistiques des sous-étapes (si étape principale)
        public StatistiquesSousEtapesDTO? StatistiquesSousEtapes { get; set; }
    }
    public class StatistiquesSousEtapesDTO
    {
        public int NombreTotal { get; set; }
        public int NombreNonCommencees { get; set; }
        public int NombreEnCours { get; set; }
        public int NombreTerminees { get; set; }
        public decimal AvancementMoyen { get; set; }
        public decimal BudgetTotal { get; set; }
        public decimal CoutTotal { get; set; }
        public decimal EcartBudgetTotal { get; set; }
        public decimal EcartBudgetPourcentage { get; set; }
    }
    /// <summary>
    /// DTO pour créer/modifier une étape
    /// </summary>
    public class CreateEtapeProjetRequest
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Le nom est requis")]
        [StringLength(200)]
        public string Nom { get; set; } = string.Empty;

        public string? Description { get; set; }

        // Hiérarchie
        public int? EtapeParentId { get; set; }
        public int Niveau { get; set; } = 1;
        public string TypeEtape { get; set; } = "Lot";

        // Dates
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }

        // Budget
        [Required]
        [Range(0, double.MaxValue)]
        public decimal BudgetPrevu { get; set; }

        public decimal? CoutReel { get; set; }

        // Quantités (optionnel, pour Items)
        public string? Unite { get; set; }
        public decimal? QuantitePrevue { get; set; }
        public decimal? PrixUnitairePrevu { get; set; }

        // Responsable
        public int? ResponsableId { get; set; }
        public string TypeResponsable { get; set; } = "Interne";
        public int? IdSousTraitant { get; set; }

        // Traçabilité DQE
        public int? LinkedDqeLotId { get; set; }
        public string? LinkedDqeLotCode { get; set; }
        public string? LinkedDqeLotName { get; set; }
        public int? LinkedDqeItemId { get; set; }
        public string? LinkedDqeItemCode { get; set; }
        public int? LinkedDqeChapterId { get; set; }
        public string? LinkedDqeChapterCode { get; set; }
        public string? LinkedDqeReference { get; set; }

        public string? Statut { get; set; }
        public bool EstActif { get; set; } = true;
    }

    /// <summary>
    /// DTO pour mettre à jour une étape
    /// </summary>
    public class UpdateEtapeProjetRequest
    {
        public int? Id { get; set; }
        public string? Nom { get; set; }
        public string? Description { get; set; }
        public string? Statut { get; set; }
        public int? PourcentageAvancement { get; set; }
        public decimal? BudgetPrevu { get; set; }
        public decimal? CoutReel { get; set; }
        public decimal? Depense { get; set; }
        public decimal? QuantiteRealisee { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }
        public int? ResponsableId { get; set; }
        public int? IdSousTraitant { get; set; }
        public bool? EstActif { get; set; }
        public string? TypeResponsable { get; set; }
    }

    // DTOs pour les entités liées
    public class ResponsableDTO
    {
        public int Id { get; set; }
        public string Prenom { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
    }

    public class SousTraitantDTO
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public decimal NoteMoyenne { get; set; }
    }
    public class CreateProjetRequest
    {
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int ClientId { get; set; }
        public int TypeProjetId { get; set; }
        public int? DevisId { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public decimal BudgetInitial { get; set; }
        public string? AdresseChantier { get; set; }
        public string? CodePostalChantier { get; set; }
        public string? VilleChantier { get; set; }
        public int? ChefProjetId { get; set; }
        public List<CreateEtapeProjetRequest>? Etapes { get; set; }

        // ========================================
        // NOUVELLES PROPRIÉTÉS - CONVERSION DQE
        // ========================================

        /// <summary>
        /// ID du DQE source (utilisé lors de conversion DQE → Projet)
        /// </summary>
        public int? LinkedDqeId { get; set; }

        /// <summary>
        /// Référence du DQE source
        /// </summary>
        public string? LinkedDqeReference { get; set; }

        /// <summary>
        /// Nom du DQE source
        /// </summary>
        public string? LinkedDqeName { get; set; }

        /// <summary>
        /// Budget HT du DQE source
        /// </summary>
        public decimal? LinkedDqeBudgetHT { get; set; }

        /// <summary>
        /// Indique si c'est une conversion DQE
        /// </summary>
        public bool IsFromDqeConversion { get; set; } = false;
    }

    //public class CreateEtapeProjetRequest
    //{
    //    public int? Id { get; set; }  // Pour mise à jour
    //    public string Nom { get; set; }
    //    public string? Description { get; set; }
    //    public DateTime? DateDebut { get; set; }
    //    public DateTime? DateFinPrevue { get; set; }
    //    public decimal BudgetPrevu { get; set; }
    //    public decimal coutReel { get; set; }
    //    public string Statut { get; set; } = "NonCommence";
    //    public bool EstActif { get; set; } = true;

    //    // ✅ NOUVELLE PROPRIÉTÉ
    //    /// <summary>
    //    /// ID du sous-traitant (si TypeResponsable = "SousTraitant")
    //    /// </summary>
    //    public int? IdSousTraitant { get; set; }

    //    // Propriétés DQE existantes
    //    public int? LinkedDqeLotId { get; set; }
    //    public string? LinkedDqeLotCode { get; set; }
    //    public string? LinkedDqeLotName { get; set; }
    //    public string? LinkedDqeReference { get; set; }
    //}
    public class UpdateAvancementRequest
    {
        public int PourcentageAvancement { get; set; }
        public int? Note { get; set; } // Note sur 5 pour évaluation
        public string? Commentaire { get; set; }
        public string? Statut { get; set; }
    }

    public class UpdateDepenseRequest
    {
        /// <summary>
        /// Montant à ajouter ou soustraire (peut être positif ou négatif)
        /// </summary>
        public decimal Montant { get; set; }

        /// <summary>
        /// Type d'opération: "Debit" ou "Credit"
        /// </summary>
        public string TypeOperation { get; set; } // Debit, Credit

        /// <summary>
        /// Référence de la transaction de trésorerie
        /// </summary>
        public string? ReferenceTransaction { get; set; }

        /// <summary>
        /// Description ou motif de la dépense
        /// </summary>
        public string? Description { get; set; }
    }

    public class UpdateProjetRequest
    {
        public string? Nom { get; set; }
        public string? Description { get; set; }
        public int? ClientId { get; set; }
        public int? TypeProjetId { get; set; }
        public int? DevisId { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public decimal? BudgetInitial { get; set; }
        public decimal? BudgetRevise { get; set; }
        public string? Statut { get; set; }
        public string? AdresseChantier { get; set; }
        public string? CodePostalChantier { get; set; }
        public string? VilleChantier { get; set; }
        public int? ChefProjetId { get; set; }
        public int? PourcentageAvancement { get; set; }

        // Étapes (optionnel pour mise à jour complète des étapes)
        public List<UpdateEtapeProjetRequest>? Etapes { get; set; }
    }


    /// <summary>
    /// DTO pour la mise à jour partielle d'une étape de projet
    /// </summary>
    //public class UpdateEtapeProjetRequest
    //{
    //    public int? Id { get; set; }
    //    public string? Nom { get; set; }
    //    public string? Description { get; set; }
    //    public DateTime? DateDebut { get; set; }
    //    public DateTime? DateFinPrevue { get; set; }
    //    public decimal? BudgetPrevu { get; set; }
    //    public decimal? CoutReel { get; set; }
    //    public string? Statut { get; set; }
    //    public int? ResponsableId { get; set; }
    //    public string? TypeResponsable { get; set; }
    //    public bool EstActif { get; set; }

    //    // ✅ NOUVELLE PROPRIÉTÉ
    //    /// <summary>
    //    /// ID du sous-traitant (si TypeResponsable = "SousTraitant")
    //    /// </summary>
    //    public int? IdSousTraitant { get; set; }
    //}

    public class TacheProjet
    {
        public int Id { get; set; }
        public int EtapeProjetId { get; set; }
        public string Code { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }

        // Quantités et unités
        public string Unite { get; set; } // m³, ml, m², ens, forf, u, kg
        public decimal QuantitePrevue { get; set; }
        public decimal QuantiteRealisee { get; set; }

        // Budget et coûts
        public decimal PrixUnitairePrevu { get; set; }
        public decimal? PrixUnitaireReel { get; set; }
        public decimal BudgetPrevu { get; set; }
        public decimal CoutReel { get; set; }

        // Statut et avancement
        public string Statut { get; set; } // NonCommence, EnCours, Termine
        public int PourcentageAvancement { get; set; }

        // Dates
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }

        // Responsable
        public int? ResponsableId { get; set; }
        public string TypeResponsable { get; set; } // Interne, SousTraitant

        // 🔗 Liens vers DQE Item source (traçabilité complète)
        public int? LinkedDqeItemId { get; set; }
        public string? LinkedDqeItemCode { get; set; }
        public int? LinkedDqeChapterId { get; set; }
        public string? LinkedDqeChapterCode { get; set; }
        public int? LinkedDqeLotId { get; set; }
        public string? LinkedDqeLotCode { get; set; }
        public string? LinkedDqeReference { get; set; }

        // Métadonnées
        public DateTime DateCreation { get; set; }
        public DateTime DateModification { get; set; }
        public bool Actif { get; set; }
    }

    /// <summary>
    /// DTO pour créer une tâche
    /// </summary>
    public class CreateTacheProjetRequest
    {
        public string Code { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public string Unite { get; set; }
        public decimal QuantitePrevue { get; set; }
        public decimal PrixUnitairePrevu { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }

        // Responsable
        public int? ResponsableId { get; set; }
        public string TypeResponsable { get; set; } = "Interne";

        // Lien DQE (optionnel si création manuelle)
        public int? LinkedDqeItemId { get; set; }
        public string? LinkedDqeItemCode { get; set; }
        public int? LinkedDqeChapterId { get; set; }
        public string? LinkedDqeChapterCode { get; set; }
        public int? LinkedDqeLotId { get; set; }
        public string? LinkedDqeLotCode { get; set; }
        public string? LinkedDqeReference { get; set; }
    }

    /// <summary>
    /// DTO pour mettre à jour une tâche
    /// </summary>
    public class UpdateTacheProjetRequest
    {
        public string? Nom { get; set; }
        public string? Description { get; set; }
        public string? Unite { get; set; }
        public decimal? QuantitePrevue { get; set; }
        public decimal? QuantiteRealisee { get; set; }
        public decimal? PrixUnitairePrevu { get; set; }
        public decimal? PrixUnitaireReel { get; set; }
        public decimal? CoutReel { get; set; }
        public string? Statut { get; set; }
        public int? PourcentageAvancement { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }
        public int? ResponsableId { get; set; }
    }

    /// <summary>
    /// DTO pour mettre à jour l'avancement d'une tâche
    /// </summary>
    public class UpdateAvancementTacheRequest
    {
        public int PourcentageAvancement { get; set; }
        public decimal? QuantiteRealisee { get; set; }
        public decimal? CoutReel { get; set; }
        public string? Commentaire { get; set; }
    }

    /// <summary>
    /// DTO de réponse avec détails de la tâche
    /// </summary>
    public class TacheProjetDetailDTO
    {
        // Informations de base
        public int Id { get; set; }
        public int EtapeProjetId { get; set; }
        public string Code { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }

        // Quantités
        public string Unite { get; set; }
        public decimal QuantitePrevue { get; set; }
        public decimal QuantiteRealisee { get; set; }

        // Budget
        public decimal PrixUnitairePrevu { get; set; }
        public decimal? PrixUnitaireReel { get; set; }
        public decimal BudgetPrevu { get; set; }
        public decimal CoutReel { get; set; }

        // Écarts
        public decimal EcartBudget { get; set; }
        public decimal EcartBudgetPourcentage { get; set; }
        public decimal EcartQuantite { get; set; }

        // Statut
        public string Statut { get; set; }
        public int PourcentageAvancement { get; set; }

        // Dates
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }

        // Responsable
        public int? ResponsableId { get; set; }
        public string TypeResponsable { get; set; }
        public string? ResponsableNom { get; set; }

        // Liens DQE
        public int? LinkedDqeItemId { get; set; }
        public string? LinkedDqeItemCode { get; set; }
        public int? LinkedDqeChapterId { get; set; }
        public string? LinkedDqeChapterCode { get; set; }
        public int? LinkedDqeLotId { get; set; }
        public string? LinkedDqeLotCode { get; set; }
        public string? LinkedDqeReference { get; set; }

        // Métadonnées
        public DateTime DateCreation { get; set; }
        public DateTime DateModification { get; set; }
    }

    /// <summary>
    /// DTO pour liste de tâches (léger)
    /// </summary>
    public class TacheProjetListItemDTO
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Nom { get; set; }
        public string Unite { get; set; }
        public decimal QuantitePrevue { get; set; }
        public decimal QuantiteRealisee { get; set; }
        public decimal BudgetPrevu { get; set; }
        public decimal CoutReel { get; set; }
        public string Statut { get; set; }
        public int PourcentageAvancement { get; set; }
        public string? ResponsableNom { get; set; }
        public string? LinkedDqeItemCode { get; set; }
    }

    /// <summary>
    /// Statistiques des tâches d'une étape
    /// </summary>
    public class TachesStatistiquesDTO
    {
        public int NombreTachesTotal { get; set; }
        public int NombreTachesNonCommencees { get; set; }
        public int NombreTachesEnCours { get; set; }
        public int NombreTachesTerminees { get; set; }
        public decimal AvancementMoyen { get; set; }
        public decimal BudgetTotal { get; set; }
        public decimal CoutTotal { get; set; }
        public decimal EcartBudgetTotal { get; set; }
        public decimal EcartBudgetPourcentage { get; set; }
    }
}