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
        public string? Chantier { get; set; }
        public string? Contact { get; set; }
        public string? QualiteMateriel { get; set; }
        public string? TypeVitrage { get; set; }
        public Boolean Actif { get; set; } = true;

        // Navigation properties
        public virtual Client? Client { get; set; }
        public virtual List<LigneDevis>? Lignes { get; set; }
        //public virtual List<DevisSection>? Sections { get; set; }
    }

    public class DevisSection
    {
        public int Id { get; set; }
        public int DevisId { get; set; }

        /// <summary>
        /// Nom de la section (ex: "Restauration", "Office", "Bureau")
        /// </summary>
        public string Nom { get; set; }

        /// <summary>
        /// Ordre d'affichage de la section
        /// </summary>
        public int Ordre { get; set; }

        /// <summary>
        /// Description optionnelle de la section
        /// </summary>
        public string? Description { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual List<LigneDevis>? Lignes { get; set; }
    }
    public class LigneDevis
    {
        public int Id { get; set; }
        public int DevisId { get; set; }

        /// <summary>
        /// Section parente (ex: Restauration, Office)
        /// </summary>
        public int? SectionId { get; set; }

        /// <summary>
        /// Ordre d'affichage dans la section
        /// </summary>
        public int Ordre { get; set; }

        /// <summary>
        /// Type d'élément (ex: "Fenetre coulissante", "Soufflet", "Fixe et imposte")
        /// </summary>
        public string? TypeElement { get; set; }

        /// <summary>
        /// Désignation de la ligne
        /// </summary>
        public string Designation { get; set; }

        /// <summary>
        /// Description additionnelle
        /// </summary>
        public string? Description { get; set; }

        // ===== DIMENSIONS =====
        /// <summary>
        /// Longueur en cm
        /// </summary>
        public decimal? Longueur { get; set; }

        /// <summary>
        /// Hauteur en cm
        /// </summary>
        public decimal? Hauteur { get; set; }

        /// <summary>
        /// Quantité
        /// </summary>
        public decimal Quantite { get; set; } = 1;

        /// <summary>
        /// Unité (U, m², ml, etc.)
        /// </summary>
        public string Unite { get; set; } = "U";

        /// <summary>
        /// Prix unitaire HT
        /// </summary>
        public decimal PrixUnitaireHT { get; set; }

        /// <summary>
        /// Total HT calculé automatiquement
        /// </summary>
        public decimal TotalHT => Quantite * PrixUnitaireHT;

        // Navigation property
        public virtual DevisSection? Section { get; set; }
    }


    public class CreateDevisRequest
    {
        public int ClientId { get; set; }
        public string Titre { get; set; }
        public string? Description { get; set; }
        public DateTime? DateValidite { get; set; }
        public string? Conditions { get; set; }
        public string? Notes { get; set; }
        public decimal RemiseValeur { get; set; }
        public decimal RemisePourcentage { get; set; }
        // Nouveaux champs
        public string? Chantier { get; set; }
        public string? Contact { get; set; }
        public string? QualiteMateriel { get; set; }
        public string? TypeVitrage { get; set; }

        /// <summary>
        /// Sections avec leurs lignes
        /// </summary>
        public List<CreateDevisSectionRequest>? Sections { get; set; }
    }



    public class CreateDevisSectionRequest
    {
        public string Nom { get; set; }
        public int Ordre { get; set; }
        public string? Description { get; set; }
        public List<CreateLigneDevisRequest>? Lignes { get; set; }
    }

    public class CreateLigneDevisRequest
    {
        public string? TypeElement { get; set; }
        public string Designation { get; set; }
        public string? Description { get; set; }
        public decimal? Longueur { get; set; }
        public decimal? Hauteur { get; set; }
        public decimal Quantite { get; set; } = 1;
        public string Unite { get; set; } = "U";
        public decimal PrixUnitaireHT { get; set; }
    }

    public class UpdateDevisRequest : CreateDevisRequest { }

    public class DevisListItem
    {
        public int Id { get; set; }
        public string Numero { get; set; }
        public string Titre { get; set; }
        public string Statut { get; set; }
        public decimal RemiseValeur { get; set; }
        public decimal RemisePourcentage { get; set; }
        public decimal MontantTTC { get; set; }
        public DateTime DateCreation { get; set; }
        public DateTime? DateValidite { get; set; }
        public string? Chantier { get; set; }
        public ClientInfo? Client { get; set; }
        public int UtilisateurCreation { get; set; }
    }

    /// <summary>
    /// Info client simplifiée
    /// </summary>
    public class ClientInfo
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Adresse { get; set; }
    }

    /// <summary>
    /// Response complète d'un devis avec toutes les sections et lignes
    /// </summary>
    public class DevisCompletResponse
    {
        public int Id { get; set; }
        public string Numero { get; set; }
        public int ClientId { get; set; }
        public string Titre { get; set; }
        public string? Description { get; set; }
        public string Statut { get; set; }
        public decimal MontantHT { get; set; }
        public decimal TauxTVA { get; set; }
        public decimal RemiseValeur { get; set; }
        public decimal RemisePourcentage { get; set; }

        public decimal MontantTTC { get; set; }
        public DateTime DateCreation { get; set; }
        public DateTime? DateValidite { get; set; }
        public DateTime? DateEnvoi { get; set; }
        public DateTime? DateValidation { get; set; }
        public string? Conditions { get; set; }
        public string? Notes { get; set; }

        // Nouveaux champs
        public string? Chantier { get; set; }
        public string? Contact { get; set; }
        public string? QualiteMateriel { get; set; }
        public string? TypeVitrage { get; set; }
        public Boolean Actif { get; set; } = true;


        public ClientInfo? Client { get; set; }
        public List<DevisSectionResponse>? Sections { get; set; }
    }

    /// <summary>
    /// Response pour une section de devis
    /// </summary>
    public class DevisSectionResponse
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public int Ordre { get; set; }
        public string? Description { get; set; }
        public List<LigneDevisResponse>? Lignes { get; set; }

        /// <summary>
        /// Total HT de la section
        /// </summary>
        public decimal TotalSectionHT { get; set; }
    }

    /// <summary>
    /// Response pour une ligne de devis
    /// </summary>
    public class LigneDevisResponse
    {
        public int Id { get; set; }
        public int Ordre { get; set; }
        public string? TypeElement { get; set; }
        public string Designation { get; set; }
        public string? Description { get; set; }
        public decimal? Longueur { get; set; }
        public decimal? Hauteur { get; set; }
        public decimal Quantite { get; set; }
        public string Unite { get; set; }
        public decimal PrixUnitaireHT { get; set; }
        public decimal TotalHT { get; set; }
    }

    // =====================================================
    // AUTRES DTOs
    // =====================================================

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
    }

    public class RechercheDevisRequest
    {
        public string? Search { get; set; }
        public string? Statut { get; set; }
        public int? ClientId { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;
    }

    public class RechercheDevisResult
    {
        public List<DevisListItem> Devis { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int TotalPages { get; set; }
    }

    public class StatistiquesDevis
    {
        public int Total { get; set; }
        public int Brouillon { get; set; }
        public int Envoye { get; set; }
        public int EnNegociation { get; set; }
        public int Valide { get; set; }
        public int Refuse { get; set; }
        public int Expire { get; set; }
        public decimal MontantTotal { get; set; }
        public decimal MontantValide { get; set; }
    }
}
