using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.Projets;
using Saf_alu_ci_Api.Controllers.Utilisateurs;

namespace Saf_alu_ci_Api.Controllers.Dqe
{
    /// <summary>
    /// DQE = Décomposition Quantitative Estimative
    /// Document détaillé listant tous les travaux d'un projet avec quantités et prix
    /// </summary>
    public class DQE
    {
        public int Id { get; set; }

        /// <summary>
        /// Référence unique auto-générée (ex: DQE-2024-023)
        /// </summary>
        public string Reference { get; set; }

        public string Nom { get; set; }
        public string? Description { get; set; }

        /// <summary>
        /// ID du client pour qui le DQE est établi
        /// </summary>
        public int ClientId { get; set; }

        /// <summary>
        /// ID du devis associé (optionnel)
        /// </summary>
        public int? DevisId { get; set; }

        /// <summary>
        /// Statut du DQE : brouillon, en_cours, validé, refusé, archivé
        /// </summary>
        public string Statut { get; set; } = "brouillon";

        /// <summary>
        /// Total HT de tous les lots (calculé automatiquement)
        /// </summary>
        public decimal TotalRevenueHT { get; set; }

        /// <summary>
        /// Taux de TVA appliqué (défaut 18% en Côte d'Ivoire)
        /// </summary>
        public decimal TauxTVA { get; set; } = 18;

        /// <summary>
        /// Montant de la TVA = TotalRevenueHT × (TauxTVA / 100)
        /// </summary>
        public decimal MontantTVA { get; set; }

        /// <summary>
        /// Total TTC = TotalRevenueHT + MontantTVA
        /// </summary>
        public decimal TotalTTC { get; set; }

        /// <summary>
        /// Date à laquelle le DQE a été validé
        /// </summary>
        public DateTime? DateValidation { get; set; }

        /// <summary>
        /// Nom de la personne qui a validé le DQE
        /// </summary>
        public int? ValidePar { get; set; }

        /// <summary>
        /// Indique si le DQE a été converti en projet
        /// </summary>
        public bool IsConverted { get; set; } = false;

        /// <summary>
        /// ID du projet créé depuis ce DQE
        /// </summary>
        public int? LinkedProjectId { get; set; }

        /// <summary>
        /// Numéro du projet créé depuis ce DQE (ex: PRJ20240046)
        /// </summary>
        public string? LinkedProjectNumber { get; set; }

        /// <summary>
        /// Date de conversion du DQE en projet
        /// </summary>
        public DateTime? ConvertedAt { get; set; }

        /// <summary>
        /// ID de l'utilisateur qui a effectué la conversion
        /// </summary>
        public int? ConvertedById { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public int UtilisateurCreation { get; set; }
        public bool Actif { get; set; } = true;

        // Navigation properties
        public virtual Client? Client { get; set; }
        public virtual Utilisateur? ConvertedBy { get; set; }
        public virtual Projet? LinkedProject { get; set; }
        public virtual List<DQELot>? Lots { get; set; }
    }

    /// <summary>
    /// Lot = Regroupement de travaux par corps d'état (ex: LOT 1 - TERRASSEMENTS)
    /// </summary>
    public class DQELot
    {
        public int Id { get; set; }
        public int DqeId { get; set; }

        /// <summary>
        /// Code du lot (ex: LOT 1, LOT 2)
        /// </summary>
        public string Code { get; set; }

        public string Nom { get; set; }
        public string? Description { get; set; }

        /// <summary>
        /// Ordre d'affichage du lot
        /// </summary>
        public int Ordre { get; set; }

        /// <summary>
        /// Total HT du lot (somme des chapitres, calculé automatiquement)
        /// </summary>
        public decimal TotalRevenueHT { get; set; }

        /// <summary>
        /// Pourcentage du lot par rapport au total DQE
        /// </summary>
        public decimal PourcentageTotal { get; set; }

        // Navigation properties
        public virtual DQE? DQE { get; set; }
        public virtual List<DQEChapter>? Chapters { get; set; }
    }

    /// <summary>
    /// Chapitre = Subdivision d'un lot (ex: CHAPITRE 1.1 - Déblais)
    /// </summary>
    public class DQEChapter
    {
        public int Id { get; set; }
        public int LotId { get; set; }

        /// <summary>
        /// Code du chapitre (ex: 1.1, 1.2)
        /// </summary>
        public string Code { get; set; }

        public string Nom { get; set; }
        public string? Description { get; set; }

