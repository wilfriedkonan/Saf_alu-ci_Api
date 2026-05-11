using System.ComponentModel.DataAnnotations;

namespace Saf_alu_ci_Api.Controllers.DevisFournisseur
{
    // =============================================
    // MODÈLES — FOURNISSEUR
    // =============================================

    public class Fournisseur
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string? RaisonSociale { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
        public string? NomContact { get; set; }
        public string? TelephoneContact { get; set; }
        public string? EmailContact { get; set; }
        public string? Ncc { get; set; }
        public bool Actif { get; set; } = true;
        public DateTime DateCreation { get; set; }
        public DateTime DateModification { get; set; }
        public int UtilisateurCreation { get; set; }
    }

    // =============================================
    // MODÈLES — DEVIS FOURNISSEUR
    // =============================================

    public class DevisFournisseurHeader
    {
        public int Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string TypeDevis { get; set; } = string.Empty;    // Classique | Technique
        public string Titre { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DateLimiteReponse { get; set; }
        public decimal RemiseGlobalePct { get; set; }
        public decimal RemiseGlobaleValeur { get; set; }
        public string Statut { get; set; } = "Brouillon";       // Brouillon | EnCours | Cloture | Selectionne
        public DateTime DateCreation { get; set; }
        public DateTime DateModification { get; set; }
        public int UtilisateurCreation { get; set; }
        public int UtilisateurModification { get; set; }

        // Navigation
        public List<DevisFournisseurSection> Sections { get; set; } = new();
        public List<DevisFournisseurLigne> Lignes { get; set; } = new();
        public List<DevisFournisseurDemande> Demandes { get; set; } = new();
    }

    public class DevisFournisseurSection
    {
        public int Id { get; set; }
        public int DevisId { get; set; }
        public int Ordre { get; set; }
        public string Titre { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal RemiseSectionPct { get; set; }
        public decimal RemiseSectionValeur { get; set; }
        public List<DevisFournisseurLigne> Lignes { get; set; } = new();
    }

    public class DevisFournisseurLigne
    {
        public int Id { get; set; }
        public int DevisId { get; set; }
        public int? SectionId { get; set; }
        public int Ordre { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Unite { get; set; }
        public decimal Quantite { get; set; }
        // Technique uniquement
        public string? TypeElement { get; set; }
        public decimal? DimensionL { get; set; }
        public decimal? DimensionH { get; set; }
        // Remises
        public decimal RemiseLignePct { get; set; }
        public decimal RemiseLigneValeur { get; set; }
    }

    public class DevisFournisseurDemande
    {
        public int Id { get; set; }
        public int DevisId { get; set; }
        public int FournisseurId { get; set; }
        public string? FournisseurNom { get; set; }
        public string? FournisseurTelephone { get; set; }
        public Guid Token { get; set; }
        public string Otp { get; set; } = string.Empty;
        public DateTime DateExpiration { get; set; }
        public int NbTentativesOtp { get; set; }
        public DateTime? OtpValideA { get; set; }
        public string Statut { get; set; } = "EnAttente";
        public string? MessageWhatsApp { get; set; }
        public DateTime? DateEnvoi { get; set; }
        public DateTime? DateOuvertureLien { get; set; }
        public DateTime? DateReponse { get; set; }
        public bool Selectionne { get; set; }
        public DateTime? DateSelection { get; set; }
        public string? CommentaireSelection { get; set; }
        public DateTime DateCreation { get; set; }
        public List<DevisFournisseurLigneReponse> Reponses { get; set; } = new();
    }

    public class DevisFournisseurLigneReponse
    {
        public int Id { get; set; }
        public int DemandeId { get; set; }
        public int LigneId { get; set; }
        public decimal PrixUnitaire { get; set; }
        public string? Commentaire { get; set; }
        public DateTime DateSaisie { get; set; }
        public bool LigneSelectionnee { get; set; }
    }

    // =============================================
    // DTOs — FOURNISSEUR
    // =============================================

    public class CreateFournisseurRequest
    {
        [Required][StringLength(150)] public string Nom { get; set; } = string.Empty;
        [StringLength(200)] public string? RaisonSociale { get; set; }
        [StringLength(150)] public string? Email { get; set; }
        [StringLength(20)] public string? Telephone { get; set; }
        [StringLength(255)] public string? Adresse { get; set; }
        [StringLength(100)] public string? Ville { get; set; }
        [StringLength(150)] public string? NomContact { get; set; }
        [StringLength(20)] public string? TelephoneContact { get; set; }
        [StringLength(150)] public string? EmailContact { get; set; }
        [StringLength(50)] public string? Ncc { get; set; }
    }

    public class UpdateFournisseurRequest : CreateFournisseurRequest { }

    // =============================================
    // DTOs — DEVIS (création / modification)
    // =============================================

    public class CreateDevisFournisseurRequest
    {
        [Required] public string TypeDevis { get; set; } = string.Empty;   // Classique | Technique
        [Required][StringLength(200)] public string Titre { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Required] public DateTime DateLimiteReponse { get; set; }
        public decimal RemiseGlobalePct { get; set; } = 0;
        public decimal RemiseGlobaleValeur { get; set; } = 0;

        // Sections — Technique uniquement (ignoré pour Classique)
        public List<CreateSectionRequest> Sections { get; set; } = new();
        // Lignes — Classique (sans SectionId) ou Technique (avec SectionIndex)
        public List<CreateLigneRequest> Lignes { get; set; } = new();
    }

    public class UpdateDevisFournisseurRequest
    {
        [Required][StringLength(200)] public string Titre { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Required] public DateTime DateLimiteReponse { get; set; }
        public decimal RemiseGlobalePct { get; set; } = 0;
        public decimal RemiseGlobaleValeur { get; set; } = 0;
    }

