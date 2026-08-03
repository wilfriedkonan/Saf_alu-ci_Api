using Saf_alu_ci_Api.Controllers.Utilisateurs;

namespace Saf_alu_ci_Api.Controllers.Stock
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }

    // ============================================================
    // PARAMÈTRES DE RECHERCHE + PAGINATION PAR ENTITÉ
    // ============================================================

    public class ArticleSearchParams
    {
        /// <summary>Recherche sur Nom, Référence, Description</summary>
        public string? Search { get; set; }
        public int? CategorieId { get; set; }
        public int? FournisseurId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class FournisseurSearchParams
    {
        /// <summary>Recherche sur Nom, Code, Ville, Email, Contact</summary>
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class CategorieSearchParams
    {
        /// <summary>Recherche sur Nom, Code</summary>
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class DepotSearchParams
    {
        /// <summary>Recherche sur Nom, Code, Ville</summary>
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class DemandeSearchParams
    {
        public string? Statut { get; set; }
        public int? ProjetId { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        /// <summary>Recherche sur N° demande, nom demandeur, désignation article (catalogue ou libre)</summary>
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class MouvementSearchParams
    {
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        public int? ArticleId { get; set; }
        public int? DepotId { get; set; }
        public string? TypeMouvement { get; set; }
        /// <summary>Recherche sur nom article, référence article, N° demande</summary>
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class EtatStockSearchParams
    {
        public int? DepotId { get; set; }
        public bool AlertesSeulement { get; set; } = false;
        /// <summary>Recherche sur nom article, référence, catégorie, dépôt</summary>
        public string? Search { get; set; }
        public int? CategorieId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    // ============================================================
    // ENTITÉS
    // ============================================================

    public class StockCategorie
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Couleur { get; set; } = "#2563eb";
        public bool Actif { get; set; } = true;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
    }

    public class StockDepot
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
        public int? ResponsableId { get; set; }
        public bool Actif { get; set; } = true;
        /// <summary>Un seul dépôt actif peut être par défaut (point d'entrée des livraisons fournisseur)</summary>
        public bool EstParDefaut { get; set; } = false;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public virtual Utilisateur? Responsable { get; set; }
    }

    public class StockFournisseur
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string? Contact { get; set; }
        public string? Telephone { get; set; }
        public string? Email { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
        public int? NoteEvaluation { get; set; }
        public bool Actif { get; set; } = true;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
    }

    public class StockArticle
    {
        public int Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CategorieId { get; set; }
        public string Unite { get; set; } = string.Empty;
        public decimal PrixUnitaireMoyen { get; set; } = 0;
        public decimal SeuilMinimum { get; set; } = 0;
        public decimal? SeuilMaximum { get; set; }
        public int? FournisseurPreferentielId { get; set; }
        public bool Actif { get; set; } = true;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public virtual StockCategorie? Categorie { get; set; }
        public virtual StockFournisseur? FournisseurPreferentiel { get; set; }
    }

    public class StockInventaire
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public int DepotId { get; set; }
        public decimal QuantiteDisponible { get; set; } = 0;
        public decimal QuantiteReservee { get; set; } = 0;
        public DateTime? DateDernierMouvement { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
    }

    public class StockDemande
    {
        public int Id { get; set; }
        public string Numero { get; set; } = string.Empty;
        public string NomDemandeur { get; set; } = string.Empty;
        public string PosteDemandeur { get; set; } = string.Empty;
        public int? UtilisateurId { get; set; }
        /// <summary>Id de l'utilisateur demandeur (lien fort, renseigné à la création)</summary>
        public int? DemandeurId { get; set; }
        /// <summary>Dépôt de destination de la demande — renseigné par le demandeur.
        /// Utilisé lors de la livraison pour transférer les articles vers ce dépôt.</summary>
        public int? DepotDemandeId { get; set; }
        public string TypeDestination { get; set; } = "Administration";
        public int? ProjetId { get; set; }
        public int? EtapeProjetId { get; set; }
        public string Statut { get; set; } = "EnAttente";
        public string? MotifDemande { get; set; }
        public decimal MontantTotal { get; set; } = 0;
        public string? NotesTraitement { get; set; }
        public string? NotesValidation { get; set; }
        public int? ValidateurId { get; set; }
        public DateTime DateDemande { get; set; } = DateTime.UtcNow;
        public DateTime? DateDebutTraitement { get; set; }
        public DateTime? DateValidation { get; set; }
        public DateTime? DateLivraisonPrevue { get; set; }
        public DateTime? DateLivraisonReelle { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public virtual List<StockDemandeArticle>? Articles { get; set; }
        public virtual StockTraitement? Traitement { get; set; }
    }

    public class StockDemandeArticle
    {
        public int Id { get; set; }
        public int DemandeId { get; set; }
        public int? ArticleId { get; set; }
        public string? DesignationLibre { get; set; }
        public string Unite { get; set; } = string.Empty;
        public decimal QuantiteDemandee { get; set; }
        public decimal? QuantiteValidee { get; set; }
        public decimal? PrixUnitaireDevis { get; set; }
        public decimal? PrixTotalLigne { get; set; }
        public string? Notes { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;

        // ── Traitement partiel ──────────────────────────────────
        /// <summary>"Stock" | "Commande" | "CommandeReste"</summary>
        public string Source { get; set; } = "Commande";
        public int? DepotDotationId { get; set; }
        public decimal? QuantiteDotee { get; set; }

        // ── Confirmation de livraison ───────────────────────────
        public bool EstLivre { get; set; } = false;
        public int? UserValidationLivraisonId { get; set; }
        public DateTime? DateLivraisonConfirmee { get; set; }

        public virtual StockArticle? Article { get; set; }
        public virtual List<StockDemandeArticleDotation>? Dotations { get; set; }
    }

    /// <summary>
    /// Dotation multi-dépôt pour une ligne article en source "Stock".
    /// Table : Stock_DemandeArticleDotations
    /// </summary>
    public class StockDemandeArticleDotation
    {
        public int Id { get; set; }
        public int DemandeArticleId { get; set; }
        public int DepotId { get; set; }
        public decimal QuantiteDotee { get; set; }
        public bool EstLivre { get; set; } = false;
        public int? UserValidationId { get; set; }
        public DateTime? DateLivraisonConfirmee { get; set; }
        /// <summary>Mouvement de sortie créé lors de la livraison directe</summary>
        public int? MouvementId { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public virtual StockDepot? Depot { get; set; }
    }

    public class StockTraitement
    {
        public int Id { get; set; }
        public int DemandeId { get; set; }
        public int? FournisseurId { get; set; }
        public string? NomFournisseurLibre { get; set; }
        public string? NumeroDevis { get; set; }
        public decimal? MontantDevisHT { get; set; }
        public decimal? MontantDevisTTC { get; set; }
        public DateTime? DateDevis { get; set; }
        public string? FichierDevisPath { get; set; }
        public string? DelaiLivraison { get; set; }
        public string? ConditionsPaiement { get; set; }
        public string? Notes { get; set; }
        public string StatutTraitement { get; set; } = "Brouillon";
        public int? TraitePar { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public virtual StockFournisseur? Fournisseur { get; set; }
    }

    public class StockMouvement
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public int DepotId { get; set; }
        public string TypeMouvement { get; set; } = string.Empty;
        public decimal Quantite { get; set; }
        public decimal QuantiteAvant { get; set; }
        public decimal QuantiteApres { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public decimal? MontantTotal { get; set; }
        public string? Reference { get; set; }
        public int? DemandeId { get; set; }
        public int? ProjetId { get; set; }
        public int? EtapeProjetId { get; set; }
        public int? DepotDestinationId { get; set; }
        public string? MotifSortie { get; set; }
        public int OperateurId { get; set; }
        public DateTime DateMouvement { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    }

    // ============================================================
    // REQUEST DTOs — CATÉGORIES
    // ============================================================

    public class CreateStockCategorieRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Couleur { get; set; } = "#2563eb";
    }

    public class UpdateStockCategorieRequest
    {
        public string? Nom { get; set; }
        public string? Description { get; set; }
        public string? Couleur { get; set; }
    }

    // ============================================================
    // REQUEST DTOs — DÉPÔTS
    // ============================================================

    public class CreateStockDepotRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
        public int? ResponsableId { get; set; }
        /// <summary>Si true, ce dépôt devient le dépôt par défaut (les autres seront déclassés)</summary>
        public bool EstParDefaut { get; set; } = false;
    }

    public class UpdateStockDepotRequest
    {
        public string? Nom { get; set; }
        public string? Description { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
        public int? ResponsableId { get; set; }
        /// <summary>Si true, ce dépôt devient le dépôt par défaut (les autres seront déclassés)</summary>
        public bool? EstParDefaut { get; set; }
    }

    // ============================================================
    // REQUEST DTOs — FOURNISSEURS
    // ============================================================

    public class CreateStockFournisseurRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string? Contact { get; set; }
        public string? Telephone { get; set; }
        public string? Email { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
    }

    public class UpdateStockFournisseurRequest
    {
        public string? Nom { get; set; }
        public string? Contact { get; set; }
        public string? Telephone { get; set; }
        public string? Email { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
        public int? NoteEvaluation { get; set; }
    }

    // ============================================================
    // REQUEST DTOs — ARTICLES
    // ============================================================

    public class CreateStockArticleRequest
    {
        public string Reference { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CategorieId { get; set; }
        public string Unite { get; set; } = string.Empty;
        public decimal PrixUnitaireMoyen { get; set; } = 0;
        public decimal SeuilMinimum { get; set; } = 0;
        public decimal? SeuilMaximum { get; set; }
        public int? FournisseurPreferentielId { get; set; }
    }

    public class UpdateStockArticleRequest
    {
        public string? Nom { get; set; }
        public string? Description { get; set; }
        public int? CategorieId { get; set; }
        public string? Unite { get; set; }
        public decimal? PrixUnitaireMoyen { get; set; }
        public decimal? SeuilMinimum { get; set; }
        public decimal? SeuilMaximum { get; set; }
        public int? FournisseurPreferentielId { get; set; }
    }

    // ============================================================
    // REQUEST DTOs — DEMANDES
    // ============================================================

    public class CreateStockDemandeRequest
    {
        public string NomDemandeur { get; set; } = string.Empty;
        public string PosteDemandeur { get; set; } = string.Empty;
        public int? UtilisateurId { get; set; }
        /// <summary>Id de l'utilisateur connecté qui fait la demande</summary>
        public int? DemandeurId { get; set; }
        public string TypeDestination { get; set; } = "Administration";
        public int? ProjetId { get; set; }
        public int? EtapeProjetId { get; set; }
        public string? MotifDemande { get; set; }
        /// <summary>Dépôt de destination choisi par le demandeur.
        /// Les articles livrés seront transférés vers ce dépôt.</summary>
        public int? DepotDemandeId { get; set; }
        public List<StockDemandeArticleRequest> Articles { get; set; } = new();
    }

    public class StockDemandeArticleRequest
    {
        public int? ArticleId { get; set; }
        public string? DesignationLibre { get; set; }
        public bool isHorsCatalogue { get; set; } = false;
        public string Unite { get; set; } = string.Empty;
        public decimal QuantiteDemandee { get; set; }

        public string? Notes { get; set; }
    }

    // ============================================================
    // REQUEST DTOs — TRAITEMENT
    // ============================================================

    public class SauvegarderTraitementRequest
    {
        public int? FournisseurId { get; set; }
        public string? NomFournisseurLibre { get; set; }
        public string? NumeroDevis { get; set; }
        public decimal? MontantDevisHT { get; set; }
        public decimal? MontantDevisTTC { get; set; }
        public DateTime? DateDevis { get; set; }
        public string? FichierDevisPath { get; set; }
        public string? DelaiLivraison { get; set; }
        public string? ConditionsPaiement { get; set; }
        public string? Notes { get; set; }
        public int TraitePar { get; set; }
        public List<MajPrixArticleRequest> articlesValides { get; set; } = new();
    }

    public class SoumettreTraitementRequest : SauvegarderTraitementRequest { }

    public class MajPrixArticleRequest
    {
        public int DemandeArticleId { get; set; }
        public decimal QuantiteValidee { get; set; }
        public decimal PrixUnitaireDevis { get; set; }

        // ── Traitement partiel ──────────────────────────────────
        /// <summary>"Stock" | "Commande"</summary>
        public string Source { get; set; } = "Commande";
        /// <summary>Requis si Source = "Stock"</summary>
        public int? DepotDotationId { get; set; }
        public decimal? QuantiteDotee { get; set; }
    }

    // ============================================================
    // REQUEST DTOs — LIVRAISON
    // ============================================================

    // ============================================================
    // REQUEST DTOs — LIVRAISON DIRECTE (bouton "Livré" du traitement)
    // ============================================================

    /// <summary>
    /// Livraison directe depuis le formulaire de traitement (bouton "Livré" sur une sous-ligne dépôt).
    /// Autonome : crée ou met à jour la dotation dans la même transaction.
    /// Aucun appel préalable à SauvegarderTraitement n'est nécessaire.
    /// </summary>
    public class LivraisonDirecteRequest
    {
        /// <summary>Id de la ligne Stock_DemandeArticles (article de la demande)</summary>
        public int DemandeArticleId { get; set; }
        /// <summary>Dépôt depuis lequel prélever</summary>
        public int DepotId { get; set; }
        /// <summary>Quantité à prélever</summary>
        public decimal Quantite { get; set; }
    }

    /// <summary>Réponse de la livraison directe</summary>
    public class LivraisonDirecteResponse
    {
        /// <summary>M1 : Sortie du dépôt source (prélèvement)</summary>
        public int MouvementSortieId { get; set; }
        /// <summary>M2 : Entrée dans le dépôt de la demande (dotation effective).
        /// Null si la demande n'a pas de DepotDemandeId ou si même dépôt.</summary>
        public int? MouvementEntreeDepotDemandeId { get; set; }
        public decimal StockApresSource { get; set; }
        public bool TousLivres { get; set; }
        public string NouveauStatut { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Confirmation de réception d'un article (userId récupéré depuis le token JWT)</summary>
    public class ConfirmerLivraisonArticleRequest
    {
        public int DemandeArticleId { get; set; }
    }

    /// <summary>Confirmation en masse de tous les articles non livrés d'une demande</summary>
    public class ConfirmerToutesLivraisonsRequest
    {
        public int DemandeId { get; set; }
    }

    /// <summary>Finalisation de la livraison : passe la demande à AttenteComptabilite</summary>
    public class FinaliserLivraisonRequest
    {
        public int DemandeId { get; set; }
    }

    // ============================================================
    // REQUEST DTOs — VALIDATION
    // ============================================================

    public class ValiderDemandeRequest
    {
        public int ValidateurId { get; set; }
        public string? NotesValidation { get; set; }
        public DateTime? DateLivraisonPrevue { get; set; }
    }

    public class RejeterDemandeRequest
    {
        public int ValidateurId { get; set; }
        public string MotifRejet { get; set; } = string.Empty;
    }

    // ============================================================
    // REQUEST DTOs — MOUVEMENTS
    // ============================================================

    public class EnregistrerEntreeRequest
    {
        public int ArticleId { get; set; }
        public int DepotId { get; set; }
        public decimal Quantite { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public string? Reference { get; set; }
        public int? DemandeId { get; set; }
        public int OperateurId { get; set; }
        public string? Notes { get; set; }
    }

    public class EnregistrerSortieRequest
    {
        public int ArticleId { get; set; }
        public int DepotId { get; set; }
        public decimal Quantite { get; set; }
        public string? Reference { get; set; }
        public int? DemandeId { get; set; }
        public int? ProjetId { get; set; }
        public int? EtapeProjetId { get; set; }
        public string? MotifSortie { get; set; }
        public int OperateurId { get; set; }
        public string? Notes { get; set; }
    }

    public class EnregistrerTransfertRequest
    {
        public int ArticleId { get; set; }
        public int DepotSourceId { get; set; }
        public int DepotDestinationId { get; set; }
        public decimal Quantite { get; set; }
        public int OperateurId { get; set; }
        public string? Notes { get; set; }
    }

    public class EnregistrerLivraisonRequest
    {
        public int DemandeId { get; set; }
        public int DepotId { get; set; }
        public int OperateurId { get; set; }
        public string? Notes { get; set; }
        public List<LigneLivraisonRequest> Lignes { get; set; } = new();
    }

    public class LigneLivraisonRequest
    {
        public int DemandeArticleId { get; set; }
        public int ArticleId { get; set; }
        public decimal QuantiteLivree { get; set; }
        public decimal? PrixUnitaire { get; set; }
    }

    // ============================================================
    // RESPONSE DTOs — VUE v_Stock_EtatStocks
    // ============================================================

    public class EtatStockDTO
    {
        public int ArticleId { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string ArticleNom { get; set; } = string.Empty;
        public string Unite { get; set; } = string.Empty;
        public decimal SeuilMinimum { get; set; }
        public decimal? SeuilMaximum { get; set; }
        public decimal PrixUnitaireMoyen { get; set; }
        public int CategorieId { get; set; }
        public string CategorieNom { get; set; } = string.Empty;
        public string CategorieCouleur { get; set; } = string.Empty;
        public int DepotId { get; set; }
        public string DepotCode { get; set; } = string.Empty;
        public string DepotNom { get; set; } = string.Empty;
        public decimal QuantiteDisponible { get; set; }
        public decimal QuantiteReservee { get; set; }
        public decimal QuantiteLibre { get; set; }
        public decimal ValeurStock { get; set; }
        public string NiveauAlerte { get; set; } = string.Empty;
        public bool EnAlerte { get; set; }
        public DateTime? DateDernierMouvement { get; set; }
    }

    // ============================================================
    // RESPONSE DTOs — VUE v_Stock_AlertesMinimum
    // ============================================================

    public class AlerteStockDTO
    {
        public int ArticleId { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string ArticleNom { get; set; } = string.Empty;
        public string Unite { get; set; } = string.Empty;
        public decimal SeuilMinimum { get; set; }
        public string CategorieNom { get; set; } = string.Empty;
        public decimal StockTotal { get; set; }
        public decimal QuantiteManquante { get; set; }
        public decimal PrixUnitaireMoyen { get; set; }
        public decimal ValeurAReapprovisionner { get; set; }
        public string TypeAlerte { get; set; } = string.Empty;
        public int? FournisseurId { get; set; }
        public string? FournisseurPreferentiel { get; set; }
        public string? TelFournisseur { get; set; }
        public string? EmailFournisseur { get; set; }
    }

    // ============================================================
    // RESPONSE DTOs — FIFO / Détail inventaire (Stock_LotEntrees)
    // ============================================================

    /// <summary>
    /// Un lot d'entrée dans la file FIFO d'un article/dépôt.
    /// Les lots sont triés par DateCreation ASC (le plus ancien en premier).
    /// </summary>
    public class LotEntreeDTO
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string ArticleNom { get; set; } = string.Empty;
        public string ArticleReference { get; set; } = string.Empty;
        public int DepotId { get; set; }
        public string DepotNom { get; set; } = string.Empty;
        public int? MouvementEntreeId { get; set; }
        /// <summary>Quantité initiale du lot à l'entrée</summary>
        public decimal QuantiteEntree { get; set; }
        /// <summary>Quantité encore disponible dans ce lot (= 0 si entièrement consommé)</summary>
        public decimal QuantiteRestante { get; set; }
        /// <summary>Quantité déjà sortie de ce lot (= QuantiteEntree - QuantiteRestante)</summary>
        public decimal QuantiteConsommee => QuantiteEntree - QuantiteRestante;
        /// <summary>Pourcentage déjà consommé</summary>
        public decimal PourcentageConsomme =>
            QuantiteEntree > 0 ? Math.Round((QuantiteConsommee / QuantiteEntree) * 100, 1) : 0;
        public decimal? PrixUnitaire { get; set; }
        /// <summary>Valeur restante du lot (QuantiteRestante × PrixUnitaire)</summary>
        public decimal? ValeurRestante =>
            PrixUnitaire.HasValue ? Math.Round(QuantiteRestante * PrixUnitaire.Value, 2) : null;
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public DateTime DateCreation { get; set; }
        /// <summary>Rang FIFO : 1 = lot le plus ancien (prochain à sortir)</summary>
        public int RangFifo { get; set; }
    }

    /// <summary>
    /// Réponse de GetDetailsInventaireAsync :
    /// liste des lots FIFO actifs + synthèse de l'inventaire.
    /// </summary>
    public class DetailsInventaireDTO
    {
        public int ArticleId { get; set; }
        public string ArticleNom { get; set; } = string.Empty;
        public string ArticleReference { get; set; } = string.Empty;
        public string Unite { get; set; } = string.Empty;
        public int DepotId { get; set; }
        public string DepotNom { get; set; } = string.Empty;
        /// <summary>Stock total dans Stock_Inventaire (source de vérité)</summary>
        public decimal QuantiteDisponible { get; set; }
        /// <summary>Somme des QuantiteRestante dans Stock_LotEntrees (doit = QuantiteDisponible)</summary>
        public decimal QuantiteTotaleLots { get; set; }
        /// <summary>Écart éventuel si les lots ne couvrent pas tout le stock (données antérieures au FIFO)</summary>
        public decimal EcartNonTrace => QuantiteDisponible - QuantiteTotaleLots;
        /// <summary>Valeur totale estimée selon prix de chaque lot</summary>
        public decimal? ValeurTotaleEstimee { get; set; }
        /// <summary>Lots triés FIFO (DateCreation ASC), inclut uniquement les lots avec QuantiteRestante > 0</summary>
        public List<LotEntreeDTO> Lots { get; set; } = new();
    }

    /// <summary>
    /// Détail de la consommation d'un lot lors d'une sortie FIFO.
    /// Une sortie peut consommer plusieurs lots successifs.
    /// </summary>
    public class SortieConsommationDTO
    {
        public int LotId { get; set; }
        public DateTime DateEntreeLot { get; set; }
        public decimal QuantiteConsommee { get; set; }
        public decimal? PrixUnitaireLot { get; set; }
        public decimal QuantiteRestanteApres { get; set; }
    }

    /// <summary>
    /// Réponse enrichie de EnregistrerSortieAsync avec détail FIFO.
    /// </summary>
    public class SortieStockResultDTO
    {
        public int MouvementId { get; set; }
        public decimal QuantiteAvant { get; set; }
        public decimal QuantiteApres { get; set; }
        /// <summary>Liste des lots consommés dans l'ordre FIFO</summary>
        public List<SortieConsommationDTO> LotsConsommes { get; set; } = new();
        /// <summary>Coût moyen pondéré de la sortie selon les lots FIFO</summary>
        public decimal? CoutMoyenPondere =>
            LotsConsommes.Any(l => l.PrixUnitaireLot.HasValue)
                ? Math.Round(
                    LotsConsommes.Where(l => l.PrixUnitaireLot.HasValue)
                        .Sum(l => l.QuantiteConsommee * l.PrixUnitaireLot!.Value)
                    / LotsConsommes.Sum(l => l.QuantiteConsommee), 4)
                : null;
        public string Message { get; set; } = string.Empty;
    }

    // ============================================================
    // RESPONSE DTOs — VUE v_Stock_HistoriqueMouvements
    // ============================================================

    public class HistoriqueMouvementDTO
    {
        public int MouvementId { get; set; }
        public DateTime DateMouvement { get; set; }
        public string TypeMouvement { get; set; } = string.Empty;
        public int ArticleId { get; set; }
        public string ArticleReference { get; set; } = string.Empty;
        public string ArticleNom { get; set; } = string.Empty;
        public string Unite { get; set; } = string.Empty;
        public string CategorieNom { get; set; } = string.Empty;
        public int DepotId { get; set; }
        public string DepotNom { get; set; } = string.Empty;
        public string? DepotDestinationNom { get; set; }
        public decimal Quantite { get; set; }
        public decimal QuantiteAvant { get; set; }
        public decimal QuantiteApres { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public decimal? MontantTotal { get; set; }
        public string? ReferenceMouvement { get; set; }
        public int? DemandeId { get; set; }
        public string? NumeroDemande { get; set; }
        public int? ProjetId { get; set; }
        public string? ProjetNom { get; set; }
        public string? ProjetNumero { get; set; }
        public int? EtapeId { get; set; }
        public string? EtapeNom { get; set; }
        public string OperateurNom { get; set; } = string.Empty;
        public string? MotifSortie { get; set; }
        public string? Notes { get; set; }
        public decimal Entree { get; set; }
        public decimal Sortie { get; set; }
    }

    // ============================================================
    // RESPONSE DTOs — VUE v_Stock_DemandesParProjet
    // ============================================================

    public class DemandeParProjetDTO
    {
        public int DemandeId { get; set; }
        public string Numero { get; set; } = string.Empty;
        public string Statut { get; set; } = string.Empty;
        public string TypeDestination { get; set; } = string.Empty;
        public string NomDemandeur { get; set; } = string.Empty;
        public string PosteDemandeur { get; set; } = string.Empty;
        public DateTime DateDemande { get; set; }
        public decimal MontantTotal { get; set; }
        public DateTime? DateValidation { get; set; }
        public DateTime? DateLivraisonPrevue { get; set; }
        public DateTime? DateLivraisonReelle { get; set; }
        public int? ProjetId { get; set; }
        public string? ProjetNumero { get; set; }
        public string? ProjetNom { get; set; }
        public int? EtapeId { get; set; }
        public string? EtapeNom { get; set; }
        public int NombreArticles { get; set; }
        public decimal TotalQteDemandee { get; set; }
        public decimal MontantLignes { get; set; }
        public int DureeTraitement { get; set; }
        public string? ValidateurNom { get; set; }
        public string? NumeroDevis { get; set; }
        public decimal? MontantDevisHT { get; set; }
        public string? FournisseurNom { get; set; }
    }

    // ============================================================
    // RESPONSE DTOs — VUE v_Stock_RapportFournisseurs
    // ============================================================

    public class RapportFournisseurDTO
    {
        public int FournisseurId { get; set; }
        public string FournisseurCode { get; set; } = string.Empty;
        public string FournisseurNom { get; set; } = string.Empty;
        public string? Telephone { get; set; }
        public string? Email { get; set; }
        public string? Ville { get; set; }
        public int NoteEvaluation { get; set; }
        public int NombreCommandes { get; set; }
        public decimal MontantTotalHT { get; set; }
        public decimal MontantMoyenCommande { get; set; }
        public DateTime? DerniereCommande { get; set; }
        public int NombreArticlesPreferentiels { get; set; }
    }

    // ============================================================
    // RESPONSE DTOs — DEMANDE DÉTAIL
    // ============================================================

    public class StockDemandeDetailDTO
    {
        public int Id { get; set; }
        public string Numero { get; set; } = string.Empty;
        public string NomDemandeur { get; set; } = string.Empty;
        public string PosteDemandeur { get; set; } = string.Empty;
        public string TypeDestination { get; set; } = string.Empty;
        public int? ProjetId { get; set; }
        public string? ProjetNom { get; set; }
        public string? ProjetNumero { get; set; }
        public int? EtapeProjetId { get; set; }
        public string? EtapeNom { get; set; }
        public string Statut { get; set; } = string.Empty;
        public string? MotifDemande { get; set; }
        public decimal MontantTotal { get; set; }
        public string? NotesTraitement { get; set; }
        public string? NotesValidation { get; set; }
        public string? ValidateurNom { get; set; }
        public DateTime DateDemande { get; set; }
        public DateTime? DateDebutTraitement { get; set; }
        public DateTime? DateValidation { get; set; }
        public DateTime? DateLivraisonPrevue { get; set; }
        public DateTime? DateLivraisonReelle { get; set; }
        /// <summary>Utilisateur demandeur (lien fort)</summary>
        public int? DemandeurId { get; set; }
        /// <summary>Dépôt de destination de la demande</summary>
        public int? DepotDemandeId { get; set; }
        public string? DepotDemandeNom { get; set; }
        public string? DepotDemandeCode { get; set; }
        public List<StockDemandeArticleDetailDTO> Articles { get; set; } = new();
        public StockTraitementDetailDTO? Traitement { get; set; }
        /// <summary>Mouvements de stock liés à cette demande</summary>
        public List<MouvementDemandeDTO> Mouvements { get; set; } = new();
        public int NbArticlesLivres { get; set; }
        public int NbArticlesTotal { get; set; }
    }

    // ============================================================
    // RESPONSE DTO — CONFIRMATION LIVRAISON ARTICLE (fournisseur)
    // ============================================================

    /// <summary>
    /// Résultat de ConfirmerLivraisonArticleAsync.
    /// Contient le statut mis à jour et les Ids des 3 mouvements créés
    /// (entrée dépôt par défaut, sortie dépôt par défaut, entrée dépôt demande).
    /// Les mouvements sont null si la demande n'a pas de DepotDemandeId
    /// ou si l'article est hors-catalogue (pas de référence catalogue).
    /// </summary>
    public class ConfirmerLivraisonArticleResultDTO
    {
        public bool TousLivres { get; set; }
        public string NouveauStatut { get; set; } = string.Empty;
        /// <summary>M1 : Entrée dans le dépôt par défaut (réception fournisseur)</summary>
        public int? MouvementEntreeDepotDefautId { get; set; }
        /// <summary>M2 : Sortie du dépôt par défaut → dépôt demande</summary>
        public int? MouvementSortieDepotDefautId { get; set; }
        /// <summary>M3 : Entrée dans le dépôt demande (article disponible pour le demandeur)</summary>
        public int? MouvementEntreeDepotDemandeId { get; set; }
        public int? DepotParDefautId { get; set; }
        public string? DepotParDefautNom { get; set; }
        public string? Message { get; set; }

        // ── Catalogage automatique des articles hors-catalogue ──────────────
        /// <summary>
        /// Article créé ou réutilisé lors du catalogage automatique.
        /// Null si l'article était déjà dans le catalogue ou si la ligne n'avait pas de DesignationLibre.
        /// </summary>
        public ArticleCatalogueResultDTO? ArticleCatalogue { get; set; }

        /// <summary>
        /// Avertissements non bloquants (ex : doublon détecté, catégorie DIVERS absente…).
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Résultat du catalogage automatique d'un article hors-catalogue.
    /// </summary>
    public class ArticleCatalogueResultDTO
    {
        public int ArticleId { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        /// <summary>true = article créé maintenant / false = article existant réutilisé</summary>
        public bool EstNouvel { get; set; }
    }

    /// <summary>Mouvement simplifié pour l'affichage dans le détail d'une demande</summary>
    public class MouvementDemandeDTO
    {
        public int Id { get; set; }
        public DateTime DateMouvement { get; set; }
        public string TypeMouvement { get; set; } = string.Empty;
        public int ArticleId { get; set; }
        public string ArticleNom { get; set; } = string.Empty;
        public string ArticleReference { get; set; } = string.Empty;
        public string Unite { get; set; } = string.Empty;
        public int DepotId { get; set; }
        public string DepotNom { get; set; } = string.Empty;
        public decimal Quantite { get; set; }
        public decimal QuantiteAvant { get; set; }
        public decimal QuantiteApres { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public string? MotifSortie { get; set; }
        public string OperateurNom { get; set; } = string.Empty;
        public string? Notes { get; set; }
        /// <summary>Id de la dotation (Stock_DemandeArticleDotations) si livraison directe</summary>
        public int? DotationId { get; set; }
    }

    public class StockDemandeArticleDetailDTO
    {
        public int Id { get; set; }
        public int? ArticleId { get; set; }
        public string? ArticleReference { get; set; }
        public string ArticleNom { get; set; } = string.Empty;
        public string? ArticleDescription { get; set; }

        public string Unite { get; set; } = string.Empty;
        public decimal QuantiteDemandee { get; set; }
        public decimal? QuantiteValidee { get; set; }
        public decimal? PrixUnitaireDevis { get; set; }
        public decimal? PrixTotalLigne { get; set; }
        public string? Notes { get; set; }

        // ── Traitement partiel ──────────────────────────────────
        /// <summary>"Stock" | "Commande" | "CommandeReste"</summary>
        public string Source { get; set; } = "Commande";
        public int? DepotDotationId { get; set; }
        public string? DepotDotationNom { get; set; }
        public decimal? QuantiteDotee { get; set; }

        // ── Livraison ───────────────────────────────────────────
        public bool EstLivre { get; set; } = false;
        public int? UserValidationLivraisonId { get; set; }
        public string? UserValidationLivraisonNom { get; set; }
        public DateTime? DateLivraisonConfirmee { get; set; }

        /// <summary>Sous-lignes dotations multi-dépôts (Source = "Stock")</summary>
        public List<StockDemandeArticleDotationDTO> Dotations { get; set; } = new();
    }

    public class StockDemandeArticleDotationDTO
    {
        public int Id { get; set; }
        public int DemandeArticleId { get; set; }
        public int DepotId { get; set; }
        public string DepotNom { get; set; } = string.Empty;
        public string DepotCode { get; set; } = string.Empty;
        public decimal QuantiteDotee { get; set; }
        public bool EstLivre { get; set; } = false;
        public string? UserValidationNom { get; set; }
        public DateTime? DateLivraisonConfirmee { get; set; }
        public int? MouvementId { get; set; }
    }

    public class StockTraitementDetailDTO
    {
        public int Id { get; set; }
        public int? FournisseurId { get; set; }
        public string? FournisseurNom { get; set; }
        public string? NomFournisseurLibre { get; set; }
        public string? NumeroDevis { get; set; }
        public decimal? MontantDevisHT { get; set; }
        public decimal? MontantDevisTTC { get; set; }
        public DateTime? DateDevis { get; set; }
        public string? FichierDevisPath { get; set; }
        public string? DelaiLivraison { get; set; }
        public string? ConditionsPaiement { get; set; }
        public string? Notes { get; set; }
        public string StatutTraitement { get; set; } = string.Empty;
        public string? TraiteParNom { get; set; }
        public DateTime DateModification { get; set; }
    }

    // ============================================================
    // RESPONSE DTOs — STATISTIQUES TABLEAU DE BORD
    // ============================================================

    public class StockStatistiquesDTO
    {
        public int TotalArticles { get; set; }
        public int TotalDepots { get; set; }
        public decimal ValeurTotaleStock { get; set; }
        public int ArticlesEnAlerte { get; set; }
        public int ArticlesEnRupture { get; set; }
        public int DemandesEnAttente { get; set; }
        public int DemandesEnTraitement { get; set; }
        public int DemandesAttenteValidation { get; set; }
        /// <summary>Demandes en cours de livraison (confirmation article par article)</summary>
        public int DemandesAttenteLivraison { get; set; }
        /// <summary>Demandes partiellement livrées (stock doté, reliquat en attente fournisseur)</summary>
        public int DemandesLivraisonPartielle { get; set; }
        /// <summary>Demandes entièrement dotées depuis le stock (flux terminé)</summary>
        public int DemandesDotees { get; set; }
        public int DemandesAttenteComptabilite { get; set; }
        public int MouvementsDuMois { get; set; }
    }

    // ============================================================
    // REQUEST DTOs — BORDEREAU D'ENTRÉE / SORTIE
    // ============================================================

    /// <summary>
    /// Bordereau d'entrée : plusieurs articles en une seule requête.
    /// Une seule transaction — tout passe ou tout échoue.
    /// </summary>
    public class BordereauEntreeRequest
    {
        public int DepotId { get; set; }
        /// <summary>N° de BL, de bon de commande, ou référence libre</summary>
        public string? Reference { get; set; }
        public int? FournisseurId { get; set; }
        public string? Notes { get; set; }
        public List<BordereauLigneEntreeRequest> Lignes { get; set; } = new();
    }

    public class BordereauLigneEntreeRequest
    {
        public int ArticleId { get; set; }
        public decimal Quantite { get; set; }
        public decimal? PrixUnitaire { get; set; }
        /// <summary>Note spécifique à cette ligne (état, lot fournisseur…)</summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Bordereau de sortie : plusieurs articles en une seule requête.
    /// Traitement ligne par ligne — permet le succès partiel.
    /// Chaque ligne applique la logique FIFO existante.
    /// </summary>
    public class BordereauSortieRequest
    {
        public int DepotId { get; set; }
        public string? Reference { get; set; }
        public int? ProjetId { get; set; }
        public int? EtapeProjetId { get; set; }
        public string? MotifSortie { get; set; }
        public string? Notes { get; set; }
        public List<BordereauLigneSortieRequest> Lignes { get; set; } = new();
    }

    public class BordereauLigneSortieRequest
    {
        public int ArticleId { get; set; }
        public decimal Quantite { get; set; }
        public string? Notes { get; set; }
    }

    // ── RESPONSE DTOs ─────────────────────────────────────────────

    public class BordereauLigneResultDTO
    {
        public int ArticleId { get; set; }
        public string ArticleNom { get; set; } = string.Empty;
        public string ArticleReference { get; set; } = string.Empty;
        public decimal Quantite { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public int? MouvementId { get; set; }
        public decimal? QuantiteAvant { get; set; }
        public decimal? QuantiteApres { get; set; }
        public bool Succes { get; set; }
        public string? Erreur { get; set; }
    }

    public class BordereauEntreeResultDTO
    {
        public string? Reference { get; set; }
        public int DepotId { get; set; }
        public string DepotNom { get; set; } = string.Empty;
        public int NbLignesTotal { get; set; }
        public int NbLignesReussies { get; set; }
        public decimal MontantTotalEntre { get; set; }
        public List<BordereauLigneResultDTO> Lignes { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class BordereauSortieResultDTO
    {
        public string? Reference { get; set; }
        public int DepotId { get; set; }
        public string DepotNom { get; set; } = string.Empty;
        public int NbLignesTotal { get; set; }
        public int NbLignesReussies { get; set; }
        public int NbLignesEchec { get; set; }
        public List<BordereauLigneResultDTO> Lignes { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}