        /// <summary>
        /// Ordre d'affichage du chapitre dans le lot
        /// </summary>
        public int Ordre { get; set; }

        /// <summary>
        /// Total HT du chapitre (somme des postes, calculé automatiquement)
        /// </summary>
        public decimal TotalRevenueHT { get; set; }

        // Navigation properties
        public virtual DQELot? Lot { get; set; }
        public virtual List<DQEItem>? Items { get; set; }
    }

    /// <summary>
    /// Poste (Item) = Ligne de détail avec quantité et prix (ex: POSTE 1.1.1 - Déblai manuel)
    /// </summary>
    public class DQEItem
    {
        public int Id { get; set; }
        public int ChapterId { get; set; }

        /// <summary>
        /// Code du poste (ex: 1.1.1, 1.1.2)
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Désignation du poste (description du travail)
        /// </summary>
        public string Designation { get; set; }

        public string? Description { get; set; }

        /// <summary>
        /// Ordre d'affichage du poste dans le chapitre
        /// </summary>
        public int Ordre { get; set; }

        /// <summary>
        /// Unité de mesure : m³, ml, m², ens, forf, u
        /// </summary>
        public string Unite { get; set; }

        /// <summary>
        /// Quantité de l'unité
        /// </summary>
        public decimal Quantite { get; set; }

        /// <summary>
        /// Prix unitaire HT
        /// </summary>
        public decimal PrixUnitaireHT { get; set; }

        /// <summary>
        /// Total HT = Quantite × PrixUnitaireHT (calculé automatiquement par trigger)
        /// </summary>
        public decimal TotalRevenueHT { get; set; }

        // Navigation properties
        public virtual DQEChapter? Chapter { get; set; }
    }

    /// <summary>
    /// Template DQE réutilisable
    /// </summary>
    public class DQETemplate
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }

        /// <summary>
        /// Type de projet pour lequel ce template est conçu
        /// </summary>
        public string? TypeProjet { get; set; }

        /// <summary>
        /// Structure DQE au format JSON
        /// </summary>
        public string JsonStructure { get; set; }

        /// <summary>
        /// Indique si le template est public (accessible à tous)
        /// </summary>
        public bool EstPublic { get; set; } = false;

        public int UtilisateurCreation { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public bool Actif { get; set; } = true;
    }

    // ========================================
    // DTOs - REQUÊTES
    // ========================================

    /// <summary>
    /// DTO pour créer un nouveau DQE
    /// </summary>
    public class CreateDQERequest
    {
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int ClientId { get; set; }
        public int? DevisId { get; set; }
        public decimal TauxTVA { get; set; } = 18;
        public List<CreateDQELotRequest>? Lots { get; set; }
    }

    public class CreateDQELotRequest
    {
        public string Code { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public List<CreateDQEChapterRequest>? Chapters { get; set; }
    }

    public class CreateDQEChapterRequest
    {
        public string Code { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public List<CreateDQEItemRequest>? Items { get; set; }
    }

    public class CreateDQEItemRequest
    {
        public string Code { get; set; }
        public string Designation { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public string Unite { get; set; }
        public decimal Quantite { get; set; }
        public decimal PrixUnitaireHT { get; set; }
    }

    /// <summary>
    /// DTO pour mettre à jour un DQE existant
    /// </summary>
    public class UpdateDQERequest
    {
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int ClientId { get; set; }
        public int? DevisId { get; set; }
        public decimal TauxTVA { get; set; }
        public string Statut { get; set; }

        public List<CreateDQELotRequest>? Lots { get; set; }
    }

    /// <summary>
    /// DTO pour valider un DQE
    /// </summary>
    public class ValidateDQERequest
    {
        public string? Commentaire { get; set; }
    }

    /// <summary>
    /// DTO pour créer un DQE depuis un template
    /// </summary>
    public class CreateDQEFromTemplateRequest
    {
        public int TemplateId { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int ClientId { get; set; }
    }

    /// <summary>
    /// DTO pour la conversion DQE → Projet
    /// </summary>
    public class ConvertDQEToProjectRequest
    {
        // Informations projet
        public string? NomProjet { get; set; } // Si null, utilise le nom du DQE
        public string? DescriptionProjet { get; set; }
        public int TypeProjetId { get; set; }
        public DateTime DateDebut { get; set; }
        public int DureeTotaleJours { get; set; }
        public int? ChefProjetId { get; set; }
        public string? Priorite { get; set; }
        public string StatutInitial { get; set; } = "Planification";

