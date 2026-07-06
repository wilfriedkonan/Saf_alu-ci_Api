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
        public string Statut { get; set; } = "Brouillon";
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
        public string? TypeDevis { get; set; }

        public virtual Client? Client { get; set; }
        public virtual List<LigneDevis>? Lignes { get; set; }

    }

    public class DevisSection
    {
        public int Id { get; set; }
        public int DevisId { get; set; }
        public string Nom { get; set; }
        public int Ordre { get; set; }
        public string? Description { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        public virtual List<DevisSousSection>? SousSections { get; set; }
        public virtual List<LigneDevis>? Lignes { get; set; }
    }

    // =====================================================
    // 🆕 SOUS-SECTION
    // =====================================================
    public class DevisSousSection
    {
        public int Id { get; set; }
        public int SectionId { get; set; }
        public int DevisId { get; set; }

        /// <summary>Code saisi manuellement (ex: SS-01, A1)</summary>
        public string? Code { get; set; }

        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        public virtual List<LigneDevis>? Lignes { get; set; }
    }

    public class LigneDevis
    {
        public int Id { get; set; }
        public int DevisId { get; set; }
        public int? SectionId { get; set; }

        /// <summary>🆕 Sous-section parente (nullable — ligne rattachée à la section si null)</summary>
        public int? SousSectionId { get; set; }

        public int Ordre { get; set; }
        public string? TypeElement { get; set; }
        public string Designation { get; set; }
        public string? Description { get; set; }

        /// <summary>🆕 Code de la ligne, saisi manuellement</summary>
        public string? Code { get; set; }

        public decimal? Longueur { get; set; }
        public decimal? Hauteur { get; set; }
        public decimal Quantite { get; set; } = 1;
        public string Unite { get; set; } = "U";
        public decimal PrixUnitaireHT { get; set; }
        public decimal TotalHT => Quantite * PrixUnitaireHT;

        public virtual DevisSection? Section { get; set; }
        public virtual DevisSousSection? SousSection { get; set; }
    }

    // =====================================================
    // DTOs CRÉATION / MISE À JOUR
    // =====================================================

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
        public string? Chantier { get; set; }
        public string? Contact { get; set; }
        public string? QualiteMateriel { get; set; }
        public string? TypeVitrage { get; set; }
        public List<CreateDevisSectionRequest>? Sections { get; set; }
        public string? TypeDevis { get; set; }
    }

    public class CreateDevisSectionRequest
    {
        public string Nom { get; set; }
        public int Ordre { get; set; }
        public string? Description { get; set; }

        /// <summary>🆕 Sous-sections optionnelles</summary>
        public List<CreateDevisSousSectionRequest>? SousSections { get; set; }

        /// <summary>Lignes directement rattachées à la section (SousSectionId = null)</summary>
        public List<CreateLigneDevisRequest>? Lignes { get; set; }
    }

    // =====================================================
    // 🆕 DTO SOUS-SECTION
    // =====================================================
    public class CreateDevisSousSectionRequest
    {
        public string? Code { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public List<CreateLigneDevisRequest>? Lignes { get; set; }
    }

    public class UpdateDevisSousSectionRequest
    {
        public string? Code { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
    }

    public class CreateLigneDevisRequest
    {
        public string? TypeElement { get; set; }
        public string Designation { get; set; }
        public string? Description { get; set; }

        /// <summary>🆕 Code de la ligne</summary>
        public string? Code { get; set; }

        public decimal? Longueur { get; set; }
        public decimal? Hauteur { get; set; }
        public decimal Quantite { get; set; } = 1;
        public string Unite { get; set; } = "U";
        public decimal PrixUnitaireHT { get; set; }

        /// <summary>🆕 Si renseigné, la ligne est rattachée à une sous-section</summary>
        public int? SousSectionId { get; set; }
    }

    public class UpdateDevisRequest : CreateDevisRequest { }

    // =====================================================
    // DTOs LECTURE
    // =====================================================

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

    public class ClientInfo
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Adresse { get; set; }
    }

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
        public string? Chantier { get; set; }
        public string? Contact { get; set; }
        public string? QualiteMateriel { get; set; }
        public string? TypeVitrage { get; set; }
        public Boolean Actif { get; set; } = true;

        public ClientInfo? Client { get; set; }
        public List<DevisSectionResponse>? Sections { get; set; }
        public string? TypeDevis { get; set; }
    }

    public class DevisSectionResponse
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public int Ordre { get; set; }
        public string? Description { get; set; }
        public decimal TotalSectionHT { get; set; }

        /// <summary>🆕 Sous-sections de cette section</summary>
        public List<DevisSousSectionResponse> SousSections { get; set; } = new();

        /// <summary>Lignes directement sur la section (sans sous-section)</summary>
        public List<LigneDevisResponse>? Lignes { get; set; }
    }

    // =====================================================
    // 🆕 RÉPONSE SOUS-SECTION
    // =====================================================
    public class DevisSousSectionResponse
    {
        public int Id { get; set; }
        public int SectionId { get; set; }
        public string? Code { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public decimal TotalSousSectionHT { get; set; }
        public List<LigneDevisResponse> Lignes { get; set; } = new();
    }

    public class LigneDevisResponse
    {
        public int Id { get; set; }
        public int Ordre { get; set; }
        public string? TypeElement { get; set; }
        public string Designation { get; set; }
        public string? Description { get; set; }

        /// <summary>🆕</summary>
        public string? Code { get; set; }

        /// <summary>🆕 Null si la ligne est directement sur la section</summary>
        public int? SousSectionId { get; set; }

        public decimal? Longueur { get; set; }
        public decimal? Hauteur { get; set; }
        public decimal Quantite { get; set; }
        public string Unite { get; set; }
        public decimal PrixUnitaireHT { get; set; }
        public decimal TotalHT { get; set; }
    }

    // =====================================================
    // AUTRES DTOs (inchangés)
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