using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Claims;

namespace Saf_alu_ci_Api.Controllers.Stock
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StockController : BaseController
    {
        private readonly StockService _stockService;

        public StockController(StockService stockService)
        {
            _stockService = stockService;
        }

        // ============================================================
        // TABLEAU DE BORD
        // ============================================================

        /// <summary>GET /api/stock/statistiques</summary>
        [HttpGet("statistiques")]
        public async Task<IActionResult> GetStatistiques()
        {
            try { return Ok(await _stockService.GetStatistiquesAsync()); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        // ============================================================
        // CATÉGORIES — liste complète + liste paginée
        // ============================================================

        /// <summary>
        /// GET /api/stock/categories
        /// Liste complète (sans pagination) — rétrocompatibilité
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try { return Ok(await _stockService.GetAllCategoriesAsync()); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// GET /api/stock/categories/paged?search=gros&page=1&pageSize=20
        /// Liste paginée avec recherche sur Code et Nom
        /// </summary>
        [HttpGet("categories/paged")]
        public async Task<IActionResult> GetCategoriesPaged(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _stockService.GetCategoriesPagedAsync(new CategorieSearchParams
                {
                    Search = search,
                    Page = page,
                    PageSize = pageSize
                });
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>GET /api/stock/categories/{id}</summary>
        [HttpGet("categories/{id}")]
        public async Task<IActionResult> GetCategorie(int id)
        {
            try
            {
                var item = await _stockService.GetCategorieByIdAsync(id);
                if (item == null) return NotFound("Catégorie non trouvée");
                return Ok(item);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>POST /api/stock/categories</summary>
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategorie([FromBody] CreateStockCategorieRequest model)
        {
            try
            {
                var id = await _stockService.CreateCategorieAsync(model);
                return CreatedAtAction(nameof(GetCategorie), new { id }, new { id, message = "Catégorie créée avec succès" });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>PUT /api/stock/categories/{id}</summary>
        [HttpPut("categories/{id}")]
        public async Task<IActionResult> UpdateCategorie(int id, [FromBody] UpdateStockCategorieRequest model)
        {
            try
            {
                await _stockService.UpdateCategorieAsync(id, model);
                return Ok(new { message = "Catégorie mise à jour" });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        // ============================================================
        // DÉPÔTS — liste complète + liste paginée
        // ============================================================

        /// <summary>GET /api/stock/depots</summary>
        [HttpGet("depots")]
        public async Task<IActionResult> GetDepots()
        {
            try { return Ok(await _stockService.GetAllDepotsAsync()); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// GET /api/stock/depots/paged?search=central&page=1&pageSize=20
        /// Liste paginée avec recherche sur Code, Nom, Ville
        /// </summary>
        [HttpGet("depots/paged")]
        public async Task<IActionResult> GetDepotsPaged(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _stockService.GetDepotsPagedAsync(new DepotSearchParams
                {
                    Search = search,
                    Page = page,
                    PageSize = pageSize
                });
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>GET /api/stock/depots/{id}</summary>
        [HttpGet("depots/{id}")]
        public async Task<IActionResult> GetDepot(int id)
        {
            try
            {
                var item = await _stockService.GetDepotByIdAsync(id);
                if (item == null) return NotFound("Dépôt non trouvé");
                return Ok(item);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>POST /api/stock/depots</summary>
        [HttpPost("depots")]
        public async Task<IActionResult> CreateDepot([FromBody] CreateStockDepotRequest model)
        {
            try
            {
                var id = await _stockService.CreateDepotAsync(model);
                return CreatedAtAction(nameof(GetDepot), new { id }, new { id, message = "Dépôt créé avec succès" });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>PUT /api/stock/depots/{id}</summary>
        [HttpPut("depots/{id}")]
        public async Task<IActionResult> UpdateDepot(int id, [FromBody] UpdateStockDepotRequest model)
        {
            try
            {
                await _stockService.UpdateDepotAsync(id, model);
                return Ok(new { message = "Dépôt mis à jour" });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        // ============================================================
        // FOURNISSEURS — liste complète + liste paginée
        // ============================================================

        /// <summary>GET /api/stock/fournisseurs</summary>
        [HttpGet("fournisseurs")]
        public async Task<IActionResult> GetFournisseurs()
        {
            try { return Ok(await _stockService.GetAllFournisseursAsync()); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// GET /api/stock/fournisseurs/paged?search=bati&page=1&pageSize=20
        /// Liste paginée avec recherche sur Nom, Code, Ville, Email, Contact
        /// </summary>
        [HttpGet("fournisseurs/paged")]
        public async Task<IActionResult> GetFournisseursPaged(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _stockService.GetFournisseursPagedAsync(new FournisseurSearchParams
                {
                    Search = search,
                    Page = page,
                    PageSize = pageSize
                });
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>GET /api/stock/fournisseurs/{id}</summary>
        [HttpGet("fournisseurs/{id}")]
        public async Task<IActionResult> GetFournisseur(int id)
        {
            try
            {
                var item = await _stockService.GetFournisseurByIdAsync(id);
                if (item == null) return NotFound("Fournisseur non trouvé");
                return Ok(item);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>POST /api/stock/fournisseurs</summary>
        [HttpPost("fournisseurs")]
        public async Task<IActionResult> CreateFournisseur([FromBody] CreateStockFournisseurRequest model)
        {
            try
            {
                var id = await _stockService.CreateFournisseurAsync(model);
                return CreatedAtAction(nameof(GetFournisseur), new { id }, new { id, message = "Fournisseur créé avec succès" });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>PUT /api/stock/fournisseurs/{id}</summary>
        [HttpPut("fournisseurs/{id}")]
        public async Task<IActionResult> UpdateFournisseur(int id, [FromBody] UpdateStockFournisseurRequest model)
        {
            try
            {
                await _stockService.UpdateFournisseurAsync(id, model);
                return Ok(new { message = "Fournisseur mis à jour" });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        // ============================================================
        // ARTICLES — liste complète + liste paginée avec recherche désignation
        // ============================================================

        /// <summary>GET /api/stock/articles?search=ciment&categorieId=1</summary>
        [HttpGet("articles")]
        public async Task<IActionResult> GetArticles([FromQuery] string? search, [FromQuery] int? categorieId)
        {
            try { return Ok(await _stockService.GetAllArticlesAsync(search, categorieId)); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// GET /api/stock/articles/paged?search=ciment&categorieId=1&fournisseurId=2&page=1&pageSize=20
        /// Liste paginée — recherche sur Nom, Référence, Description (désignation)
        /// Réponse : PagedResult contenant Items, TotalItems, Page, PageSize, TotalPages
        /// </summary>
        [HttpGet("articles/paged")]
        public async Task<IActionResult> GetArticlesPaged(
            [FromQuery] string? search,
            [FromQuery] int? categorieId,
            [FromQuery] int? fournisseurId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _stockService.GetArticlesPagedAsync(new ArticleSearchParams
                {
                    Search = search,
                    CategorieId = categorieId,
                    FournisseurId = fournisseurId,
                    Page = page,
                    PageSize = pageSize
                });
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>GET /api/stock/articles/{id}</summary>
        [HttpGet("articles/{id}")]
        public async Task<IActionResult> GetArticle(int id)
        {
            try
            {
                var item = await _stockService.GetArticleByIdAsync(id);
                if (item == null) return NotFound("Article non trouvé");
                return Ok(item);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>POST /api/stock/articles</summary>
        [HttpPost("articles")]
        public async Task<IActionResult> CreateArticle([FromBody] CreateStockArticleRequest model)
        {
            try
            {
                var id = await _stockService.CreateArticleAsync(model);
                return CreatedAtAction(nameof(GetArticle), new { id }, new { id, message = "Article créé avec succès" });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>PUT /api/stock/articles/{id}</summary>
        [HttpPut("articles/{id}")]
        public async Task<IActionResult> UpdateArticle(int id, [FromBody] UpdateStockArticleRequest model)
        {
            try
            {
                await _stockService.UpdateArticleAsync(id, model);
                return Ok(new { message = "Article mis à jour" });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        // ============================================================
        // INVENTAIRE / ÉTAT DES STOCKS — liste complète + paginée
        // ============================================================

        /// <summary>GET /api/stock/inventaire?depotId=1&alertesSeulement=false</summary>
        [HttpGet("inventaire")]
        public async Task<IActionResult> GetEtatStocks([FromQuery] int? depotId, [FromQuery] bool alertesSeulement = false)
        {
            try { return Ok(await _stockService.GetEtatStocksAsync(depotId, alertesSeulement)); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// GET /api/stock/inventaire/paged?search=ciment&depotId=1&categorieId=2&alertesSeulement=false&page=1&pageSize=50
        /// Inventaire paginé — recherche sur ArticleNom, Référence, CategorieNom, DepotNom
        /// </summary>
        [HttpGet("inventaire/paged")]
        public async Task<IActionResult> GetEtatStocksPaged(
            [FromQuery] string? search,
            [FromQuery] int? depotId,
            [FromQuery] int? categorieId,
            [FromQuery] bool alertesSeulement = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var result = await _stockService.GetEtatStocksPagedAsync(new EtatStockSearchParams
                {
                    Search = search,
                    DepotId = depotId,
                    CategorieId = categorieId,
                    AlertesSeulement = alertesSeulement,
                    Page = page,
                    PageSize = pageSize
                });
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>GET /api/stock/alertes</summary>
        [HttpGet("alertes")]
        public async Task<IActionResult> GetAlertes()
        {
            try { return Ok(await _stockService.GetAlertesStockAsync()); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// GET /api/stock/inventaire/details?articleId=5&amp;depotId=2
        /// Retourne le détail FIFO de l'inventaire pour un article dans un dépôt :
        /// liste des lots d'entrée disponibles (QuantiteRestante > 0), triés par date ASC.
        /// Le lot en rang 1 est le prochain à sortir.
        /// Inclut la synthèse : stock total, total des lots tracés, écart non tracé, valeur estimée.
        /// </summary>
        [HttpGet("inventaire/details")]
        public async Task<IActionResult> GetDetailsInventaire(
            [FromQuery] int articleId,
            [FromQuery] int depotId)
        {
            try
            {
                if (articleId <= 0 || depotId <= 0)
                    return BadRequest("articleId et depotId sont obligatoires.");

                var result = await _stockService.GetDetailsInventaireAsync(articleId, depotId);
                if (result == null)
                    return NotFound($"Aucun article Id={articleId} ou dépôt Id={depotId} trouvé.");

                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        // ============================================================
        // DEMANDES — liste complète + liste paginée avec recherche désignation
        // ============================================================

        /// <summary>GET /api/stock/demandes?statut=EnAttente&projetId=5</summary>
        [HttpGet("demandes")]
        public async Task<IActionResult> GetDemandes([FromQuery] string? statut, [FromQuery] int? projetId)
        {
            try { return Ok(await _stockService.GetAllDemandesAsync(statut, projetId)); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// GET /api/stock/demandes/paged?search=ciment&statut=EnAttente&projetId=5&dateDebut=2024-01-01&dateFin=2024-12-31&page=1&pageSize=20
        /// Liste paginée — recherche sur N° demande, nom demandeur, désignation article (catalogue ET libre)
        /// </summary>
        [HttpGet("demandes/paged")]
        public async Task<IActionResult> GetDemandesPaged(
            [FromQuery] string? search,
            [FromQuery] string? statut,
            [FromQuery] int? projetId,
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _stockService.GetDemandesPagedAsync(new DemandeSearchParams
                {
                    Search = search,
                    Statut = statut,
                    ProjetId = projetId,
                    DateDebut = dateDebut,
                    DateFin = dateFin,
                    Page = page,
                    PageSize = pageSize
                });
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>GET /api/stock/demandes/{id}</summary>
        [HttpGet("demandes/{id}")]
        public async Task<IActionResult> GetDemande(int id)
        {
            try
            {
                var item = await _stockService.GetDemandeByIdAsync(id);
                if (item == null) return NotFound("Demande non trouvée");
                return Ok(item);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>POST /api/stock/demandes</summary>
        [HttpPost("demandes")]
        public async Task<IActionResult> CreateDemande([FromBody] CreateStockDemandeRequest model)
        {
            try
            {
                if (model.Articles == null || !model.Articles.Any())
                    return BadRequest("La demande doit contenir au moins un article");
                if (model.TypeDestination == "Projet" && !model.ProjetId.HasValue)
                    return BadRequest("Un projet doit être sélectionné pour une demande de type Projet");
                var id = await _stockService.CreateDemandeAsync(model);
                return CreatedAtAction(nameof(GetDemande), new { id }, new
                {
                    id,
                    message = "Demande soumise avec succès. Statut : En attente de traitement"
                });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>PUT /api/stock/demandes/{id}/traitement/sauvegarder</summary>
        [HttpPut("demandes/{id}/traitement/sauvegarder")]
        public async Task<IActionResult> SauvegarderTraitement(int id, [FromBody] SauvegarderTraitementRequest model)
        {
            try
            {
                model.TraitePar = GetCurrentUserId();
                var demande = await _stockService.GetDemandeByIdAsync(id);
                if (demande == null) return NotFound("Demande non trouvée");
                if (demande.Statut != "EnAttente" && demande.Statut != "EnTraitement")
                    return BadRequest($"La demande ne peut pas être traitée dans l'état '{demande.Statut}'");
                var nouveauStatut = await _stockService.SauvegarderTraitementAsync(id, model);
                return Ok(new { message = "Traitement sauvegardé (brouillon). Vous pouvez reprendre à tout moment.", statut = nouveauStatut });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>PUT /api/stock/demandes/{id}/traitement/soumettre</summary>
        [HttpPut("demandes/{id}/traitement/soumettre")]
        public async Task<IActionResult> SoumettreTraitement(int id, [FromBody] SoumettreTraitementRequest model)
        {
            try
            {
                model.TraitePar = GetCurrentUserId();
                var demande = await _stockService.GetDemandeByIdAsync(id);
                if (demande == null) return NotFound("Demande non trouvée");
                if (demande.Statut != "EnAttente" && demande.Statut != "EnTraitement" && demande.Statut != "LivraisonPartielle")
                    return BadRequest($"La demande ne peut pas être soumise depuis l'état '{demande.Statut}'");

                // Fournisseur obligatoire si au moins un article en commande (direct ou reliquat)
                bool aArticlesCommande = model.articlesValides.Any(a =>
                    a.Source == "Commande" || a.Source == "CommandeReste");
                if (aArticlesCommande && !model.FournisseurId.HasValue && string.IsNullOrEmpty(model.NomFournisseurLibre))
                    return BadRequest("Un fournisseur doit être renseigné pour les articles en commande.");

                var articlesStockSansDepot = model.articlesValides
                    .Where(a => a.Source == "Stock" && !a.DepotDotationId.HasValue)
                    .ToList();
                if (articlesStockSansDepot.Any())
                    return BadRequest($"{articlesStockSansDepot.Count} article(s) marqué(s) 'Stock' n'ont pas de dépôt source.");

                var nouveauStatut = await _stockService.SoumettreTraitementAsync(id, model);

                // Message adapté selon le cas déterminé par RecalculerStatutDemandeAsync
                var message = nouveauStatut switch
                {
                    "Dotee" => "Tous les articles ont été dotés depuis le stock. Demande clôturée.",
                    "LivraisonPartielle" => "Dotation partielle effectuée. Les articles restants partent en validation fournisseur.",
                    "AttenteValidation" => "Traitement soumis. Demande en attente de validation.",
                    _ => $"Traitement soumis (statut : {nouveauStatut})."
                };

                return Ok(new { message, statut = nouveauStatut });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>PUT /api/stock/demandes/{id}/valider</summary>
        [HttpPut("demandes/{id}/valider")]
        public async Task<IActionResult> ValiderDemande(int id, [FromBody] ValiderDemandeRequest model)
        {
            try
            {
                model.ValidateurId = GetCurrentUserId();
                var demande = await _stockService.GetDemandeByIdAsync(id);
                if (demande == null) return NotFound("Demande non trouvée");
                // Valider est possible depuis AttenteValidation ou LivraisonPartielle (Cas 2)
                if (demande.Statut != "AttenteValidation" && demande.Statut != "LivraisonPartielle")
                    return BadRequest($"La demande doit être en 'AttenteValidation' ou 'LivraisonPartielle'. Statut actuel : {demande.Statut}");
                await _stockService.ValiderDemandeAsync(id, model);
                var demandeUpdated = await _stockService.GetDemandeByIdAsync(id);
                var msg = demandeUpdated?.Statut switch
                {
                    "AttenteLivraison" => "Demande validée. En attente de réception des articles commandés.",
                    "AttenteComptabilite" => "Demande validée. Passée en attente de comptabilité.",
                    _ => "Demande validée."
                };
                return Ok(new { message = msg, statut = demandeUpdated?.Statut });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>PUT /api/stock/demandes/{id}/rejeter</summary>
        [HttpPut("demandes/{id}/rejeter")]
        public async Task<IActionResult> RejeterDemande(int id, [FromBody] RejeterDemandeRequest model)
        {
            try
            {
                model.ValidateurId = GetCurrentUserId();
                var demande = await _stockService.GetDemandeByIdAsync(id);
                if (demande == null) return NotFound("Demande non trouvée");
                if (demande.Statut != "AttenteValidation")
                    return BadRequest("La demande doit être en 'AttenteValidation' pour être rejetée");
                if (string.IsNullOrWhiteSpace(model.MotifRejet))
                    return BadRequest("Le motif de rejet est obligatoire");
                await _stockService.RejeterDemandeAsync(id, model);
                return Ok(new { message = "Demande rejetée." });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        // ============================================================
        // LIVRAISON — Confirmation article par article
        // ============================================================

        /// <summary>
        /// POST /api/stock/demandes/{id}/livraison/articles/{articleId}/confirmer
        /// Confirme la réception d'un article de commande.
        /// L'utilisateur connecté est automatiquement enregistré comme validateur.
        /// Si tous les articles sont désormais livrés, la demande passe à AttenteComptabilite.
        /// </summary>
        [HttpPost("demandes/{id}/livraison/articles/{articleId}/confirmer")]
        public async Task<IActionResult> ConfirmerLivraisonArticle(int id, int articleId)
        {
            try
            {
                var demande = await _stockService.GetDemandeByIdAsync(id);
                if (demande == null) return NotFound("Demande non trouvée");
                if (demande.Statut != "AttenteLivraison")
                    return BadRequest($"La demande n'est pas en attente de livraison. Statut actuel : {demande.Statut}");

                var userId = GetCurrentUserId();
                var result = await _stockService.ConfirmerLivraisonArticleAsync(articleId, userId);
                var tousLivres = result.TousLivres;
                var nouveauStatut = result.NouveauStatut;

                return Ok(new
                {
                    message = tousLivres
                        ? "Article confirmé. Tous les articles sont livrés — demande passée en attente de comptabilité."
                        : "Réception de l'article confirmée.",
                    tousLivres,
                    nouveauStatut
                });
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// POST /api/stock/demandes/{id}/livraison/confirmer-tout
        /// Confirme la réception de tous les articles en commande non encore livrés.
        /// Passe automatiquement la demande à AttenteComptabilite.
        /// </summary>
        [HttpPost("demandes/{id}/livraison/confirmer-tout")]
        public async Task<IActionResult> ConfirmerToutesLivraisons(int id)
        {
            try
            {
                var demande = await _stockService.GetDemandeByIdAsync(id);
                if (demande == null) return NotFound("Demande non trouvée");
                if (demande.Statut != "AttenteLivraison")
                    return BadRequest($"La demande n'est pas en attente de livraison. Statut actuel : {demande.Statut}");

                var userId = GetCurrentUserId();
                await _stockService.ConfirmerToutesLivraisonsAsync(id, userId);

                return Ok(new
                {
                    message = "Toutes les réceptions confirmées. Demande passée en attente de comptabilité.",
                    nouveauStatut = "AttenteComptabilite"
                });
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// POST /api/stock/demandes/{id}/livraison/finaliser
        /// Finalise la livraison après confirmation manuelle de tous les articles.
        /// Guard côté service : tous les articles doivent être EstLivre=1.
        /// </summary>
        [HttpPost("demandes/{id}/livraison/finaliser")]
        public async Task<IActionResult> FinaliserLivraison(int id)
        {
            try
            {
                var demande = await _stockService.GetDemandeByIdAsync(id);
                if (demande == null) return NotFound("Demande non trouvée");
                if (demande.Statut != "AttenteLivraison" && demande.Statut != "LivraisonPartielle")
                    return BadRequest($"La demande n'est pas en attente de livraison. Statut actuel : {demande.Statut}");

                await _stockService.FinaliserLivraisonAsync(id);

                return Ok(new
                {
                    message = "Livraison finalisée. Demande passée en attente de comptabilité.",
                    nouveauStatut = "AttenteComptabilite"
                });
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// POST /api/stock/demandes/{id}/livraison/direct
        /// Livraison directe — bouton "Livré" du formulaire de traitement.
        /// AUTONOME : crée la dotation dans la même transaction si elle n'existe pas encore.
        /// Aucun appel préalable à SauvegarderTraitement n'est requis.
        /// En phase traitement (EnAttente/EnTraitement) : ne modifie pas le statut principal.
        /// En phase post-validation (AttenteLivraison/LivraisonPartielle) : recalcule le statut.
        /// </summary>
        [HttpPost("demandes/{id}/livraison/direct")]
        public async Task<IActionResult> LivraisonDirecte(int id, [FromBody] LivraisonDirecteRequest model)
        {
            try
            {
                if (model.Quantite <= 0)
                    return BadRequest("La quantité doit être supérieure à zéro.");

                var demande = await _stockService.GetDemandeByIdAsync(id);
                if (demande == null) return NotFound("Demande non trouvée");

                var statutsAutorisés = new[] { "EnAttente", "EnTraitement",
                                               "AttenteLivraison", "LivraisonPartielle" };
                if (!statutsAutorisés.Contains(demande.Statut))
                    return BadRequest($"La livraison directe n'est pas possible pour le statut '{demande.Statut}'.");

                var userId = GetCurrentUserId();
                var result = await _stockService.LivraisonDirecteAsync(
                    id, model.DemandeArticleId, model.DepotId, model.Quantite, userId);

                return Ok(result);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        // ============================================================
        // MOUVEMENTS DE STOCK — liste complète + paginée avec recherche
        // ============================================================

        /// <summary>GET /api/stock/mouvements?dateDebut=&dateFin=&articleId=&depotId=&typeMouvement=</summary>
        [HttpGet("mouvements")]
        public async Task<IActionResult> GetHistorique(
            [FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin,
            [FromQuery] int? articleId, [FromQuery] int? depotId, [FromQuery] string? typeMouvement)
        {
            try { return Ok(await _stockService.GetHistoriqueAsync(dateDebut, dateFin, articleId, depotId, typeMouvement)); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// GET /api/stock/mouvements/paged?search=ciment&dateDebut=&dateFin=&depotId=&typeMouvement=&page=1&pageSize=50
        /// Historique paginé — recherche sur ArticleNom, ArticleReference, NumeroDemande, OperateurNom
        /// </summary>
        [HttpGet("mouvements/paged")]
        public async Task<IActionResult> GetHistoriquePaged(
            [FromQuery] string? search,
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int? articleId,
            [FromQuery] int? depotId,
            [FromQuery] string? typeMouvement,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var result = await _stockService.GetHistoriquePagedAsync(new MouvementSearchParams
                {
                    Search = search,
                    DateDebut = dateDebut,
                    DateFin = dateFin,
                    ArticleId = articleId,
                    DepotId = depotId,
                    TypeMouvement = typeMouvement,
                    Page = page,
                    PageSize = pageSize
                });
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>POST /api/stock/mouvements/entree</summary>
        [HttpPost("mouvements/entree")]
        public async Task<IActionResult> EnregistrerEntree([FromBody] EnregistrerEntreeRequest model)
        {
            try
            {
                if (model.Quantite <= 0) return BadRequest("La quantité doit être supérieure à 0");
                await _stockService.EnregistrerEntreeAsync(model);
                return Ok(new { message = "Entrée de stock enregistrée avec succès" });
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>
        /// POST /api/stock/mouvements/sortie
        /// Applique la logique FIFO : consomme les lots dans l'ordre d'entrée (le plus ancien en premier).
        /// La réponse inclut le détail des lots consommés et le coût moyen pondéré de la sortie.
        /// </summary>
        [HttpPost("mouvements/sortie")]
        public async Task<IActionResult> EnregistrerSortie([FromBody] EnregistrerSortieRequest model)
        {
            try
            {
                if (model.Quantite <= 0) return BadRequest("La quantité doit être supérieure à 0");
                var result = await _stockService.EnregistrerSortieAsync(model);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>POST /api/stock/mouvements/transfert</summary>
        [HttpPost("mouvements/transfert")]
        public async Task<IActionResult> EnregistrerTransfert([FromBody] EnregistrerTransfertRequest model)
        {
            try
            {
                if (model.Quantite <= 0) return BadRequest("La quantité doit être supérieure à 0");
                if (model.DepotSourceId == model.DepotDestinationId)
                    return BadRequest("Le dépôt source et le dépôt destination doivent être différents");
                await _stockService.EnregistrerTransfertAsync(model);
                return Ok(new { message = "Transfert inter-dépôts enregistré avec succès" });
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        // ============================================================
        // RAPPORTS — depuis les vues SQL
        // ============================================================

        /// <summary>GET /api/stock/rapports/demandes-par-projet?projetId=5</summary>
        [HttpGet("rapports/demandes-par-projet")]
        public async Task<IActionResult> GetDemandesParProjet([FromQuery] int? projetId)
        {
            try { return Ok(await _stockService.GetDemandesParProjetAsync(projetId)); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        /// <summary>GET /api/stock/rapports/fournisseurs</summary>
        [HttpGet("rapports/fournisseurs")]
        public async Task<IActionResult> GetRapportFournisseurs()
        {
            try { return Ok(await _stockService.GetRapportFournisseursAsync()); }
            catch (Exception ex) { return StatusCode(500, $"Erreur serveur : {ex.Message}"); }
        }

        // ============================================================
        // EXPORTS — PDF (QuestPDF)
        // ============================================================

        [HttpGet("exports/etat-stocks/pdf")]
        public async Task<IActionResult> ExportEtatStocksPdf([FromQuery] int? depotId)
        {
            try
            {
                var data = await _stockService.GetEtatStocksAsync(depotId);
                var pdfBytes = GenererPdfEtatStocks(data, depotId);
                return File(pdfBytes, "application/pdf", $"etat-stocks-{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur génération PDF : {ex.Message}"); }
        }

        [HttpGet("exports/alertes/pdf")]
        public async Task<IActionResult> ExportAlertesPdf()
        {
            try
            {
                var data = await _stockService.GetAlertesStockAsync();
                var pdfBytes = GenererPdfAlertes(data);
                return File(pdfBytes, "application/pdf", $"alertes-stock-{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur génération PDF : {ex.Message}"); }
        }

        [HttpGet("exports/historique/pdf")]
        public async Task<IActionResult> ExportHistoriquePdf(
            [FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin,
            [FromQuery] int? articleId, [FromQuery] int? depotId)
        {
            try
            {
                var data = await _stockService.GetHistoriqueAsync(dateDebut, dateFin, articleId, depotId);
                var pdfBytes = GenererPdfHistorique(data, dateDebut, dateFin);
                return File(pdfBytes, "application/pdf", $"historique-mouvements-{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur génération PDF : {ex.Message}"); }
        }

        [HttpGet("exports/demandes-par-projet/pdf")]
        public async Task<IActionResult> ExportDemandesParProjetPdf([FromQuery] int? projetId)
        {
            try
            {
                var data = await _stockService.GetDemandesParProjetAsync(projetId);
                var pdfBytes = GenererPdfDemandesParProjet(data);
                return File(pdfBytes, "application/pdf", $"demandes-par-projet-{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur génération PDF : {ex.Message}"); }
        }

        [HttpGet("exports/fournisseurs/pdf")]
        public async Task<IActionResult> ExportFournisseursPdf()
        {
            try
            {
                var data = await _stockService.GetRapportFournisseursAsync();
                var pdfBytes = GenererPdfFournisseurs(data);
                return File(pdfBytes, "application/pdf", $"rapport-fournisseurs-{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur génération PDF : {ex.Message}"); }
        }

        // ============================================================
        // EXPORTS — EXCEL (ClosedXML)
        // ============================================================

        [HttpGet("exports/etat-stocks/excel")]
        public async Task<IActionResult> ExportEtatStocksExcel([FromQuery] int? depotId)
        {
            try
            {
                var data = await _stockService.GetEtatStocksAsync(depotId);
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("État des Stocks");
                var headers = new[] { "Référence", "Article", "Catégorie", "Dépôt", "Unité",
                    "Qté Disponible", "Qté Réservée", "Qté Libre", "Seuil Min", "Prix Moyen",
                    "Valeur Stock", "Niveau Alerte", "Dernier Mouvement" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
                    ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }
                int row = 2;
                foreach (var item in data)
                {
                    ws.Cell(row, 1).Value = item.Reference;
                    ws.Cell(row, 2).Value = item.ArticleNom;
                    ws.Cell(row, 3).Value = item.CategorieNom;
                    ws.Cell(row, 4).Value = item.DepotNom;
                    ws.Cell(row, 5).Value = item.Unite;
                    ws.Cell(row, 6).Value = (double)item.QuantiteDisponible;
                    ws.Cell(row, 7).Value = (double)item.QuantiteReservee;
                    ws.Cell(row, 8).Value = (double)item.QuantiteLibre;
                    ws.Cell(row, 9).Value = (double)item.SeuilMinimum;
                    ws.Cell(row, 10).Value = (double)item.PrixUnitaireMoyen;
                    ws.Cell(row, 11).Value = (double)item.ValeurStock;
                    ws.Cell(row, 12).Value = item.NiveauAlerte;
                    ws.Cell(row, 13).Value = item.DateDernierMouvement?.ToString("dd/MM/yyyy") ?? "";
                    if (item.NiveauAlerte == "Rupture") ws.Row(row).Style.Fill.BackgroundColor = XLColor.LightCoral;
                    else if (item.NiveauAlerte == "Critique") ws.Row(row).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    row++;
                }
                ws.Columns().AdjustToContents();
                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"etat-stocks-{DateTime.Now:yyyyMMdd}.xlsx");
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur génération Excel : {ex.Message}"); }
        }

        [HttpGet("exports/historique/excel")]
        public async Task<IActionResult> ExportHistoriqueExcel(
            [FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin,
            [FromQuery] int? articleId, [FromQuery] int? depotId)
        {
            try
            {
                var data = await _stockService.GetHistoriqueAsync(dateDebut, dateFin, articleId, depotId);
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Historique Mouvements");
                var headers = new[] { "Date", "Type", "Référence Article", "Article", "Catégorie",
                    "Dépôt", "Entrée", "Sortie", "Qté Avant", "Qté Après", "Prix Unitaire",
                    "Montant", "N° Demande", "Projet", "Étape", "Opérateur", "Motif / Notes" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
                    ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }
                int row = 2;
                foreach (var item in data)
                {
                    ws.Cell(row, 1).Value = item.DateMouvement.ToString("dd/MM/yyyy HH:mm");
                    ws.Cell(row, 2).Value = item.TypeMouvement;
                    ws.Cell(row, 3).Value = item.ArticleReference;
                    ws.Cell(row, 4).Value = item.ArticleNom;
                    ws.Cell(row, 5).Value = item.CategorieNom;
                    ws.Cell(row, 6).Value = item.DepotNom;
                    ws.Cell(row, 7).Value = (double)item.Entree;
                    ws.Cell(row, 8).Value = (double)item.Sortie;
                    ws.Cell(row, 9).Value = (double)item.QuantiteAvant;
                    ws.Cell(row, 10).Value = (double)item.QuantiteApres;
                    ws.Cell(row, 11).Value = item.PrixUnitaire.HasValue ? (double)item.PrixUnitaire.Value : 0;
                    ws.Cell(row, 12).Value = item.MontantTotal.HasValue ? (double)item.MontantTotal.Value : 0;
                    ws.Cell(row, 13).Value = item.NumeroDemande ?? "";
                    ws.Cell(row, 14).Value = item.ProjetNom ?? "";
                    ws.Cell(row, 15).Value = item.EtapeNom ?? "";
                    ws.Cell(row, 16).Value = item.OperateurNom;
                    ws.Cell(row, 17).Value = item.MotifSortie ?? item.Notes ?? "";
                    row++;
                }
                ws.Columns().AdjustToContents();
                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"historique-mouvements-{DateTime.Now:yyyyMMdd}.xlsx");
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur génération Excel : {ex.Message}"); }
        }

        [HttpGet("exports/alertes/excel")]
        public async Task<IActionResult> ExportAlertesExcel()
        {
            try
            {
                var data = await _stockService.GetAlertesStockAsync();
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Alertes Stock");
                var headers = new[] { "Type Alerte", "Référence", "Article", "Catégorie",
                    "Unité", "Stock Total", "Seuil Min", "Qté Manquante",
                    "Valeur à Réappro.", "Fournisseur Préf.", "Téléphone", "Email" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#dc2626");
                    ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }
                int row = 2;
                foreach (var item in data)
                {
                    ws.Cell(row, 1).Value = item.TypeAlerte;
                    ws.Cell(row, 2).Value = item.Reference;
                    ws.Cell(row, 3).Value = item.ArticleNom;
                    ws.Cell(row, 4).Value = item.CategorieNom;
                    ws.Cell(row, 5).Value = item.Unite;
                    ws.Cell(row, 6).Value = (double)item.StockTotal;
                    ws.Cell(row, 7).Value = (double)item.SeuilMinimum;
                    ws.Cell(row, 8).Value = (double)item.QuantiteManquante;
                    ws.Cell(row, 9).Value = (double)item.ValeurAReapprovisionner;
                    ws.Cell(row, 10).Value = item.FournisseurPreferentiel ?? "";
                    ws.Cell(row, 11).Value = item.TelFournisseur ?? "";
                    ws.Cell(row, 12).Value = item.EmailFournisseur ?? "";
                    ws.Row(row).Style.Fill.BackgroundColor =
                        item.TypeAlerte == "Rupture" ? XLColor.LightCoral : XLColor.LightYellow;
                    row++;
                }
                ws.Columns().AdjustToContents();
                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"alertes-stock-{DateTime.Now:yyyyMMdd}.xlsx");
            }
            catch (Exception ex) { return StatusCode(500, $"Erreur génération Excel : {ex.Message}"); }
        }

        // ============================================================
        // GÉNÉRATEURS PDF (QuestPDF) — inchangés
        // ============================================================

        private byte[] GenererPdfEtatStocks(List<EtatStockDTO> data, int? depotId)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(8));
                    page.Header().Element(ComposeHeader($"ÉTAT DES STOCKS — {DateTime.Now:dd/MM/yyyy}",
                        depotId.HasValue ? $"Dépôt #{depotId}" : "Tous les dépôts"));
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.2f); cols.RelativeColumn(2.5f); cols.RelativeColumn(1.5f);
                            cols.RelativeColumn(1.5f); cols.RelativeColumn(0.6f); cols.RelativeColumn(1f);
                            cols.RelativeColumn(1f); cols.RelativeColumn(1f); cols.RelativeColumn(1f);
                            cols.RelativeColumn(1.2f); cols.RelativeColumn(1f);
                        });
                        table.Header(header =>
                        {
                            foreach (var h in new[] { "Référence", "Article", "Catégorie", "Dépôt", "Unité",
                                "Disponible", "Réservée", "Libre", "Seuil Min", "Valeur (F CFA)", "Statut" })
                                header.Cell().Background("#1e3a5f").Padding(4).Text(h).Bold().FontColor(Colors.White).FontSize(7.5f);
                        });
                        foreach (var item in data)
                        {
                            var bgColor = item.NiveauAlerte == "Rupture" ? "#fee2e2"
                                        : item.NiveauAlerte == "Critique" ? "#fef9c3" : Colors.White;
                            IContainer Cell() => table.Cell().Background(bgColor).Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
                            Cell().Text(item.Reference).FontSize(7.5f);
                            Cell().Text(item.ArticleNom).FontSize(7.5f);
                            Cell().Text(item.CategorieNom).FontSize(7.5f);
                            Cell().Text(item.DepotNom).FontSize(7.5f);
                            Cell().Text(item.Unite).FontSize(7.5f);
                            Cell().AlignRight().Text($"{item.QuantiteDisponible:N2}").FontSize(7.5f);
                            Cell().AlignRight().Text($"{item.QuantiteReservee:N2}").FontSize(7.5f);
                            Cell().AlignRight().Text($"{item.QuantiteLibre:N2}").FontSize(7.5f);
                            Cell().AlignRight().Text($"{item.SeuilMinimum:N2}").FontSize(7.5f);
                            Cell().AlignRight().Text($"{item.ValeurStock:N0}").FontSize(7.5f);
                            Cell().AlignCenter().Text(item.NiveauAlerte).Bold().FontSize(7.5f);
                        }
                    });
                    page.Footer().Element(ComposeFooter());
                });
            }).GeneratePdf();
        }

        private byte[] GenererPdfAlertes(List<AlerteStockDTO> data)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    page.Header().Element(ComposeHeader("ALERTES STOCK MINIMUM", $"Édité le {DateTime.Now:dd/MM/yyyy HH:mm}"));
                    page.Content().Column(col =>
                    {
                        var ruptures = data.Where(d => d.TypeAlerte == "Rupture").ToList();
                        var critiques = data.Where(d => d.TypeAlerte == "Critique").ToList();
                        if (ruptures.Any())
                        {
                            col.Item().PaddingBottom(8).Text($"RUPTURES DE STOCK ({ruptures.Count} article(s))").Bold().FontSize(11).FontColor("#dc2626");
                            col.Item().Element(c => ComposeTableAlertes(c, ruptures));
                        }
                        if (critiques.Any())
                        {
                            col.Item().PaddingTop(16).PaddingBottom(8).Text($"NIVEAUX CRITIQUES ({critiques.Count} article(s))").Bold().FontSize(11).FontColor("#b45309");
                            col.Item().Element(c => ComposeTableAlertes(c, critiques));
                        }
                    });
                    page.Footer().Element(ComposeFooter());
                });
            }).GeneratePdf();
        }

        private void ComposeTableAlertes(IContainer container, List<AlerteStockDTO> data)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1.5f); cols.RelativeColumn(3f); cols.RelativeColumn(0.8f);
                    cols.RelativeColumn(1f); cols.RelativeColumn(1f); cols.RelativeColumn(1.5f); cols.RelativeColumn(1.5f);
                });
                table.Header(header =>
                {
                    foreach (var h in new[] { "Référence", "Article", "Unité", "Stock", "Manquant", "Fournisseur", "Contact" })
                        header.Cell().Background("#374151").Padding(4).Text(h).Bold().FontColor(Colors.White).FontSize(8);
                });
                foreach (var item in data)
                {
                    var bg = item.TypeAlerte == "Rupture" ? "#fee2e2" : "#fef9c3";
                    IContainer Cell() => table.Cell().Background(bg).Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
                    Cell().Text(item.Reference).FontSize(8);
                    Cell().Text(item.ArticleNom).FontSize(8);
                    Cell().Text(item.Unite).FontSize(8);
                    Cell().AlignRight().Text($"{item.StockTotal:N2}").FontSize(8);
                    Cell().AlignRight().Text($"{item.QuantiteManquante:N2}").Bold().FontSize(8);
                    Cell().Text(item.FournisseurPreferentiel ?? "N/A").FontSize(8);
                    Cell().Text(item.TelFournisseur ?? "").FontSize(8);
                }
            });
        }

        private byte[] GenererPdfHistorique(List<HistoriqueMouvementDTO> data, DateTime? dateDebut, DateTime? dateFin)
        {
            var periode = dateDebut.HasValue && dateFin.HasValue
                ? $"{dateDebut.Value:dd/MM/yyyy} au {dateFin.Value:dd/MM/yyyy}" : "Toute période";
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(7.5f));
                    page.Header().Element(ComposeHeader("HISTORIQUE DES MOUVEMENTS DE STOCK", periode));
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.5f); cols.RelativeColumn(1f); cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(2.5f); cols.RelativeColumn(1.5f); cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f); cols.RelativeColumn(0.8f); cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1.5f); cols.RelativeColumn(1f);
                        });
                        table.Header(header =>
                        {
                            foreach (var h in new[] { "Date", "Type", "Réf. Article", "Article", "Dépôt",
                                "Unité", "Entrée", "Sortie", "Stock Après", "Projet / Demande", "Opérateur" })
                                header.Cell().Background("#1e3a5f").Padding(4).Text(h).Bold().FontColor(Colors.White).FontSize(7);
                        });
                        bool alternate = false;
                        foreach (var item in data)
                        {
                            var bg = alternate ? Color.FromHex("#f9fafb") : Colors.White; alternate = !alternate;
                            IContainer Cell() => table.Cell().Background(bg).Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);
                            Cell().Text(item.DateMouvement.ToString("dd/MM/yy HH:mm"));
                            Cell().Text(item.TypeMouvement);
                            Cell().Text(item.ArticleReference);
                            Cell().Text(item.ArticleNom);
                            Cell().Text(item.DepotNom);
                            Cell().Text(item.Unite);
                            Cell().AlignRight().Text(item.Entree > 0 ? $"+{item.Entree:N2}" : "").FontColor(Colors.Green.Darken2);
                            Cell().AlignRight().Text(item.Sortie > 0 ? $"-{item.Sortie:N2}" : "").FontColor(Colors.Red.Darken2);
                            Cell().AlignRight().Text($"{item.QuantiteApres:N2}");
                            Cell().Text(item.ProjetNom ?? item.NumeroDemande ?? "");
                            Cell().Text(item.OperateurNom);
                        }
                    });
                    page.Footer().Element(ComposeFooter());
                });
            }).GeneratePdf();
        }

        private byte[] GenererPdfDemandesParProjet(List<DemandeParProjetDTO> data)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(8));
                    page.Header().Element(ComposeHeader("SUIVI DES DEMANDES PAR PROJET", $"Édité le {DateTime.Now:dd/MM/yyyy}"));
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.2f); cols.RelativeColumn(2f); cols.RelativeColumn(2f);
                            cols.RelativeColumn(1f); cols.RelativeColumn(1f); cols.RelativeColumn(1f);
                            cols.RelativeColumn(0.8f); cols.RelativeColumn(1.5f); cols.RelativeColumn(1.5f); cols.RelativeColumn(1f);
                        });
                        table.Header(header =>
                        {
                            foreach (var h in new[] { "N° Demande", "Projet", "Étape", "Demandeur", "Date", "Statut",
                                "Articles", "Montant HT", "Fournisseur", "Validateur" })
                                header.Cell().Background("#1e3a5f").Padding(4).Text(h).Bold().FontColor(Colors.White).FontSize(7.5f);
                        });
                        bool alternate = false;
                        foreach (var item in data)
                        {
                            var bg = alternate ? Color.FromHex("#f9fafb") : Colors.White; alternate = !alternate;
                            IContainer Cell() => table.Cell().Background(bg).Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);
                            Cell().Text(item.Numero);
                            Cell().Text(item.ProjetNom ?? item.TypeDestination);
                            Cell().Text(item.EtapeNom ?? "—");
                            Cell().Text(item.NomDemandeur);
                            Cell().Text(item.DateDemande.ToString("dd/MM/yyyy"));
                            Cell().Text(item.Statut);
                            Cell().AlignCenter().Text(item.NombreArticles.ToString());
                            Cell().AlignRight().Text($"{item.MontantDevisHT ?? item.MontantTotal:N0}");
                            Cell().Text(item.FournisseurNom ?? "—");
                            Cell().Text(item.ValidateurNom ?? "—");
                        }
                    });
                    page.Footer().Element(ComposeFooter());
                });
            }).GeneratePdf();
        }

        private byte[] GenererPdfFournisseurs(List<RapportFournisseurDTO> data)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    page.Header().Element(ComposeHeader("RAPPORT FOURNISSEURS", $"Édité le {DateTime.Now:dd/MM/yyyy}"));
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1f); cols.RelativeColumn(2.5f); cols.RelativeColumn(1.5f);
                            cols.RelativeColumn(1.5f); cols.RelativeColumn(2f); cols.RelativeColumn(1f);
                            cols.RelativeColumn(1.5f); cols.RelativeColumn(1.5f); cols.RelativeColumn(1.5f); cols.RelativeColumn(0.8f);
                        });
                        table.Header(header =>
                        {
                            foreach (var h in new[] { "Code", "Nom", "Ville", "Téléphone", "Email",
                                "Commandes", "Total HT (F CFA)", "Moy./Cmd.", "Dernière Cmd.", "Note" })
                                header.Cell().Background("#1e3a5f").Padding(4).Text(h).Bold().FontColor(Colors.White).FontSize(8);
                        });
                        bool alternate = false;
                        foreach (var item in data)
                        {
                            var bg = alternate ? Color.FromHex("#f9fafb") : Colors.White; alternate = !alternate;
                            IContainer Cell() => table.Cell().Background(bg).Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);
                            Cell().Text(item.FournisseurCode);
                            Cell().Text(item.FournisseurNom).Bold();
                            Cell().Text(item.Ville ?? "");
                            Cell().Text(item.Telephone ?? "");
                            Cell().Text(item.Email ?? "");
                            Cell().AlignCenter().Text(item.NombreCommandes.ToString());
                            Cell().AlignRight().Text($"{item.MontantTotalHT:N0}");
                            Cell().AlignRight().Text($"{item.MontantMoyenCommande:N0}");
                            Cell().AlignCenter().Text(item.DerniereCommande?.ToString("dd/MM/yyyy") ?? "—");
                            Cell().AlignCenter().Text(item.NoteEvaluation > 0 ? $"{item.NoteEvaluation}/5" : "—");
                        }
                    });
                    page.Footer().Element(ComposeFooter());
                });
            }).GeneratePdf();
        }

        // ============================================================
        // HELPERS QuestPDF (inchangés)
        // ============================================================

        private Action<IContainer> ComposeHeader(string titre, string sousTitre) =>
            container => container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("SimplicityProjects").Bold().FontSize(14).FontColor("#1e3a5f");
                        c.Item().Text(titre).Bold().FontSize(11).FontColor("#374151");
                        c.Item().Text(sousTitre).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(150).AlignRight().Column(c =>
                        c.Item().Text($"Généré le {DateTime.Now:dd/MM/yyyy à HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1).AlignRight());
                });
                col.Item().PaddingTop(6).BorderBottom(2).BorderColor("#1e3a5f");
                col.Item().Height(8);
            });

        private Action<IContainer> ComposeFooter() =>
            container => container.BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("SimplicityProjects — Module Gestion de Stock").FontSize(7).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" / ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
            });

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return 3; // Fallback - à améliorer
        }
    }
}