        // Configuration des étapes
        public string ModeCreationEtapes { get; set; } = "automatique"; // automatique, manuel
        public string MethodeCalculDurees { get; set; } = "proportionnelle"; // proportionnelle, egales, personnalisee

        // Options avancées
        public bool AttacherDQE { get; set; } = true;
        public bool AssignerOuvriers { get; set; } = false;
        public bool NotifierChefProjet { get; set; } = false;
        public bool ActiverSuiviBudgetaire { get; set; } = true;
    }

    // ========================================
    // DTOs - RÉPONSES
    // ========================================

    /// <summary>
    /// DTO pour l'affichage en liste
    /// </summary>
    public class DQEListItemDTO
    {
        public int Id { get; set; }
        public string Reference { get; set; }
        public string Nom { get; set; }
        public string Statut { get; set; }
        public decimal TotalRevenueHT { get; set; }
        public int LotsCount { get; set; }
        public DateTime DateCreation { get; set; }

        // Client
        public int ClientId { get; set; }
        public string ClientNom { get; set; }

        // Conversion
        public bool IsConverted { get; set; }
        public int? LinkedProjectId { get; set; }
        public string? LinkedProjectNumber { get; set; }
        public DateTime? ConvertedAt { get; set; }

        // État de conversion calculé
        public string ConversionStatus { get; set; } // convertible, converti, non_convertible
    }

    /// <summary>
    /// DTO pour l'affichage détaillé complet
    /// </summary>
    public class DQEDetailDTO
    {
        public int Id { get; set; }
        public string Reference { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public string Statut { get; set; }
        public decimal TotalRevenueHT { get; set; }
        public decimal TauxTVA { get; set; }
        public decimal MontantTVA { get; set; }
        public decimal TotalTTC { get; set; }
        public DateTime? DateValidation { get; set; }
        public int? ValidePar { get; set; }
        public DateTime DateCreation { get; set; }

        // Client
        public ClientDTO Client { get; set; }

        // Conversion
        public bool IsConverted { get; set; }
        public ProjectLinkDTO? LinkedProject { get; set; }

        // Structure hiérarchique
        public List<DQELotDTO> Lots { get; set; }
    }

    public class ClientDTO
    {
        public int Id { get; set; }
        public string Nom { get; set; }
    }

    public class ProjectLinkDTO
    {
        public int Id { get; set; }
        public string Numero { get; set; }
        public string Nom { get; set; }
        public string Statut { get; set; }
        public int PourcentageAvancement { get; set; }
        public DateTime ConvertedAt { get; set; }
        public string ConvertedBy { get; set; }
    }

    public class DQELotDTO
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public decimal TotalRevenueHT { get; set; }
        public decimal PourcentageTotal { get; set; }
        public int ChaptersCount { get; set; }
        public int ItemsCount { get; set; }
        public List<DQEChapterDTO>? Chapters { get; set; }
    }

    public class DQEChapterDTO
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public decimal TotalRevenueHT { get; set; }
        public int ItemsCount { get; set; }
        public List<DQEItemDTO>? Items { get; set; }
    }

    public class DQEItemDTO
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Designation { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public string Unite { get; set; }
        public decimal Quantite { get; set; }
        public decimal PrixUnitaireHT { get; set; }
        public decimal TotalRevenueHT { get; set; }
    }

    /// <summary>
    /// DTO pour la prévisualisation de conversion
    /// </summary>
    public class ConversionPreviewDTO
    {
        public DQESummaryDTO DQE { get; set; }
        public ProjectPreviewDTO ProjetPrevu { get; set; }
        public List<StagePreviewDTO> EtapesPrevues { get; set; }
    }

    public class DQESummaryDTO
    {
        public int Id { get; set; }
        public string Reference { get; set; }
        public string Nom { get; set; }
        public decimal TotalRevenueHT { get; set; }
        public int LotsCount { get; set; }
        public string ClientNom { get; set; }
    }

    public class ProjectPreviewDTO
    {
        public string Nom { get; set; }
        public string NumeroProjet { get; set; }
        public decimal BudgetInitial { get; set; }
        public DateTime DateDebut { get; set; }
        public DateTime DateFinPrevue { get; set; }
        public int DureeTotaleJours { get; set; }
    }

    public class StagePreviewDTO
    {
        public string Nom { get; set; }
        public string Code { get; set; }
        public decimal BudgetPrevu { get; set; }
        public int DureeJours { get; set; }
        public DateTime DateDebut { get; set; }
        public DateTime DateFinPrevue { get; set; }
        public decimal PourcentageBudget { get; set; }
    }
}