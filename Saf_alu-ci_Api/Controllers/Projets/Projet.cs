using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.SousTraitants;
using Saf_alu_ci_Api.Controllers.Utilisateurs;

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

        /// <summary>
        /// ID du DQE source (si projet créé depuis conversion DQE)
        /// </summary>
        public int? LinkedDqeId { get; set; }

        /// <summary>
        /// Référence du DQE source (ex: DQE-2024-023)
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
        /// Indique si le projet a été créé depuis un DQE
        /// </summary>
        public bool IsFromDqeConversion { get; set; } = false;

        /// <summary>
        /// Date de conversion du DQE en projet
        /// </summary>
        public DateTime? DqeConvertedAt { get; set; }

        /// <summary>
        /// ID de l'utilisateur qui a effectué la conversion
        /// </summary>
        public int? DqeConvertedById { get; set; }

        // Navigation properties
        public virtual Client? Client { get; set; }
        public virtual TypeProjet? TypeProjet { get; set; }
        public virtual Utilisateur? ChefProjet { get; set; }
        public virtual Utilisateur? DqeConvertedBy { get; set; }
        public virtual List<EtapeProjet>? Etapes { get; set; }
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
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }
        public string Statut { get; set; } = "NonCommence";
        public int PourcentageAvancement { get; set; } = 0;
        public decimal BudgetPrevu { get; set; }
        public decimal CoutReel { get; set; }
        public decimal Depense { get; set; } = 0;
        public int? ResponsableId { get; set; }
        public string TypeResponsable { get; set; } = "Interne";

        // ✅ NOUVELLE PROPRIÉTÉ
        /// <summary>
        /// ID du sous-traitant assigné à cette étape (si TypeResponsable = "SousTraitant")
        /// </summary>
        public int? IdSousTraitant { get; set; }

        // Propriétés DQE
        public int? LinkedDqeLotId { get; set; }
        public string? LinkedDqeLotCode { get; set; }
        public string? LinkedDqeLotName { get; set; }
        public string? LinkedDqeReference { get; set; }
        public bool EstActif { get; set; } = true;

        // ✅ Navigation property pour le sous-traitant
        public virtual SousTraitant? SousTraitant { get; set; }
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

    public class CreateEtapeProjetRequest
    {
        public int? Id { get; set; }  // Pour mise à jour
        public string Nom { get; set; }
        public string? Description { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public decimal BudgetPrevu { get; set; }
        public decimal coutReel { get; set; }
        public string Statut { get; set; } = "NonCommence";
        public bool EstActif { get; set; } = true;

        // ✅ NOUVELLE PROPRIÉTÉ
        /// <summary>
        /// ID du sous-traitant (si TypeResponsable = "SousTraitant")
        /// </summary>
        public int? IdSousTraitant { get; set; }

        // Propriétés DQE existantes
        public int? LinkedDqeLotId { get; set; }
        public string? LinkedDqeLotCode { get; set; }
        public string? LinkedDqeLotName { get; set; }
        public string? LinkedDqeReference { get; set; }
    }
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
    public class UpdateEtapeProjetRequest
    {
        public int? Id { get; set; }
        public string? Nom { get; set; }
        public string? Description { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public decimal? BudgetPrevu { get; set; }
        public decimal? CoutReel { get; set; }
        public string? Statut { get; set; }
        public int? ResponsableId { get; set; }
        public string? TypeResponsable { get; set; }
        public bool EstActif { get; set; }

        // ✅ NOUVELLE PROPRIÉTÉ
        /// <summary>
        /// ID du sous-traitant (si TypeResponsable = "SousTraitant")
        /// </summary>
        public int? IdSousTraitant { get; set; }
    }
}