    // =============================================
    // DTOs — SECTIONS
    // =============================================

    public class CreateSectionRequest
    {
        [Required][StringLength(150)] public string Titre { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal RemiseSectionPct { get; set; } = 0;
        public decimal RemiseSectionValeur { get; set; } = 0;
        public int Ordre { get; set; } = 1;
    }

    public class UpdateSectionRequest : CreateSectionRequest { }

    // =============================================
    // DTOs — LIGNES
    // =============================================

    public class CreateLigneRequest
    {
        [Required][StringLength(300)] public string Designation { get; set; } = string.Empty;
        [StringLength(500)] public string? Description { get; set; }
        [StringLength(20)] public string? Unite { get; set; }
        public decimal Quantite { get; set; } = 1;
        public int? SectionId { get; set; }
        public int Ordre { get; set; } = 1;
        // Technique
        [StringLength(100)] public string? TypeElement { get; set; }
        public decimal? DimensionL { get; set; }
        public decimal? DimensionH { get; set; }
        // Remises
        public decimal RemiseLignePct { get; set; } = 0;
        public decimal RemiseLigneValeur { get; set; } = 0;
    }

    public class UpdateLigneRequest : CreateLigneRequest { }

    // =============================================
    // DTOs — DEMANDES (envoi aux fournisseurs)
    // =============================================

    public class EnvoyerDemandesRequest
    {
        [Required] public List<int> FournisseurIds { get; set; } = new();
        /// <summary>Durée de validité du lien en heures (défaut 48h)</summary>
        public int DureeValiditeHeures { get; set; } = 48;
        public string? MessagePersonnalise { get; set; }
    }

    // =============================================
    // DTOs — ACCÈS PUBLIC (fournisseur)
    // =============================================

    public class ValiderOtpRequest
    {
        [Required] public string Otp { get; set; } = string.Empty;
    }

    public class SoumettreReponsesRequest
    {
        [Required] public List<LigneReponseItem> Reponses { get; set; } = new();
    }

    public class LigneReponseItem
    {
        public int LigneId { get; set; }
        [Range(0, double.MaxValue)] public decimal PrixUnitaire { get; set; }
        public string? Commentaire { get; set; }
    }

    // =============================================
    // DTOs — SÉLECTION
    // =============================================

    public class SelectionnerFournisseurRequest
    {
        [Required] public int DemandeId { get; set; }
        public string? Commentaire { get; set; }
    }

    public class SelectionnerLignesRequest
    {
        /// <summary>Clé = LigneId, Valeur = DemandeId du fournisseur retenu pour cette ligne</summary>
        [Required] public Dictionary<int, int> SelectionParLigne { get; set; } = new();
    }

    // =============================================
    // DTOs — RÉPONSES / VUES
    // =============================================

    public class DevisPublicDTO
    {
        public int Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string TypeDevis { get; set; } = string.Empty;
        public string Titre { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DateLimiteReponse { get; set; }
        public string FournisseurNom { get; set; } = string.Empty;
        public bool DejaRepondu { get; set; }
        public List<SectionPublicDTO> Sections { get; set; } = new();
        public List<LignePublicDTO> Lignes { get; set; } = new();
    }

    public class SectionPublicDTO
    {
        public int Id { get; set; }
        public int Ordre { get; set; }
        public string Titre { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<LignePublicDTO> Lignes { get; set; } = new();
    }

    public class LignePublicDTO
    {
        public int Id { get; set; }
        public int? SectionId { get; set; }
        public int Ordre { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Unite { get; set; }
        public decimal Quantite { get; set; }
        public string? TypeElement { get; set; }
        public decimal? DimensionL { get; set; }
        public decimal? DimensionH { get; set; }
        // Prix déjà saisi (si le fournisseur revient sur le formulaire)
        public decimal? PrixUnitaireSaisi { get; set; }
        public string? CommentaireSaisi { get; set; }
    }

    public class ComparaisonLigneDTO
    {
        public int LigneId { get; set; }
        public int Ordre { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string? Unite { get; set; }
        public decimal Quantite { get; set; }
        public string? TypeElement { get; set; }
        public decimal? DimensionL { get; set; }
        public decimal? DimensionH { get; set; }
        public List<OffreLigneDTO> Offres { get; set; } = new();
    }

    public class OffreLigneDTO
    {
        public int DemandeId { get; set; }
        public int FournisseurId { get; set; }
        public string FournisseurNom { get; set; } = string.Empty;
        public decimal PrixUnitaire { get; set; }
        public decimal MontantBrut { get; set; }
        public decimal MontantNet { get; set; }
        public string? Commentaire { get; set; }
        public int RangPrix { get; set; }
        public bool LigneSelectionnee { get; set; }
    }

    public class ComparaisonDevisDTO
    {
        public int DevisId { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Titre { get; set; } = string.Empty;
        public string TypeDevis { get; set; } = string.Empty;
        public int NombreFournisseursAyantRepondu { get; set; }
        public List<FournisseurTotalDTO> TotauxParFournisseur { get; set; } = new();
        public List<SectionComparaisonDTO> Sections { get; set; } = new();
        public List<ComparaisonLigneDTO> Lignes { get; set; } = new();
    }

    public class FournisseurTotalDTO
    {
        public int DemandeId { get; set; }
        public int FournisseurId { get; set; }
        public string FournisseurNom { get; set; } = string.Empty;
        public decimal TotalBrut { get; set; }
        public decimal TotalNet { get; set; }
        public bool Selectionne { get; set; }
        public string? CommentaireSelection { get; set; }
    }

    public class SectionComparaisonDTO
    {
        public int SectionId { get; set; }
        public string SectionTitre { get; set; } = string.Empty;
        public List<ComparaisonLigneDTO> Lignes { get; set; } = new();
    }
}