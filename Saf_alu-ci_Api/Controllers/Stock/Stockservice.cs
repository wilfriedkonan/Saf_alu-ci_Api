using Microsoft.Data.SqlClient;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.Stock
{
    public class StockService
    {
        private readonly string _connectionString;

        public StockService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ── Utilitaire interne : valider les paramètres de page ──
        private static (int page, int pageSize, int offset) NormalisePagination(int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);
            return (page, pageSize, (page - 1) * pageSize);
        }

        // ============================================================
        // CATÉGORIES — avec pagination + recherche
        // ============================================================

        public async Task<List<StockCategorie>> GetAllCategoriesAsync()
        {
            var list = new List<StockCategorie>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM Stock_Categories WHERE Actif = 1 ORDER BY Nom", conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapToCategorie(reader));
            return list;
        }

        /// <summary>Liste paginée des catégories avec recherche sur Code et Nom</summary>
        public async Task<PagedResult<StockCategorie>> GetCategoriesPagedAsync(CategorieSearchParams p)
        {
            var (page, pageSize, offset) = NormalisePagination(p.Page, p.PageSize);
            var hasSearch = !string.IsNullOrWhiteSpace(p.Search);

            var where = "WHERE Actif = 1";
            if (hasSearch) where += " AND (Code LIKE @Search OR Nom LIKE @Search)";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Compte total
            int total;
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM Stock_Categories {where}", conn))
            {
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                total = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // Données paginées
            var list = new List<StockCategorie>();
            var sql = $@"SELECT * FROM Stock_Categories {where}
                         ORDER BY Nom
                         OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) list.Add(MapToCategorie(reader));
            }

            return new PagedResult<StockCategorie> { Items = list, TotalItems = total, Page = page, PageSize = pageSize };
        }

        public async Task<StockCategorie?> GetCategorieByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM Stock_Categories WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return MapToCategorie(reader);
            return null;
        }

        public async Task<int> CreateCategorieAsync(CreateStockCategorieRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO Stock_Categories (Code, Nom, Description, Couleur, Actif, DateCreation, DateModification)
                VALUES (@Code, @Nom, @Description, @Couleur, 1, GETUTCDATE(), GETUTCDATE());
                SELECT SCOPE_IDENTITY();", conn);
            cmd.Parameters.AddWithValue("@Code", req.Code);
            cmd.Parameters.AddWithValue("@Nom", req.Nom);
            cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Couleur", req.Couleur);
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateCategorieAsync(int id, UpdateStockCategorieRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Stock_Categories SET
                    Nom = ISNULL(@Nom, Nom), Description = ISNULL(@Description, Description),
                    Couleur = ISNULL(@Couleur, Couleur), DateModification = GETUTCDATE()
                WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Nom", req.Nom ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Couleur", req.Couleur ?? (object)DBNull.Value);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ============================================================
        // DÉPÔTS — avec pagination + recherche
        // ============================================================

        public async Task<List<StockDepot>> GetAllDepotsAsync()
        {
            var list = new List<StockDepot>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT d.*, u.Prenom + ' ' + u.Nom AS ResponsableNom
                FROM Stock_Depots d LEFT JOIN Utilisateurs u ON d.ResponsableId = u.Id
                WHERE d.Actif = 1 ORDER BY d.Nom", conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapToDepot(reader));
            return list;
        }

        /// <summary>Liste paginée des dépôts avec recherche sur Code, Nom, Ville</summary>
        public async Task<PagedResult<StockDepot>> GetDepotsPagedAsync(DepotSearchParams p)
        {
            var (page, pageSize, offset) = NormalisePagination(p.Page, p.PageSize);
            var hasSearch = !string.IsNullOrWhiteSpace(p.Search);

            var where = "WHERE d.Actif = 1";
            if (hasSearch) where += " AND (d.Code LIKE @Search OR d.Nom LIKE @Search OR d.Ville LIKE @Search)";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int total;
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM Stock_Depots d {where}", conn))
            {
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                total = (int)(await cmd.ExecuteScalarAsync())!;
            }

            var list = new List<StockDepot>();
            var sql = $@"SELECT d.*, u.Prenom + ' ' + u.Nom AS ResponsableNom
                         FROM Stock_Depots d LEFT JOIN Utilisateurs u ON d.ResponsableId = u.Id
                         {where} ORDER BY d.Nom
                         OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) list.Add(MapToDepot(reader));
            }
            return new PagedResult<StockDepot> { Items = list, TotalItems = total, Page = page, PageSize = pageSize };
        }

        public async Task<StockDepot?> GetDepotByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT d.*, u.Prenom + ' ' + u.Nom AS ResponsableNom
                FROM Stock_Depots d LEFT JOIN Utilisateurs u ON d.ResponsableId = u.Id
                WHERE d.Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return MapToDepot(reader);
            return null;
        }

        public async Task<int> CreateDepotAsync(CreateStockDepotRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                // Si ce dépôt doit être par défaut, retirer le flag sur les autres en amont
                if (req.EstParDefaut)
                    await RetirerDepotParDefautAsync(conn, transaction);

                using var cmd = new SqlCommand(@"
                    INSERT INTO Stock_Depots (Code, Nom, Description, Adresse, Ville, ResponsableId,
                        Actif, EstParDefaut, DateCreation, DateModification)
                    VALUES (@Code, @Nom, @Description, @Adresse, @Ville, @ResponsableId,
                        1, @EstParDefaut, GETUTCDATE(), GETUTCDATE());
                    SELECT SCOPE_IDENTITY();", conn, transaction);
                cmd.Parameters.AddWithValue("@Code", req.Code);
                cmd.Parameters.AddWithValue("@Nom", req.Nom);
                cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Adresse", req.Adresse ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Ville", req.Ville ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ResponsableId", req.ResponsableId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@EstParDefaut", req.EstParDefaut);
                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                transaction.Commit();
                return id;
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task UpdateDepotAsync(int id, UpdateStockDepotRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                // Si on demande à ce dépôt de devenir par défaut, retirer le flag sur les autres
                if (req.EstParDefaut == true)
                    await RetirerDepotParDefautAsync(conn, transaction, exceptId: id);

                var sql = @"UPDATE Stock_Depots SET
                    Nom = ISNULL(@Nom, Nom), Description = ISNULL(@Description, Description),
                    Adresse = ISNULL(@Adresse, Adresse), Ville = ISNULL(@Ville, Ville),
                    ResponsableId = @ResponsableId, DateModification = GETUTCDATE()";
                if (req.EstParDefaut.HasValue)
                    sql += ", EstParDefaut = @EstParDefaut";
                sql += " WHERE Id = @Id";

                using var cmd = new SqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Nom", req.Nom ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Adresse", req.Adresse ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Ville", req.Ville ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ResponsableId", req.ResponsableId ?? (object)DBNull.Value);
                if (req.EstParDefaut.HasValue)
                    cmd.Parameters.AddWithValue("@EstParDefaut", req.EstParDefaut.Value);
                await cmd.ExecuteNonQueryAsync();
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        /// <summary>Retire le flag EstParDefaut de tous les dépôts (sauf exceptId si précisé).</summary>
        private async Task RetirerDepotParDefautAsync(
            SqlConnection conn, SqlTransaction transaction, int? exceptId = null)
        {
            var sql = "UPDATE Stock_Depots SET EstParDefaut = 0, DateModification = GETUTCDATE() WHERE EstParDefaut = 1";
            if (exceptId.HasValue) sql += " AND Id <> @ExceptId";
            using var cmd = new SqlCommand(sql, conn, transaction);
            if (exceptId.HasValue) cmd.Parameters.AddWithValue("@ExceptId", exceptId.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Définit explicitement un dépôt comme par défaut.
        /// Endpoint dédié : PUT /api/stock/depots/{id}/par-defaut
        /// </summary>
        public async Task SetDepotParDefautAsync(int depotId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                // Vérifier existence + actif
                int existe;
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Stock_Depots WHERE Id = @Id AND Actif = 1", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", depotId);
                    existe = (int)(await cmd.ExecuteScalarAsync())!;
                }
                if (existe == 0)
                    throw new InvalidOperationException("Dépôt introuvable ou inactif.");

                await RetirerDepotParDefautAsync(conn, transaction, exceptId: depotId);
                using (var cmd = new SqlCommand(
                    "UPDATE Stock_Depots SET EstParDefaut = 1, DateModification = GETUTCDATE() WHERE Id = @Id",
                    conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", depotId);
                    await cmd.ExecuteNonQueryAsync();
                }
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        // ============================================================
        // FOURNISSEURS — avec pagination + recherche
        // ============================================================

        public async Task<List<StockFournisseur>> GetAllFournisseursAsync()
        {
            var list = new List<StockFournisseur>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM Fournisseurs WHERE Actif = 1 ORDER BY Nom", conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapToFournisseur(reader));
            return list;
        }

        /// <summary>Liste paginée des fournisseurs avec recherche sur Nom, Code, Ville, Email, Contact</summary>
        public async Task<PagedResult<StockFournisseur>> GetFournisseursPagedAsync(FournisseurSearchParams p)
        {
            var (page, pageSize, offset) = NormalisePagination(p.Page, p.PageSize);
            var hasSearch = !string.IsNullOrWhiteSpace(p.Search);

            var where = "WHERE Actif = 1";
            if (hasSearch) where += @" AND (Nom LIKE @Search OR Code LIKE @Search
                                       OR Ville LIKE @Search OR Email LIKE @Search OR Contact LIKE @Search)";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int total;
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM Fournisseurs {where}", conn))
            {
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                total = (int)(await cmd.ExecuteScalarAsync())!;
            }

            var list = new List<StockFournisseur>();
            var sql = $@"SELECT * FROM Fournisseurs {where}
                         ORDER BY Nom
                         OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) list.Add(MapToFournisseur(reader));
            }
            return new PagedResult<StockFournisseur> { Items = list, TotalItems = total, Page = page, PageSize = pageSize };
        }

        public async Task<StockFournisseur?> GetFournisseurByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM Fournisseurs WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return MapToFournisseur(reader);
            return null;
        }

        public async Task<int> CreateFournisseurAsync(CreateStockFournisseurRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO Fournisseurs (Code, Nom, Contact, Telephone, Email, Adresse, Ville, Actif, DateCreation, DateModification)
                VALUES (@Code, @Nom, @Contact, @Telephone, @Email, @Adresse, @Ville, 1, GETUTCDATE(), GETUTCDATE());
                SELECT SCOPE_IDENTITY();", conn);
            cmd.Parameters.AddWithValue("@Code", req.Code);
            cmd.Parameters.AddWithValue("@Nom", req.Nom);
            cmd.Parameters.AddWithValue("@Contact", req.Contact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Telephone", req.Telephone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", req.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Adresse", req.Adresse ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ville", req.Ville ?? (object)DBNull.Value);
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateFournisseurAsync(int id, UpdateStockFournisseurRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Fournisseurs SET
                    Nom = ISNULL(@Nom, Nom), Contact = ISNULL(@Contact, Contact),
                    Telephone = ISNULL(@Telephone, Telephone), Email = ISNULL(@Email, Email),
                    Adresse = ISNULL(@Adresse, Adresse), Ville = ISNULL(@Ville, Ville),
                    NoteEvaluation = @NoteEvaluation, DateModification = GETUTCDATE()
                WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Nom", req.Nom ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Contact", req.Contact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Telephone", req.Telephone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", req.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Adresse", req.Adresse ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ville", req.Ville ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NoteEvaluation", req.NoteEvaluation ?? (object)DBNull.Value);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ============================================================
        // ARTICLES — avec pagination + recherche par désignation
        // ============================================================

        public async Task<List<StockArticle>> GetAllArticlesAsync(string? searchTerm = null, int? categorieId = null)
        {
            var list = new List<StockArticle>();
            using var conn = new SqlConnection(_connectionString);
            var sql = @"SELECT a.*, c.Nom AS CategorieNom, c.Couleur AS CategorieCouleur, f.Nom AS FournisseurNom
                        FROM Stock_Articles a
                        INNER JOIN Stock_Categories c ON a.CategorieId = c.Id
                        LEFT JOIN Fournisseurs f ON a.FournisseurPreferentielId = f.Id
                        WHERE a.Actif = 1";
            if (!string.IsNullOrEmpty(searchTerm)) sql += " AND (a.Nom LIKE @Search OR a.Reference LIKE @Search OR a.Description LIKE @Search)";
            if (categorieId.HasValue) sql += " AND a.CategorieId = @CategorieId";
            sql += " ORDER BY a.Reference";
            using var cmd = new SqlCommand(sql, conn);
            if (!string.IsNullOrEmpty(searchTerm)) cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
            if (categorieId.HasValue) cmd.Parameters.AddWithValue("@CategorieId", categorieId.Value);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapToArticle(reader));
            return list;
        }

        /// <summary>
        /// Liste paginée des articles avec recherche sur Nom, Référence, Description.
        /// Filtres optionnels : catégorie, fournisseur préférentiel.
        /// </summary>
        public async Task<PagedResult<StockArticle>> GetArticlesPagedAsync(ArticleSearchParams p)
        {
            var (page, pageSize, offset) = NormalisePagination(p.Page, p.PageSize);
            var hasSearch = !string.IsNullOrWhiteSpace(p.Search);

            var where = "WHERE a.Actif = 1";
            if (hasSearch) where += " AND (a.Nom LIKE @Search OR a.Reference LIKE @Search OR a.Description LIKE @Search)";
            if (p.CategorieId.HasValue) where += " AND a.CategorieId = @CategorieId";
            if (p.FournisseurId.HasValue) where += " AND a.FournisseurPreferentielId = @FournisseurId";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int total;
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM Stock_Articles a {where}", conn))
            {
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                if (p.CategorieId.HasValue) cmd.Parameters.AddWithValue("@CategorieId", p.CategorieId.Value);
                if (p.FournisseurId.HasValue) cmd.Parameters.AddWithValue("@FournisseurId", p.FournisseurId.Value);
                total = (int)(await cmd.ExecuteScalarAsync())!;
            }

            var list = new List<StockArticle>();
            var sql = $@"SELECT a.*, c.Nom AS CategorieNom, c.Couleur AS CategorieCouleur, f.Nom AS FournisseurNom
                         FROM Stock_Articles a
                         INNER JOIN Stock_Categories c ON a.CategorieId = c.Id
                         LEFT JOIN Fournisseurs f ON a.FournisseurPreferentielId = f.Id
                         {where}
                         ORDER BY a.Reference
                         OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                if (p.CategorieId.HasValue) cmd.Parameters.AddWithValue("@CategorieId", p.CategorieId.Value);
                if (p.FournisseurId.HasValue) cmd.Parameters.AddWithValue("@FournisseurId", p.FournisseurId.Value);
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) list.Add(MapToArticle(reader));
            }
            return new PagedResult<StockArticle> { Items = list, TotalItems = total, Page = page, PageSize = pageSize };
        }

        public async Task<StockArticle?> GetArticleByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT a.*, c.Nom AS CategorieNom, c.Couleur AS CategorieCouleur, f.Nom AS FournisseurNom
                FROM Stock_Articles a
                INNER JOIN Stock_Categories c ON a.CategorieId = c.Id
                LEFT JOIN Fournisseurs f ON a.FournisseurPreferentielId = f.Id
                WHERE a.Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return MapToArticle(reader);
            return null;
        }

        public async Task<int> CreateArticleAsync(CreateStockArticleRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO Stock_Articles (Reference, Nom, Description, CategorieId, Unite,
                    PrixUnitaireMoyen, SeuilMinimum, SeuilMaximum, FournisseurPreferentielId,
                    Actif, DateCreation, DateModification)
                VALUES (@Reference, @Nom, @Description, @CategorieId, @Unite,
                    @PrixUnitaireMoyen, @SeuilMinimum, @SeuilMaximum, @FournisseurPreferentielId,
                    1, GETUTCDATE(), GETUTCDATE());
                SELECT SCOPE_IDENTITY();", conn);
            cmd.Parameters.AddWithValue("@Reference", req.Reference);
            cmd.Parameters.AddWithValue("@Nom", req.Nom);
            cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CategorieId", req.CategorieId);
            cmd.Parameters.AddWithValue("@Unite", req.Unite);
            cmd.Parameters.AddWithValue("@PrixUnitaireMoyen", req.PrixUnitaireMoyen);
            cmd.Parameters.AddWithValue("@SeuilMinimum", req.SeuilMinimum);
            cmd.Parameters.AddWithValue("@SeuilMaximum", req.SeuilMaximum ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FournisseurPreferentielId", req.FournisseurPreferentielId ?? (object)DBNull.Value);
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateArticleAsync(int id, UpdateStockArticleRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Stock_Articles SET
                    Nom = ISNULL(@Nom, Nom), Description = ISNULL(@Description, Description),
                    CategorieId = ISNULL(@CategorieId, CategorieId), Unite = ISNULL(@Unite, Unite),
                    PrixUnitaireMoyen = ISNULL(@PrixUnitaireMoyen, PrixUnitaireMoyen),
                    SeuilMinimum = ISNULL(@SeuilMinimum, SeuilMinimum),
                    SeuilMaximum = @SeuilMaximum, FournisseurPreferentielId = @FournisseurPreferentielId,
                    DateModification = GETUTCDATE()
                WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Nom", req.Nom ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CategorieId", req.CategorieId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Unite", req.Unite ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PrixUnitaireMoyen", req.PrixUnitaireMoyen ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SeuilMinimum", req.SeuilMinimum ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SeuilMaximum", req.SeuilMaximum ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FournisseurPreferentielId", req.FournisseurPreferentielId ?? (object)DBNull.Value);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ============================================================
        // INVENTAIRE / ÉTAT DES STOCKS — avec pagination + recherche
        // ============================================================

        public async Task<List<EtatStockDTO>> GetEtatStocksAsync(int? depotId = null, bool alertesSeulement = false)
        {
            var list = new List<EtatStockDTO>();
            using var conn = new SqlConnection(_connectionString);
            var sql = "SELECT * FROM v_Stock_EtatStocks WHERE 1=1";
            if (depotId.HasValue) sql += " AND DepotId = @DepotId";
            if (alertesSeulement) sql += " AND EnAlerte = 1";
            sql += " ORDER BY CategorieNom, ArticleNom, DepotNom";
            using var cmd = new SqlCommand(sql, conn);
            if (depotId.HasValue) cmd.Parameters.AddWithValue("@DepotId", depotId.Value);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapToEtatStock(reader));
            return list;
        }

        /// <summary>Inventaire paginé avec recherche sur ArticleNom, Référence, CategorieNom, DepotNom</summary>
        public async Task<PagedResult<EtatStockDTO>> GetEtatStocksPagedAsync(EtatStockSearchParams p)
        {
            var (page, pageSize, offset) = NormalisePagination(p.Page, p.PageSize);
            var hasSearch = !string.IsNullOrWhiteSpace(p.Search);

            var where = "WHERE 1=1";
            if (p.DepotId.HasValue) where += " AND DepotId = @DepotId";
            if (p.AlertesSeulement) where += " AND EnAlerte = 1";
            if (hasSearch) where += " AND (ArticleNom LIKE @Search OR Reference LIKE @Search OR CategorieNom LIKE @Search OR DepotNom LIKE @Search)";
            if (p.CategorieId.HasValue) where += " AND CategorieId = @CategorieId";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int total;
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM v_Stock_EtatStocks {where}", conn))
            {
                if (p.DepotId.HasValue) cmd.Parameters.AddWithValue("@DepotId", p.DepotId.Value);
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                if (p.CategorieId.HasValue) cmd.Parameters.AddWithValue("@CategorieId", p.CategorieId.Value);
                total = (int)(await cmd.ExecuteScalarAsync())!;
            }

            var list = new List<EtatStockDTO>();
            var sql = $@"SELECT * FROM v_Stock_EtatStocks {where}
                         ORDER BY CategorieNom, ArticleNom, DepotNom
                         OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (p.DepotId.HasValue) cmd.Parameters.AddWithValue("@DepotId", p.DepotId.Value);
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                if (p.CategorieId.HasValue) cmd.Parameters.AddWithValue("@CategorieId", p.CategorieId.Value);
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(MapToEtatStock(reader));
            }
            return new PagedResult<EtatStockDTO> { Items = list, TotalItems = total, Page = page, PageSize = pageSize };
        }

        public async Task<List<AlerteStockDTO>> GetAlertesStockAsync()
        {
            var list = new List<AlerteStockDTO>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM v_Stock_AlertesMinimum ORDER BY TypeAlerte DESC, QuantiteManquante DESC", conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapToAlerteStock(reader));
            return list;
        }

        // ============================================================
        // DEMANDES — avec pagination + recherche par désignation
        // ============================================================

        public async Task<List<StockDemandeDetailDTO>> GetAllDemandesAsync(string? statut = null, int? projetId = null)
        {
            var list = new List<StockDemandeDetailDTO>();
            using var conn = new SqlConnection(_connectionString);
            var sql = BuildDemandeBaseQuery() + " WHERE 1=1";
            if (!string.IsNullOrEmpty(statut)) sql += " AND dem.Statut = @Statut";
            if (projetId.HasValue) sql += " AND dem.ProjetId = @ProjetId";
            sql += " ORDER BY dem.DateCreation DESC";
            using var cmd = new SqlCommand(sql, conn);
            if (!string.IsNullOrEmpty(statut)) cmd.Parameters.AddWithValue("@Statut", statut);
            if (projetId.HasValue) cmd.Parameters.AddWithValue("@ProjetId", projetId.Value);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapToDemandeDetail(reader));
            reader.Close();
            foreach (var dem in list) dem.Articles = await GetDemandeArticlesAsync(conn, dem.Id);
            return list;
        }

        /// <summary>
        /// Liste paginée des demandes avec recherche sur :
        /// N° demande, nom demandeur, désignation article catalogue, désignation libre.
        /// </summary>
        public async Task<PagedResult<StockDemandeDetailDTO>> GetDemandesPagedAsync(DemandeSearchParams p)
        {
            var (page, pageSize, offset) = NormalisePagination(p.Page, p.PageSize);
            var hasSearch = !string.IsNullOrWhiteSpace(p.Search);

            // Sous-requête : IDs de demandes dont un article (catalogue ou libre) correspond à la recherche
            var where = "WHERE 1=1";
            if (!string.IsNullOrEmpty(p.Statut)) where += " AND dem.Statut = @Statut";
            if (p.ProjetId.HasValue) where += " AND dem.ProjetId = @ProjetId";
            if (p.DateDebut.HasValue) where += " AND dem.DateDemande >= @DateDebut";
            if (p.DateFin.HasValue) where += " AND dem.DateDemande <= @DateFin";
            if (hasSearch) where += @" AND (
                dem.Numero LIKE @Search
                OR dem.NomDemandeur LIKE @Search
                OR dem.PosteDemandeur LIKE @Search
                OR EXISTS (
                    SELECT 1 FROM Stock_DemandeArticles da
                    LEFT JOIN Stock_Articles a ON da.ArticleId = a.Id
                    WHERE da.DemandeId = dem.Id
                    AND (a.Nom LIKE @Search OR a.Reference LIKE @Search OR da.DesignationLibre LIKE @Search)
                )
            )";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Compter le total
            int total;
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM Stock_Demandes dem {where}", conn))
            {
                if (!string.IsNullOrEmpty(p.Statut)) cmd.Parameters.AddWithValue("@Statut", p.Statut);
                if (p.ProjetId.HasValue) cmd.Parameters.AddWithValue("@ProjetId", p.ProjetId.Value);
                if (p.DateDebut.HasValue) cmd.Parameters.AddWithValue("@DateDebut", p.DateDebut.Value);
                if (p.DateFin.HasValue) cmd.Parameters.AddWithValue("@DateFin", p.DateFin.Value);
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                total = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // Récupérer les IDs paginés d'abord (évite de paginer sur la jointure complexe)
            var ids = new List<int>();
            var idsSql = $@"SELECT dem.Id FROM Stock_Demandes dem {where}
                            ORDER BY dem.DateCreation DESC
                            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            using (var cmd = new SqlCommand(idsSql, conn))
            {
                if (!string.IsNullOrEmpty(p.Statut)) cmd.Parameters.AddWithValue("@Statut", p.Statut);
                if (p.ProjetId.HasValue) cmd.Parameters.AddWithValue("@ProjetId", p.ProjetId.Value);
                if (p.DateDebut.HasValue) cmd.Parameters.AddWithValue("@DateDebut", p.DateDebut.Value);
                if (p.DateFin.HasValue) cmd.Parameters.AddWithValue("@DateFin", p.DateFin.Value);
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                using var rId = await cmd.ExecuteReaderAsync();
                while (await rId.ReadAsync()) ids.Add(rId.GetInt32(0));
            }

            if (!ids.Any())
                return new PagedResult<StockDemandeDetailDTO> { Items = new(), TotalItems = total, Page = page, PageSize = pageSize };

            // Charger les demandes complètes pour ces IDs
            var inClause = string.Join(",", ids);
            var dataSql = BuildDemandeBaseQuery() + $" WHERE dem.Id IN ({inClause}) ORDER BY dem.DateCreation DESC";
            var list = new List<StockDemandeDetailDTO>();
            using (var cmd = new SqlCommand(dataSql, conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) list.Add(MapToDemandeDetail(reader));
                reader.Close();
                foreach (var dem in list) dem.Articles = await GetDemandeArticlesAsync(conn, dem.Id);
            }

            return new PagedResult<StockDemandeDetailDTO> { Items = list, TotalItems = total, Page = page, PageSize = pageSize };
        }

        public async Task<StockDemandeDetailDTO?> GetDemandeByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = BuildDemandeBaseQuery() + " WHERE dem.Id = @Id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            var dem = MapToDemandeDetail(reader);
            reader.Close();
            dem.Articles = await GetDemandeArticlesAsync(conn, id);
            dem.Mouvements = await GetDemandeMouvementsAsync(conn, id);
            dem.NbArticlesTotal = dem.Articles.Count;
            dem.NbArticlesLivres = dem.Articles.Count(a => a.EstLivre);
            return dem;
        }

        private static string BuildDemandeBaseQuery() => @"
            SELECT dem.*,
                p.Nom AS ProjetNom, p.Numero AS ProjetNumero,
                ep.Nom AS EtapeNom,
                v.Prenom + ' ' + v.Nom AS ValidateurNom,
                t.Id AS TraitementId, t.FournisseurId, t.NomFournisseurLibre,
                t.NumeroDevis, t.MontantDevisHT, t.MontantDevisTTC,
                t.DateDevis, t.FichierDevisPath, t.DelaiLivraison,
                t.ConditionsPaiement, t.Notes AS TraitementNotes,
                t.StatutTraitement, t.DateModification AS TraitementDateMod,
                f.Nom AS FournisseurNom,
                tp.Prenom + ' ' + tp.Nom AS TraiteParNom,
                dd.Nom  AS DepotDemandeNom,
                dd.Code AS DepotDemandeCode
            FROM Stock_Demandes dem
            LEFT JOIN Projets p ON dem.ProjetId = p.Id
            LEFT JOIN EtapesProjets ep ON dem.EtapeProjetId = ep.Id
            LEFT JOIN Utilisateurs v ON dem.ValidateurId = v.Id
            LEFT JOIN Stock_Traitements t ON dem.Id = t.DemandeId
            LEFT JOIN Fournisseurs f ON t.FournisseurId = f.Id
            LEFT JOIN Utilisateurs tp ON t.TraitePar = tp.Id
            LEFT JOIN Stock_Depots dd ON dem.DepotDemandeId = dd.Id";

        public async Task<int> CreateDemandeAsync(CreateStockDemandeRequest req)
        {

            using var conn = new SqlConnection(_connectionString);
            if (req.TypeDestination == "Administration")
            {
                var depot = await GetDepotBydefault();
                req.DepotDemandeId = depot != null ? depot.Id : (int?)null;
            }
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                var numero = await GenererNumeroDemande(conn, transaction);
                int demandeId;
                using (var cmd = new SqlCommand(@"
                    INSERT INTO Stock_Demandes (Numero, NomDemandeur, PosteDemandeur, UtilisateurId,
                        DemandeurId, DepotDemandeId,
                        TypeDestination, ProjetId, EtapeProjetId, Statut, MotifDemande,
                        DateDemande, DateCreation, DateModification)
                    VALUES (@Numero, @NomDemandeur, @PosteDemandeur, @UtilisateurId,
                        @DemandeurId, @DepotDemandeId,
                        @TypeDestination, @ProjetId, @EtapeProjetId, 'EnAttente', @MotifDemande,
                        GETUTCDATE(), GETUTCDATE(), GETUTCDATE());
                    SELECT SCOPE_IDENTITY();", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Numero", numero);
                    cmd.Parameters.AddWithValue("@NomDemandeur", req.NomDemandeur);
                    cmd.Parameters.AddWithValue("@PosteDemandeur", req.PosteDemandeur);
                    cmd.Parameters.AddWithValue("@UtilisateurId", req.UtilisateurId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DemandeurId", req.DemandeurId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DepotDemandeId", req.DepotDemandeId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TypeDestination", req.TypeDestination);
                    cmd.Parameters.AddWithValue("@ProjetId", req.ProjetId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@EtapeProjetId", req.EtapeProjetId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MotifDemande", req.MotifDemande ?? (object)DBNull.Value);
                    demandeId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
                foreach (var art in req.Articles)
                {
                    var source = art.isHorsCatalogue ? "Commande" : "Stock";
                    using var cmd = new SqlCommand(@"
                        INSERT INTO Stock_DemandeArticles (DemandeId, ArticleId, DesignationLibre,
                            Unite, QuantiteDemandee, Notes, Source, DateCreation)
                        VALUES (@DemandeId, @ArticleId, @DesignationLibre, @Unite, @QuantiteDemandee, @Notes, @Source, GETUTCDATE())", conn, transaction);
                    cmd.Parameters.AddWithValue("@DemandeId", demandeId);
                    cmd.Parameters.AddWithValue("@ArticleId", art.ArticleId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DesignationLibre", art.DesignationLibre ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Unite", art.Unite);
                    cmd.Parameters.AddWithValue("@QuantiteDemandee", art.QuantiteDemandee);
                    cmd.Parameters.AddWithValue("@Source", source);
                    cmd.Parameters.AddWithValue("@Notes", art.Notes ?? (object)DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
                transaction.Commit();
                return demandeId;
            }
            catch { transaction.Rollback(); throw; }
        }

        // ============================================================
        // TRAITEMENT (inchangé)
        // ============================================================

        public async Task<string> SauvegarderTraitementAsync(int demandeId, SauvegarderTraitementRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                await UpsertTraitement(conn, transaction, demandeId, req, "Brouillon");
                await MajPrixArticlesDemande(conn, transaction, req.articlesValides);
                await MajStatutDemande(conn, transaction, demandeId, "EnTraitement");
                transaction.Commit();
                var nouveauStatut = await RecalculerStatutDemandeAsync(conn, transaction, demandeId);
                return nouveauStatut;
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task<string> SoumettreTraitementAsync(int demandeId, SoumettreTraitementRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                await UpsertTraitement(conn, transaction, demandeId, req, "Complet");
                await MajPrixArticlesDemande(conn, transaction, req.articlesValides);

                // Recalculer le montant total
                using (var cmd = new SqlCommand(@"
                    UPDATE Stock_Demandes SET
                        MontantTotal     = (SELECT ISNULL(SUM(PrixTotalLigne), 0) FROM Stock_DemandeArticles WHERE DemandeId = @Id),
                        DateModification = GETUTCDATE()
                    WHERE Id = @Id", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", demandeId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Routing central selon les 3 cas métier
                var nouveauStatut = await RecalculerStatutDemandeAsync(conn, transaction, demandeId);

                transaction.Commit();
                return nouveauStatut;
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task<string> ValiderDemandeAsync(int demandeId, ValiderDemandeRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                using (var cmd = new SqlCommand(@"
                    UPDATE Stock_Demandes SET
                        ValidateurId        = @ValidateurId,
                        NotesValidation     = @NotesValidation,
                        DateValidation      = GETUTCDATE(),
                        DateLivraisonPrevue = @DateLivraisonPrevue,
                        DateModification    = GETUTCDATE()
                    WHERE Id = @Id AND Statut = 'AttenteValidation'", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", demandeId);
                    cmd.Parameters.AddWithValue("@ValidateurId", req.ValidateurId);
                    cmd.Parameters.AddWithValue("@NotesValidation", req.NotesValidation ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateLivraisonPrevue", req.DateLivraisonPrevue ?? (object)DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Routing central selon les 3 cas métier
                // Routing central selon les 3 cas métier
                var nouveauStatut = await RecalculerStatutDemandeAsync(conn, transaction, demandeId);

                transaction.Commit();
                return nouveauStatut;
            }
            catch { transaction.Rollback(); throw; }
        }

        // ============================================================
        // RECALCUL CENTRAL DU STATUT — 3 cas métier
        // ============================================================

        /// <summary>
        /// Évalue l'état réel de tous les articles de la demande et choisit
        /// le statut approprié selon 3 cas :
        ///
        ///   Cas 1 — Tous Source='Stock' ET tous EstLivre=1
        ///       → "Dotee" : le demandeur a été doté, flux terminé.
        ///
        ///   Cas 2 — Mix Stock (livrés) + Commande (non encore reçus)
        ///       → "LivraisonPartielle" : stock doté, reste en attente fournisseur.
        ///         Le flux continue : LivraisonPartielle → AttenteValidation
        ///                                               → AttenteLivraison → Livree → AttenteComptabilite
        ///
        ///   Cas 3 — Tous Source='Commande' (ou aucun article livré)
        ///       → "AttenteValidation" : flux classique validation → livraison fournisseur.
        ///
        /// Appelée après SoumettreTraitement, ValiderDemande et LivraisonDirecte.
        /// </summary>
        private async Task<string> RecalculerStatutDemandeAsync(
      SqlConnection conn, SqlTransaction transaction, int demandeId)
        {
            // Lire le statut actuel en premier — utilisé pour router le Cas 2
            var statutActuel = await GetStatutActuelAsync(conn, transaction, demandeId);

            // Compter les articles par Source et état de livraison
            int articlesStock, articlesCommande, articlesStockLivres, articlesCommandeLivres;

            using (var cmd = new SqlCommand(@"
        SELECT
            SUM(CASE WHEN Source = 'Stock'                             THEN 1 ELSE 0 END) AS NbStock,
            SUM(CASE WHEN Source = 'Commande' OR Source='CommandeReste' THEN 1 ELSE 0 END) AS NbCommande,
            SUM(CASE WHEN Source = 'Stock'    AND EstLivre = 1         THEN 1 ELSE 0 END) AS NbStockLivres,
            SUM(CASE WHEN (Source='Commande'  OR Source='CommandeReste')
                      AND EstLivre = 1                                 THEN 1 ELSE 0 END) AS NbCommandeLivres
        FROM Stock_DemandeArticles
        WHERE DemandeId = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", demandeId);
                using var r = await cmd.ExecuteReaderAsync();
                await r.ReadAsync();
                articlesStock = r.GetInt32("NbStock");
                articlesCommande = r.GetInt32("NbCommande");
                articlesStockLivres = r.GetInt32("NbStockLivres");
                articlesCommandeLivres = r.GetInt32("NbCommandeLivres");
            }

            string nouveauStatut;
            DateTime? dateLivraison = null;

            bool tousStock = articlesStock > 0 && articlesCommande == 0;
            bool tousStockLivres = tousStock && articlesStockLivres == articlesStock;
            bool mixStockLivresOnly = articlesStock > 0 && articlesCommande > 0
                                      && articlesStockLivres == articlesStock;
            bool aucunLivre = articlesStockLivres == 0 && articlesCommandeLivres == 0;

            if (tousStockLivres)
            {
                // Cas 1 — Tout stock livré → Dotee (flux terminé)
                nouveauStatut = "Dotee";
                dateLivraison = DateTime.UtcNow;
            }
            else if (mixStockLivresOnly)
            {
                // Cas 2 — Stock livré + commandes en attente
                // Flux : LivraisonPartielle → AttenteValidation → AttenteLivraison → (livraison fournisseur)
                nouveauStatut = statutActuel switch
                {
                    "LivraisonPartielle" => "AttenteValidation",
                    "AttenteValidation" => "AttenteLivraison",
                    _ => "LivraisonPartielle"  // EnAttente / EnTraitement
                };
            }
            else if (articlesCommande > 0 && aucunLivre)
            {
                // Cas 3 — Tout en commande → AttenteValidation classique
                nouveauStatut = "AttenteValidation";
            }
            else
            {
                // En phase de livraison fournisseur (AttenteLivraison) :
                // si tous les articles (stock + commande) sont désormais livrés → AttenteComptabilite
                int totalTous = articlesStock + articlesCommande;
                int livresTous = articlesStockLivres + articlesCommandeLivres;
                if (totalTous > 0 && livresTous == totalTous && statutActuel == "AttenteLivraison")
                {
                    nouveauStatut = "AttenteComptabilite";
                    dateLivraison = DateTime.UtcNow;
                    await MajStatutDemande(conn, transaction, demandeId, nouveauStatut, dateLivraison);
                    return nouveauStatut;
                }
                // AttenteComptabilite, Livree, ou AttenteLivraison encore en cours → inchangé
                return statutActuel;
            }

            await MajStatutDemande(conn, transaction, demandeId, nouveauStatut, dateLivraison);
            return nouveauStatut;
        }
        private async Task<string> GetStatutActuelAsync(
            SqlConnection conn, SqlTransaction transaction, int demandeId)
        {
            using var cmd = new SqlCommand(
                "SELECT Statut FROM Stock_Demandes WHERE Id = @Id", conn, transaction);
            cmd.Parameters.AddWithValue("@Id", demandeId);
            return (string)(await cmd.ExecuteScalarAsync())!;
        }

        public async Task RejeterDemandeAsync(int demandeId, RejeterDemandeRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Stock_Demandes SET Statut = 'Rejetee', ValidateurId = @ValidateurId,
                    NotesValidation = @MotifRejet, DateValidation = GETUTCDATE(), DateModification = GETUTCDATE()
                WHERE Id = @Id AND Statut = 'AttenteValidation'", conn);
            cmd.Parameters.AddWithValue("@Id", demandeId);
            cmd.Parameters.AddWithValue("@ValidateurId", req.ValidateurId);
            cmd.Parameters.AddWithValue("@MotifRejet", req.MotifRejet);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ============================================================
        // LIVRAISON — Confirmation article par article
        // ============================================================

        /// <summary>
        /// Confirme la réception d'un article de commande (livraison fournisseur).
        ///
        /// Flux de mouvements (si la demande a un DepotDemandeId et l'article un ArticleId) :
        ///   M1 — Entrée dans le dépôt par défaut   (réception fournisseur)
        ///   M2 — Sortie du dépôt par défaut         (transfert vers dépôt demande)
        ///   M3 — Entrée dans le dépôt de la demande (article disponible pour le demandeur)
        ///
        /// Si la demande n'a pas de DepotDemandeId ou si l'article est hors-catalogue,
        /// les mouvements sont omis mais l'article est quand même marqué livré.
        ///
        /// Si tous les articles sont livrés, passe la demande à AttenteComptabilite.
        /// </summary>
        public async Task<ConfirmerLivraisonArticleResultDTO> ConfirmerLivraisonArticleAsync(
            int demandeArticleId, int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                // ── 1. Charger l'article et sa demande ────────────────────────────
                int demandeId;
                int? articleId;
                decimal quantiteValidee;
                using (var cmd = new SqlCommand(@"
                    SELECT da.DemandeId, da.ArticleId, ISNULL(da.QuantiteValidee, da.QuantiteDemandee) AS Qte
                    FROM Stock_DemandeArticles da
                    WHERE da.Id = @Id
                      AND (da.Source = 'Commande' OR da.Source = 'CommandeReste')
                      AND da.EstLivre = 0", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", demandeArticleId);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (!await r.ReadAsync())
                        throw new InvalidOperationException("Article introuvable, déjà livré, ou source incorrecte.");
                    demandeId = r.GetInt32("DemandeId");
                    articleId = r.IsDBNull("ArticleId") ? (int?)null : r.GetInt32("ArticleId");
                    quantiteValidee = r.GetDecimal("Qte");
                }

                // ── 2. Marquer l'article comme livré ──────────────────────────────
                using (var cmd = new SqlCommand(@"
                    UPDATE Stock_DemandeArticles SET
                        EstLivre                  = 1,
                        UserValidationLivraisonId = @UserId,
                        DateLivraisonConfirmee    = GETUTCDATE()
                    WHERE Id = @Id", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", demandeArticleId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // ── 3. Mouvements de stock (si article catalogue + dépôt demande défini) ──
                int? mouvM1 = null, mouvM2 = null, mouvM3 = null;
                int? depotDefautId = null;
                string? depotDefautNom = null;

                var depotDemandeId = await GetDepotDemandeIdAsync(conn, transaction, demandeId);

                if (articleId.HasValue && depotDemandeId.HasValue)
                {
                    (depotDefautId, depotDefautNom) = await GetDepotParDefautAsync(conn, transaction);

                    // M1 — Entrée dans le dépôt par défaut (réception fournisseur)
                    var qAvantDefaut = await GetOuInitialiserInventaire(conn, transaction, articleId.Value, depotDefautId.Value);
                    mouvM1 = await InsererMouvementReturnIdAsync(conn, transaction, new StockMouvement
                    {
                        ArticleId = articleId.Value,
                        DepotId = depotDefautId.Value,
                        TypeMouvement = "Entree",
                        Quantite = quantiteValidee,
                        QuantiteAvant = qAvantDefaut,
                        QuantiteApres = qAvantDefaut + quantiteValidee,
                        DemandeId = demandeId,
                        MotifSortie = null,
                        Notes = "Réception fournisseur — en transit vers dépôt demande",
                        OperateurId = userId,
                    });
                    // Inventaire dépôt par défaut : +quantite (entrée)
                    await MajInventaire(conn, transaction, articleId.Value, depotDefautId.Value,
                        qAvantDefaut + quantiteValidee, null);

                    // M2 — Sortie du dépôt par défaut (transfert vers dépôt demande)
                    var qApresDefautSortie = qAvantDefaut; // net = 0 sur le dépôt par défaut
                    mouvM2 = await InsererMouvementReturnIdAsync(conn, transaction, new StockMouvement
                    {
                        ArticleId = articleId.Value,
                        DepotId = depotDefautId.Value,
                        TypeMouvement = "Sortie",
                        Quantite = quantiteValidee,
                        QuantiteAvant = qAvantDefaut + quantiteValidee,
                        QuantiteApres = qApresDefautSortie,
                        DemandeId = demandeId,
                        DepotDestinationId = depotDemandeId,
                        MotifSortie = "Transfert vers dépôt demande",
                        Notes = $"Transfert vers dépôt Id={depotDemandeId}",
                        OperateurId = userId,
                    });
                    // Inventaire dépôt par défaut : revient à qAvantDefaut (sortie annule l'entrée)
                    await MajInventaire(conn, transaction, articleId.Value, depotDefautId.Value,
                        qApresDefautSortie, null);

                    // M3 — Entrée dans le dépôt de la demande (article disponible pour le demandeur)
                    var qAvantDemande = await GetOuInitialiserInventaire(conn, transaction, articleId.Value, depotDemandeId.Value);
                    mouvM3 = await InsererMouvementReturnIdAsync(conn, transaction, new StockMouvement
                    {
                        ArticleId = articleId.Value,
                        DepotId = depotDemandeId.Value,
                        TypeMouvement = "Entree",
                        Quantite = quantiteValidee,
                        QuantiteAvant = qAvantDemande,
                        QuantiteApres = qAvantDemande + quantiteValidee,
                        DemandeId = demandeId,
                        Notes = "Dotation effective — article mis à disposition du demandeur",
                        OperateurId = userId,
                    });
                    // Inventaire dépôt demande : +quantite
                    await MajInventaire(conn, transaction, articleId.Value, depotDemandeId.Value,
                        qAvantDemande + quantiteValidee, null);
                }

                // ── 4. Recalculer le statut de la demande ─────────────────────────
                var nouveauStatut = await RecalculerStatutDemandeAsync(conn, transaction, demandeId);

                transaction.Commit();
                return new ConfirmerLivraisonArticleResultDTO
                {
                    TousLivres = nouveauStatut == "AttenteComptabilite",
                    NouveauStatut = nouveauStatut,
                    MouvementEntreeDepotDefautId = mouvM1,
                    MouvementSortieDepotDefautId = mouvM2,
                    MouvementEntreeDepotDemandeId = mouvM3,
                    DepotParDefautId = depotDefautId,
                    DepotParDefautNom = depotDefautNom,
                    Message = nouveauStatut == "AttenteComptabilite"
                        ? "Article confirmé. Tous les articles sont livrés — demande passée en attente de comptabilité."
                        : "Réception confirmée. Article transféré vers le dépôt de la demande."
                };
            }
            catch { transaction.Rollback(); throw; }
        }

        /// <summary>
        /// Confirme la réception de TOUS les articles en commande non encore livrés.
        /// Crée les 3 mouvements (entrée dépôt par défaut, sortie, entrée dépôt demande)
        /// pour chaque article catalogue si la demande a un DepotDemandeId.
        /// Passe automatiquement la demande à AttenteComptabilite.
        /// </summary>
        public async Task ConfirmerToutesLivraisonsAsync(int demandeId, int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                // Guard : demande en AttenteLivraison
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Stock_Demandes WHERE Id = @Id AND Statut = 'AttenteLivraison'",
                    conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", demandeId);
                    if ((int)(await cmd.ExecuteScalarAsync())! == 0)
                        throw new InvalidOperationException("La demande n'est pas en attente de livraison.");
                }

                // Charger les articles Commande non livrés
                var articles = new List<(int Id, int? ArticleId, decimal Qte)>();
                using (var cmd = new SqlCommand(@"
                    SELECT Id, ArticleId, ISNULL(QuantiteValidee, QuantiteDemandee) AS Qte
                    FROM Stock_DemandeArticles
                    WHERE DemandeId = @DemandeId
                      AND (Source = 'Commande' OR Source = 'CommandeReste')
                      AND EstLivre = 0", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@DemandeId", demandeId);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        articles.Add((
                            r.GetInt32("Id"),
                            r.IsDBNull("ArticleId") ? (int?)null : r.GetInt32("ArticleId"),
                            r.GetDecimal("Qte")
                        ));
                }

                // Préparer dépôts (une seule fois pour tous les articles)
                var depotDemandeId = await GetDepotDemandeIdAsync(conn, transaction, demandeId);
                int? depotDefautId = null;
                if (depotDemandeId.HasValue)
                {
                    var (dId, _) = await GetDepotParDefautAsync(conn, transaction);
                    depotDefautId = dId;
                }

                foreach (var (artLineId, articleId, qte) in articles)
                {
                    // Marquer livré
                    using (var cmd = new SqlCommand(@"
                        UPDATE Stock_DemandeArticles SET
                            EstLivre = 1, UserValidationLivraisonId = @UserId, DateLivraisonConfirmee = GETUTCDATE()
                        WHERE Id = @Id", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", artLineId);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 3 mouvements si article catalogue + dépôt demande configuré
                    if (articleId.HasValue && depotDemandeId.HasValue && depotDefautId.HasValue)
                    {
                        var qAvantD = await GetOuInitialiserInventaire(conn, transaction, articleId.Value, depotDefautId.Value);
                        // M1 — Entrée dépôt par défaut
                        await InsererMouvementReturnIdAsync(conn, transaction, new StockMouvement
                        {
                            ArticleId = articleId.Value,
                            DepotId = depotDefautId.Value,
                            TypeMouvement = "Entree",
                            Quantite = qte,
                            QuantiteAvant = qAvantD,
                            QuantiteApres = qAvantD + qte,
                            DemandeId = demandeId,
                            Notes = "Réception fournisseur (confirmation masse)",
                            OperateurId = userId,
                        });
                        await MajInventaire(conn, transaction, articleId.Value, depotDefautId.Value, qAvantD + qte, null);

                        // M2 — Sortie dépôt par défaut
                        await InsererMouvementReturnIdAsync(conn, transaction, new StockMouvement
                        {
                            ArticleId = articleId.Value,
                            DepotId = depotDefautId.Value,
                            TypeMouvement = "Sortie",
                            Quantite = qte,
                            QuantiteAvant = qAvantD + qte,
                            QuantiteApres = qAvantD,
                            DemandeId = demandeId,
                            DepotDestinationId = depotDemandeId,
                            MotifSortie = "Transfert vers dépôt demande",
                            OperateurId = userId,
                        });
                        await MajInventaire(conn, transaction, articleId.Value, depotDefautId.Value, qAvantD, null);

                        // M3 — Entrée dépôt demande
                        var qAvantDem = await GetOuInitialiserInventaire(conn, transaction, articleId.Value, depotDemandeId.Value);
                        await InsererMouvementReturnIdAsync(conn, transaction, new StockMouvement
                        {
                            ArticleId = articleId.Value,
                            DepotId = depotDemandeId.Value,
                            TypeMouvement = "Entree",
                            Quantite = qte,
                            QuantiteAvant = qAvantDem,
                            QuantiteApres = qAvantDem + qte,
                            DemandeId = demandeId,
                            Notes = "Dotation effective — confirmation masse",
                            OperateurId = userId,
                        });
                        await MajInventaire(conn, transaction, articleId.Value, depotDemandeId.Value, qAvantDem + qte, null);
                    }
                }

                await MajStatutDemande(conn, transaction, demandeId, "AttenteComptabilite", DateTime.UtcNow);
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        /// <summary>
        /// Finalise manuellement la livraison (bouton du sheet) après que l'utilisateur
        /// a confirmé tous les articles un par un. Guard : tous les articles doivent être EstLivre=1.
        /// </summary>
        public async Task FinaliserLivraisonAsync(int demandeId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                // Guard : articles non livrés ?
                int nonLivres;
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Stock_DemandeArticles WHERE DemandeId = @Id AND EstLivre = 0",
                    conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", demandeId);
                    nonLivres = (int)(await cmd.ExecuteScalarAsync())!;
                }
                if (nonLivres > 0)
                    throw new InvalidOperationException(
                        $"Impossible de finaliser : {nonLivres} article(s) non confirmé(s).");

                // Vérification statut
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Stock_Demandes WHERE Id = @Id AND Statut IN ('AttenteLivraison','LivraisonPartielle')",
                    conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", demandeId);
                    if ((int)(await cmd.ExecuteScalarAsync())! == 0)
                        throw new InvalidOperationException("La demande n'est pas en attente de livraison.");
                }

                await MajStatutDemande(conn, transaction, demandeId, "AttenteComptabilite", DateTime.UtcNow);
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        // ============================================================
        // LIVRAISON DIRECTE — bouton "Livré" du formulaire de traitement
        // ============================================================

        /// <summary>
        /// Livraison directe depuis le formulaire de traitement (dotation effective).
        /// AUTONOME : ne dépend d'aucun appel préalable à SauvegarderTraitement.
        ///
        /// Mouvements créés :
        ///   M1 — Sortie du dépôt source (prélèvement du stock gestionnaire)
        ///   M2 — Entrée dans le dépôt de la demande (dotation effective, si DepotDemandeId défini)
        ///
        /// En phase traitement (EnAttente/EnTraitement) : ne modifie pas le statut principal.
        /// En phase post-validation : recalcule via RecalculerStatutDemandeAsync.
        /// </summary>
        public async Task<LivraisonDirecteResponse> LivraisonDirecteAsync(
            int demandeId, int demandeArticleId, int depotSourceId, decimal quantite, int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                // ── 1. Charger la ligne article ───────────────────────────────────
                int articleId;
                using (var cmd = new SqlCommand(@"
                    SELECT da.ArticleId, da.Source
                    FROM Stock_DemandeArticles da
                    WHERE da.Id = @ArtId AND da.DemandeId = @DemandeId", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@ArtId", demandeArticleId);
                    cmd.Parameters.AddWithValue("@DemandeId", demandeId);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (!await r.ReadAsync())
                        throw new InvalidOperationException("Ligne article introuvable dans cette demande.");
                    if (r.IsDBNull("ArticleId"))
                        throw new InvalidOperationException(
                            "Cet article est une désignation libre (hors catalogue) : sortie de stock impossible.");
                    articleId = r.GetInt32("ArticleId");
                }

                // ── 2. Vérifier disponibilité stock dans le dépôt source ──────────
                var qAvantSource = await GetOuInitialiserInventaire(conn, transaction, articleId, depotSourceId);
                if (qAvantSource < quantite)
                    throw new InvalidOperationException(
                        $"Stock insuffisant dans le dépôt (disponible : {qAvantSource}, demandé : {quantite}).");

                // ── 3. Upsert dotation ────────────────────────────────────────────
                int dotationId;
                using (var cmd = new SqlCommand(@"
                    DECLARE @Id INT;
                    SELECT @Id = Id FROM Stock_DemandeArticleDotations
                    WHERE DemandeArticleId = @ArtId AND DepotId = @DepotId AND EstLivre = 0;
                    IF @Id IS NOT NULL
                    BEGIN
                        UPDATE Stock_DemandeArticleDotations SET QuantiteDotee = @Qte WHERE Id = @Id;
                        SELECT @Id;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO Stock_DemandeArticleDotations
                            (DemandeArticleId, DepotId, QuantiteDotee, EstLivre, DateCreation)
                        VALUES (@ArtId, @DepotId, @Qte, 0, GETUTCDATE());
                        SELECT SCOPE_IDENTITY();
                    END", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@ArtId", demandeArticleId);
                    cmd.Parameters.AddWithValue("@DepotId", depotSourceId);
                    cmd.Parameters.AddWithValue("@Qte", quantite);
                    dotationId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // Mettre à jour la ligne article : Source='Stock', dépôt et quantité
                using (var cmd = new SqlCommand(@"
                    UPDATE Stock_DemandeArticles SET
                        Source          = 'Stock',
                        DepotDotationId = @DepotId,
                        QuantiteDotee   = ISNULL(QuantiteDotee, 0) + @Qte
                    WHERE Id = @ArtId", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@ArtId", demandeArticleId);
                    cmd.Parameters.AddWithValue("@DepotId", depotSourceId);
                    cmd.Parameters.AddWithValue("@Qte", quantite);
                    await cmd.ExecuteNonQueryAsync();
                }

                // ── 4. M1 — Sortie du dépôt source ───────────────────────────────
                var qApresSource = qAvantSource - quantite;

                // Obtenir le DepotDemandeId avant de créer les mouvements
                var depotDemandeId = await GetDepotDemandeIdAsync(conn, transaction, demandeId);

                var mouvSortieId = await InsererMouvementReturnIdAsync(conn, transaction, new StockMouvement
                {
                    ArticleId = articleId,
                    DepotId = depotSourceId,
                    TypeMouvement = "Sortie",
                    Quantite = quantite,
                    QuantiteAvant = qAvantSource,
                    QuantiteApres = qApresSource,
                    DemandeId = demandeId,
                    DepotDestinationId = depotDemandeId,
                    MotifSortie = depotDemandeId.HasValue
                        ? "Dotation demande (transfert vers dépôt demande)"
                        : "Dotation demande (livraison directe)",
                    OperateurId = userId,
                });

                // ── 5. Décrémenter inventaire dépôt source ────────────────────────
                await MajInventaire(conn, transaction, articleId, depotSourceId, qApresSource, null);

                // ── 6. M2 — Entrée dans le dépôt de la demande (dotation effective) ──
                int? mouvEntreeDemandeId = null;
                if (depotDemandeId.HasValue && depotDemandeId.Value != depotSourceId)
                {
                    var qAvantDemande = await GetOuInitialiserInventaire(conn, transaction, articleId, depotDemandeId.Value);
                    mouvEntreeDemandeId = await InsererMouvementReturnIdAsync(conn, transaction, new StockMouvement
                    {
                        ArticleId = articleId,
                        DepotId = depotDemandeId.Value,
                        TypeMouvement = "Entree",
                        Quantite = quantite,
                        QuantiteAvant = qAvantDemande,
                        QuantiteApres = qAvantDemande + quantite,
                        DemandeId = demandeId,
                        Notes = "Dotation effective — article mis à disposition du demandeur",
                        OperateurId = userId,
                    });
                    await MajInventaire(conn, transaction, articleId, depotDemandeId.Value,
                        qAvantDemande + quantite, null);
                }

                // ── 7. Marquer la dotation livrée + lier mouvement sortie ─────────
                using (var cmd = new SqlCommand(@"
                    UPDATE Stock_DemandeArticleDotations SET
                        EstLivre               = 1,
                        UserValidationId       = @UserId,
                        DateLivraisonConfirmee = GETUTCDATE(),
                        MouvementId            = @MouvementId
                    WHERE Id = @DotId", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@DotId", dotationId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@MouvementId", mouvSortieId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // ── 8. Marquer l'article livré si toutes ses dotations sont livrées ──
                int dotationsNonLivrees;
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Stock_DemandeArticleDotations WHERE DemandeArticleId = @ArtId AND EstLivre = 0",
                    conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@ArtId", demandeArticleId);
                    dotationsNonLivrees = (int)(await cmd.ExecuteScalarAsync())!;
                }
                if (dotationsNonLivrees == 0)
                {
                    using var cmd = new SqlCommand(@"
                        UPDATE Stock_DemandeArticles SET
                            EstLivre                  = 1,
                            UserValidationLivraisonId = @UserId,
                            DateLivraisonConfirmee    = GETUTCDATE()
                        WHERE Id = @ArtId", conn, transaction);
                    cmd.Parameters.AddWithValue("@ArtId", demandeArticleId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // ── 9. Recalculer le statut selon la phase ────────────────────────
                var statutActuel = await GetStatutActuelAsync(conn, transaction, demandeId);
                bool enPhaseTraitement = statutActuel == "EnAttente" || statutActuel == "EnTraitement";

                int totalArticles, articlesLivres;
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Stock_DemandeArticles WHERE DemandeId = @Did", conn, transaction))
                { cmd.Parameters.AddWithValue("@Did", demandeId); totalArticles = (int)(await cmd.ExecuteScalarAsync())!; }
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Stock_DemandeArticles WHERE DemandeId = @Did AND EstLivre = 1", conn, transaction))
                { cmd.Parameters.AddWithValue("@Did", demandeId); articlesLivres = (int)(await cmd.ExecuteScalarAsync())!; }

                string nouveauStatut = statutActuel;
                if (!enPhaseTraitement)
                    nouveauStatut = await RecalculerStatutDemandeAsync(conn, transaction, demandeId);

                transaction.Commit();

                return new LivraisonDirecteResponse
                {
                    MouvementSortieId = mouvSortieId,
                    MouvementEntreeDepotDemandeId = mouvEntreeDemandeId,
                    StockApresSource = qApresSource,
                    TousLivres = articlesLivres == totalArticles,
                    NouveauStatut = nouveauStatut,
                    Message = articlesLivres == totalArticles && enPhaseTraitement
                        ? "Tous les articles sont dotés depuis le stock — vous pouvez soumettre."
                        : articlesLivres == totalArticles
                            ? "Tous les articles sont livrés. Demande passée en attente de comptabilité."
                            : $"Dotation effectuée ({articlesLivres}/{totalArticles} articles livrés)."
                };
            }
            catch { transaction.Rollback(); throw; }
        }
        // ============================================================
        // MOUVEMENTS — avec pagination + recherche
        // ============================================================

        // ============================================================
        // DÉTAIL INVENTAIRE FIFO — lots d'entrée triés par date
        // ============================================================

        /// <summary>
        /// Retourne le détail FIFO de l'inventaire pour un article dans un dépôt :
        /// liste des lots d'entrée encore disponibles (QuantiteRestante > 0),
        /// triés par DateCreation ASC (le plus ancien en premier = prochain à sortir).
        /// </summary>
        public async Task<DetailsInventaireDTO?> GetDetailsInventaireAsync(int articleId, int depotId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // ── En-tête article + dépôt + stock actuel ────────────────────
            DetailsInventaireDTO? result = null;
            using (var cmd = new SqlCommand(@"
                SELECT
                    a.Id AS ArticleId, a.Nom AS ArticleNom, a.Reference AS ArticleRef,
                    a.Unite,
                    d.Id AS DepotId, d.Nom AS DepotNom,
                    ISNULL(i.QuantiteDisponible, 0) AS QuantiteDisponible
                FROM Stock_Articles a
                JOIN Stock_Depots d ON d.Id = @DepotId
                LEFT JOIN Stock_Inventaire i
                    ON i.ArticleId = a.Id AND i.DepotId = @DepotId
                WHERE a.Id = @ArticleId", conn))
            {
                cmd.Parameters.AddWithValue("@ArticleId", articleId);
                cmd.Parameters.AddWithValue("@DepotId", depotId);
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return null;
                result = new DetailsInventaireDTO
                {
                    ArticleId = r.GetInt32("ArticleId"),
                    ArticleNom = r.GetString("ArticleNom"),
                    ArticleReference = r.GetString("ArticleRef"),
                    Unite = r.GetString("Unite"),
                    DepotId = r.GetInt32("DepotId"),
                    DepotNom = r.GetString("DepotNom"),
                    QuantiteDisponible = r.GetDecimal("QuantiteDisponible"),
                };
            }

            // ── Lots FIFO disponibles ──────────────────────────────────────
            using (var cmd = new SqlCommand(@"
                SELECT
                    l.Id, l.QuantiteEntree, l.QuantiteRestante,
                    l.PrixUnitaire, l.Reference, l.Notes, l.DateCreation,
                    l.MouvementEntreeId
                FROM Stock_LotEntrees l
                WHERE l.ArticleId      = @ArticleId
                  AND l.DepotId        = @DepotId
                  AND l.QuantiteRestante > 0
                ORDER BY l.DateCreation ASC", conn))
            {
                cmd.Parameters.AddWithValue("@ArticleId", articleId);
                cmd.Parameters.AddWithValue("@DepotId", depotId);
                using var r = await cmd.ExecuteReaderAsync();
                int rang = 1;
                while (await r.ReadAsync())
                {
                    var lot = new LotEntreeDTO
                    {
                        Id = r.GetInt32("Id"),
                        ArticleId = articleId,
                        ArticleNom = result!.ArticleNom,
                        ArticleReference = result.ArticleReference,
                        DepotId = depotId,
                        DepotNom = result.DepotNom,
                        MouvementEntreeId = r.IsDBNull("MouvementEntreeId") ? null : r.GetInt32("MouvementEntreeId"),
                        QuantiteEntree = r.GetDecimal("QuantiteEntree"),
                        QuantiteRestante = r.GetDecimal("QuantiteRestante"),
                        PrixUnitaire = r.IsDBNull("PrixUnitaire") ? null : r.GetDecimal("PrixUnitaire"),
                        Reference = r.IsDBNull("Reference") ? null : r.GetString("Reference"),
                        Notes = r.IsDBNull("Notes") ? null : r.GetString("Notes"),
                        DateCreation = r.GetDateTime("DateCreation"),
                        RangFifo = rang++,
                    };
                    result!.Lots.Add(lot);
                }
            }

            // Synthèse
            result!.QuantiteTotaleLots = result.Lots.Sum(l => l.QuantiteRestante);
            result.ValeurTotaleEstimee = result.Lots.Any(l => l.PrixUnitaire.HasValue)
                ? result.Lots.Where(l => l.PrixUnitaire.HasValue)
                    .Sum(l => l.QuantiteRestante * l.PrixUnitaire!.Value)
                : null;

            return result;
        }

        // ============================================================
        // MOUVEMENTS — Entrée, Sortie FIFO, Transfert
        // ============================================================

        public async Task EnregistrerEntreeAsync(EnregistrerEntreeRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                var qAvant = await GetOuInitialiserInventaire(conn, transaction, req.ArticleId, req.DepotId);
                var qApres = qAvant + req.Quantite;

                // ── Mouvement standard ─────────────────────────────────────
                int mouvementId;
                using (var cmd = new SqlCommand(@"
                    INSERT INTO Stock_Mouvements (ArticleId, DepotId, TypeMouvement, Quantite,
                        QuantiteAvant, QuantiteApres, PrixUnitaire, MontantTotal, Reference,
                        DemandeId, ProjetId, EtapeProjetId, DepotDestinationId, MotifSortie,
                        OperateurId, DateMouvement, Notes, DateCreation)
                    VALUES (@ArticleId, @DepotId, 'Entree', @Quantite,
                        @QAvant, @QApres, @PrixUnitaire, @MontantTotal, @Reference,
                        @DemandeId, NULL, NULL, NULL, NULL,
                        @OperateurId, GETUTCDATE(), @Notes, GETUTCDATE());
                    SELECT SCOPE_IDENTITY();", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@ArticleId", req.ArticleId);
                    cmd.Parameters.AddWithValue("@DepotId", req.DepotId);
                    cmd.Parameters.AddWithValue("@Quantite", req.Quantite);
                    cmd.Parameters.AddWithValue("@QAvant", qAvant);
                    cmd.Parameters.AddWithValue("@QApres", qApres);
                    cmd.Parameters.AddWithValue("@PrixUnitaire", req.PrixUnitaire ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MontantTotal", req.PrixUnitaire.HasValue
                        ? (object)(req.PrixUnitaire.Value * req.Quantite) : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Reference", req.Reference ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DemandeId", req.DemandeId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@OperateurId", req.OperateurId);
                    cmd.Parameters.AddWithValue("@Notes", req.Notes ?? (object)DBNull.Value);
                    mouvementId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // ── Créer le lot FIFO ──────────────────────────────────────
                using (var cmd = new SqlCommand(@"
                    INSERT INTO Stock_LotEntrees
                        (ArticleId, DepotId, MouvementEntreeId, QuantiteEntree, QuantiteRestante,
                         PrixUnitaire, Reference, Notes, DateCreation)
                    VALUES
                        (@ArticleId, @DepotId, @MouvId, @Qte, @Qte,
                         @Prix, @Ref, @Notes, GETUTCDATE())", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@ArticleId", req.ArticleId);
                    cmd.Parameters.AddWithValue("@DepotId", req.DepotId);
                    cmd.Parameters.AddWithValue("@MouvId", mouvementId);
                    cmd.Parameters.AddWithValue("@Qte", req.Quantite);
                    cmd.Parameters.AddWithValue("@Prix", req.PrixUnitaire ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Ref", req.Reference ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Notes", req.Notes ?? (object)DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                await MajInventaire(conn, transaction, req.ArticleId, req.DepotId, qApres, null);

                if (req.PrixUnitaire.HasValue && req.PrixUnitaire > 0)
                    await MajPrixUnitaireMoyen(conn, transaction, req.ArticleId, req.Quantite, req.PrixUnitaire.Value);

                if (req.DemandeId.HasValue)
                    await MajStatutDemande(conn, transaction, req.DemandeId.Value, "Livre", DateTime.UtcNow);

                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task<SortieStockResultDTO> EnregistrerSortieAsync(EnregistrerSortieRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                // ── 1. Vérifier le stock total disponible ──────────────────
                var qAvant = await GetQuantiteInventaire(conn, transaction, req.ArticleId, req.DepotId);
                if (qAvant < req.Quantite)
                    throw new InvalidOperationException(
                        $"Stock insuffisant. Disponible : {qAvant}, Demandé : {req.Quantite}.");

                // ── 2. Charger les lots FIFO disponibles (DateCreation ASC) ─
                var lots = new List<(int Id, decimal QteRestante, decimal? Prix)>();
                using (var cmd = new SqlCommand(@"
                    SELECT Id, QuantiteRestante, PrixUnitaire
                    FROM Stock_LotEntrees
                    WHERE ArticleId       = @ArticleId
                      AND DepotId         = @DepotId
                      AND QuantiteRestante > 0
                    ORDER BY DateCreation ASC", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@ArticleId", req.ArticleId);
                    cmd.Parameters.AddWithValue("@DepotId", req.DepotId);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        lots.Add((
                            r.GetInt32("Id"),
                            r.GetDecimal("QuantiteRestante"),
                            r.IsDBNull("PrixUnitaire") ? (decimal?)null : r.GetDecimal("PrixUnitaire")
                        ));
                }

                // ── 3. Mouvement de sortie global ──────────────────────────
                var qApres = qAvant - req.Quantite;
                int mouvementId;
                using (var cmd = new SqlCommand(@"
                    INSERT INTO Stock_Mouvements (ArticleId, DepotId, TypeMouvement, Quantite,
                        QuantiteAvant, QuantiteApres, Reference, DemandeId,
                        ProjetId, EtapeProjetId, MotifSortie, OperateurId, DateMouvement, Notes, DateCreation)
                    VALUES (@ArticleId, @DepotId, 'Sortie', @Quantite,
                        @QAvant, @QApres, @Reference, @DemandeId,
                        @ProjetId, @EtapeProjetId, @MotifSortie, @OperateurId, @DateMouvement, @Notes, GETUTCDATE());
                    SELECT SCOPE_IDENTITY();", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@ArticleId", req.ArticleId);
                    cmd.Parameters.AddWithValue("@DepotId", req.DepotId);
                    cmd.Parameters.AddWithValue("@Quantite", req.Quantite);
                    cmd.Parameters.AddWithValue("@QAvant", qAvant);
                    cmd.Parameters.AddWithValue("@QApres", qApres);
                    cmd.Parameters.AddWithValue("@Reference", req.Reference ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DemandeId", req.DemandeId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ProjetId", req.ProjetId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@EtapeProjetId", req.EtapeProjetId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MotifSortie", req.MotifSortie ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@OperateurId", req.OperateurId);
                    cmd.Parameters.AddWithValue("@Notes", req.Notes ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateMouvement", req.dateMouvement);
                    mouvementId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // ── 4. Consommer les lots FIFO ─────────────────────────────
                var consommations = new List<SortieConsommationDTO>();
                var resteAConsommer = req.Quantite;

                foreach (var (lotId, qteRestante, prixLot) in lots)
                {
                    if (resteAConsommer <= 0) break;

                    var qteConsommeeLot = Math.Min(resteAConsommer, qteRestante);
                    var qteRestanteApres = qteRestante - qteConsommeeLot;

                    // Décrémenter le lot
                    using (var cmd = new SqlCommand(@"
                        UPDATE Stock_LotEntrees
                        SET QuantiteRestante = @QteRestante
                        WHERE Id = @Id", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@QteRestante", qteRestanteApres);
                        cmd.Parameters.AddWithValue("@Id", lotId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    consommations.Add(new SortieConsommationDTO
                    {
                        LotId = lotId,
                        QuantiteConsommee = qteConsommeeLot,
                        PrixUnitaireLot = prixLot,
                        QuantiteRestanteApres = qteRestanteApres,
                        // DateEntreeLot sera valorisée si besoin depuis le détail des lots
                    });

                    resteAConsommer -= qteConsommeeLot;
                }

                // ── 5. Si des lots ne couvrent pas tout (stock antérieur au FIFO) ──
                // La quantité non couverte par des lots est quand même sortie (le stock
                // global est correct — c'est juste la traçabilité FIFO qui est partielle).
                // Aucune exception : on a déjà vérifié le stock total à l'étape 1.

                // ── 6. Mettre à jour l'inventaire global ───────────────────
                await MajInventaire(conn, transaction, req.ArticleId, req.DepotId, qApres, null);

                transaction.Commit();

                return new SortieStockResultDTO
                {
                    MouvementId = mouvementId,
                    QuantiteAvant = qAvant,
                    QuantiteApres = qApres,
                    LotsConsommes = consommations,
                    Message = lots.Count == 0
                        ? "Sortie enregistrée. Aucun lot FIFO trouvé — traçabilité partielle (stock antérieur au FIFO)."
                        : $"Sortie FIFO effectuée. {consommations.Count} lot(s) consommé(s)."
                };
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task EnregistrerTransfertAsync(EnregistrerTransfertRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                var qSource = await GetQuantiteInventaire(conn, transaction, req.ArticleId, req.DepotSourceId);
                if (qSource < req.Quantite)
                    throw new InvalidOperationException($"Stock insuffisant dans le dépôt source. Disponible : {qSource}");
                await InsererMouvement(conn, transaction, new StockMouvement
                {
                    ArticleId = req.ArticleId,
                    DepotId = req.DepotSourceId,
                    TypeMouvement = "Transfert",
                    Quantite = req.Quantite,
                    QuantiteAvant = qSource,
                    QuantiteApres = qSource - req.Quantite,
                    DepotDestinationId = req.DepotDestinationId,
                    OperateurId = req.OperateurId,
                    Notes = req.Notes
                });
                await MajInventaire(conn, transaction, req.ArticleId, req.DepotSourceId, qSource - req.Quantite, null);
                var qDest = await GetOuInitialiserInventaire(conn, transaction, req.ArticleId, req.DepotDestinationId);
                await InsererMouvement(conn, transaction, new StockMouvement
                {
                    ArticleId = req.ArticleId,
                    DepotId = req.DepotDestinationId,
                    TypeMouvement = "Entree",
                    Quantite = req.Quantite,
                    QuantiteAvant = qDest,
                    QuantiteApres = qDest + req.Quantite,
                    OperateurId = req.OperateurId,
                    Notes = $"Transfert depuis dépôt #{req.DepotSourceId}"
                });
                await MajInventaire(conn, transaction, req.ArticleId, req.DepotDestinationId, qDest + req.Quantite, null);
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        // ============================================================
        // BORDEREAU D'ENTRÉE — plusieurs articles, une transaction
        // ============================================================

        /// <summary>
        /// Enregistre un bordereau d'entrée (plusieurs articles) en une seule transaction.
        /// Tout passe ou tout échoue : si une ligne est invalide, le bordereau entier est annulé.
        /// Pour chaque ligne : création du mouvement + lot FIFO + maj inventaire + prix moyen.
        /// </summary>
        public async Task<BordereauEntreeResultDTO> EnregistrerBordereauEntreeAsync(
            BordereauEntreeRequest req, int operateurId)
        {
            if (!req.Lignes.Any())
                throw new ArgumentException("Le bordereau doit contenir au moins une ligne.");

            if (req.Lignes.Any(l => l.Quantite <= 0))
                throw new ArgumentException("Toutes les quantités doivent être supérieures à 0.");

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Récupérer le nom du dépôt pour la réponse
                string depotNom;
                using (var cmd = new SqlCommand(
                    "SELECT Nom FROM Stock_Depots WHERE Id = @Id AND Actif = 1", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", req.DepotId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result == null)
                        throw new InvalidOperationException($"Dépôt {req.DepotId} introuvable ou inactif.");
                    depotNom = result.ToString()!;
                }

                var lignesResult = new List<BordereauLigneResultDTO>();
                decimal montantTotal = 0;

                foreach (var ligne in req.Lignes)
                {
                    // Récupérer le nom et la référence de l'article
                    string articleNom = "", articleRef = "";
                    using (var cmd = new SqlCommand(
                        "SELECT Nom, Reference FROM Stock_Articles WHERE Id = @Id AND Actif = 1",
                        conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", ligne.ArticleId);
                        using var r = await cmd.ExecuteReaderAsync();
                        if (!await r.ReadAsync())
                            throw new InvalidOperationException(
                                $"Article {ligne.ArticleId} introuvable ou inactif. Bordereau annulé.");
                        articleNom = r.GetString("Nom");
                        articleRef = r.GetString("Reference");
                    }

                    var qAvant = await GetOuInitialiserInventaire(conn, transaction, ligne.ArticleId, req.DepotId);
                    var qApres = qAvant + ligne.Quantite;

                    // Référence : on préfixe avec le N° de bordereau si fourni
                    var refMouvement = string.IsNullOrEmpty(req.Reference)
                        ? null
                        : req.Reference;

                    // Insérer le mouvement
                    int mouvementId;
                    using (var cmd = new SqlCommand(@"
                INSERT INTO Stock_Mouvements
                    (ArticleId, DepotId, TypeMouvement, Quantite,
                     QuantiteAvant, QuantiteApres, PrixUnitaire, MontantTotal,
                     Reference, OperateurId, DateMouvement, Notes, DateCreation)
                VALUES
                    (@ArticleId, @DepotId, 'Entree', @Quantite,
                     @QAvant, @QApres, @PrixUnitaire, @MontantTotal,
                     @Reference, @OperateurId, @DateMouvement, @Notes, GETUTCDATE());
                SELECT SCOPE_IDENTITY();", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ArticleId", ligne.ArticleId);
                        cmd.Parameters.AddWithValue("@DepotId", req.DepotId);
                        cmd.Parameters.AddWithValue("@Quantite", ligne.Quantite);
                        cmd.Parameters.AddWithValue("@QAvant", qAvant);
                        cmd.Parameters.AddWithValue("@QApres", qApres);
                        cmd.Parameters.AddWithValue("@PrixUnitaire", ligne.PrixUnitaire ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@MontantTotal", ligne.PrixUnitaire.HasValue
                            ? (object)(ligne.PrixUnitaire.Value * ligne.Quantite) : DBNull.Value);
                        cmd.Parameters.AddWithValue("@Reference", refMouvement ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@OperateurId", operateurId);
                        cmd.Parameters.AddWithValue("@Notes",
                            string.IsNullOrEmpty(ligne.Notes) ? req.Notes ?? (object)DBNull.Value
                                                              : (object)ligne.Notes);
                        cmd.Parameters.AddWithValue("@DateMouvement", req.dateMouvement);
                        mouvementId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                    }

                    // Créer le lot FIFO
                    using (var cmd = new SqlCommand(@"
                INSERT INTO Stock_LotEntrees
                    (ArticleId, DepotId, MouvementEntreeId,
                     QuantiteEntree, QuantiteRestante, PrixUnitaire, Reference, Notes, DateCreation)
                VALUES
                    (@ArticleId, @DepotId, @MouvId,
                     @Qte, @Qte, @Prix, @Ref, @Notes, GETUTCDATE())", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ArticleId", ligne.ArticleId);
                        cmd.Parameters.AddWithValue("@DepotId", req.DepotId);
                        cmd.Parameters.AddWithValue("@MouvId", mouvementId);
                        cmd.Parameters.AddWithValue("@Qte", ligne.Quantite);
                        cmd.Parameters.AddWithValue("@Prix", ligne.PrixUnitaire ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Ref", refMouvement ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Notes", ligne.Notes ?? (object)DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Mettre à jour l'inventaire
                    await MajInventaire(conn, transaction, ligne.ArticleId, req.DepotId, qApres, null);

                    // Mettre à jour le prix moyen
                    if (ligne.PrixUnitaire.HasValue && ligne.PrixUnitaire > 0)
                        await MajPrixUnitaireMoyen(conn, transaction, ligne.ArticleId,
                            ligne.Quantite, ligne.PrixUnitaire.Value);

                    if (ligne.PrixUnitaire.HasValue)
                        montantTotal += ligne.PrixUnitaire.Value * ligne.Quantite;

                    lignesResult.Add(new BordereauLigneResultDTO
                    {
                        ArticleId = ligne.ArticleId,
                        ArticleNom = articleNom,
                        ArticleReference = articleRef,
                        Quantite = ligne.Quantite,
                        PrixUnitaire = ligne.PrixUnitaire,
                        MouvementId = mouvementId,
                        QuantiteAvant = qAvant,
                        QuantiteApres = qApres,
                        Succes = true,
                    });
                }

                transaction.Commit();

                return new BordereauEntreeResultDTO
                {
                    Reference = req.Reference,
                    DepotId = req.DepotId,
                    DepotNom = depotNom,
                    NbLignesTotal = req.Lignes.Count,
                    NbLignesReussies = lignesResult.Count,
                    MontantTotalEntre = montantTotal,
                    Lignes = lignesResult,
                    Message = $"Bordereau d'entrée enregistré : {lignesResult.Count} article(s) réceptionnés."
                };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ============================================================
        // BORDEREAU DE SORTIE — plusieurs articles, traitement FIFO ligne par ligne
        // ============================================================

        /// <summary>
        /// Enregistre un bordereau de sortie (plusieurs articles).
        /// Traitement ligne par ligne avec la logique FIFO existante.
        /// Permet le succès partiel : les lignes en erreur (stock insuffisant…)
        /// sont signalées dans la réponse sans bloquer les autres.
        /// </summary>
        public async Task<BordereauSortieResultDTO> EnregistrerBordereauSortieAsync(
            BordereauSortieRequest req, int operateurId)
        {
            if (!req.Lignes.Any())
                throw new ArgumentException("Le bordereau doit contenir au moins une ligne.");

            // Récupérer le nom du dépôt
            string depotNom;
            using (var conn0 = new SqlConnection(_connectionString))
            {
                await conn0.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT Nom FROM Stock_Depots WHERE Id = @Id AND Actif = 1", conn0);
                cmd.Parameters.AddWithValue("@Id", req.DepotId);
                var r = await cmd.ExecuteScalarAsync();
                if (r == null)
                    throw new InvalidOperationException($"Dépôt {req.DepotId} introuvable ou inactif.");
                depotNom = r.ToString()!;
            }

            var lignesResult = new List<BordereauLigneResultDTO>();

            // Traiter chaque ligne indépendamment (transaction par ligne → succès partiel)
            foreach (var ligne in req.Lignes)
            {
                // Récupérer les infos article pour la réponse
                string articleNom = $"Article #{ligne.ArticleId}", articleRef = "";
                try
                {
                    using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync();

                    using (var cmd = new SqlCommand(
                        "SELECT Nom, Reference FROM Stock_Articles WHERE Id = @Id AND Actif = 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", ligne.ArticleId);
                        using var r = await cmd.ExecuteReaderAsync();
                        if (await r.ReadAsync())
                        {
                            articleNom = r.GetString("Nom");
                            articleRef = r.GetString("Reference");
                        }
                    }

                    // Déléguer à la logique FIFO existante
                    var sortieReq = new EnregistrerSortieRequest
                    {
                        ArticleId = ligne.ArticleId,
                        DepotId = req.DepotId,
                        Quantite = ligne.Quantite,
                        Reference = req.Reference,
                        ProjetId = req.ProjetId,
                        EtapeProjetId = req.EtapeProjetId,
                        MotifSortie = req.MotifSortie,
                        OperateurId = operateurId,
                        dateMouvement = req.dateMouvement,
                        Notes = string.IsNullOrEmpty(ligne.Notes) ? req.Notes : ligne.Notes,
                    };

                    var result = await EnregistrerSortieAsync(sortieReq);

                    lignesResult.Add(new BordereauLigneResultDTO
                    {
                        ArticleId = ligne.ArticleId,
                        ArticleNom = articleNom,
                        ArticleReference = articleRef,
                        Quantite = ligne.Quantite,
                        MouvementId = result.MouvementId,
                        QuantiteAvant = result.QuantiteAvant,
                        QuantiteApres = result.QuantiteApres,
                        Succes = true,
                    });
                }
                catch (Exception ex)
                {
                    lignesResult.Add(new BordereauLigneResultDTO
                    {
                        ArticleId = ligne.ArticleId,
                        ArticleNom = articleNom,
                        ArticleReference = articleRef,
                        Quantite = ligne.Quantite,
                        Succes = false,
                        Erreur = ex.Message,
                    });
                }
            }

            var nbReussies = lignesResult.Count(l => l.Succes);
            var nbEchec = lignesResult.Count(l => !l.Succes);

            return new BordereauSortieResultDTO
            {
                Reference = req.Reference,
                DepotId = req.DepotId,
                DepotNom = depotNom,
                NbLignesTotal = req.Lignes.Count,
                NbLignesReussies = nbReussies,
                NbLignesEchec = nbEchec,
                Lignes = lignesResult,
                Message = nbEchec == 0
                    ? $"Bordereau de sortie enregistré : {nbReussies} article(s) sortis avec succès."
                    : $"{nbReussies} sortie(s) réussie(s), {nbEchec} échec(s). Vérifiez les détails."
            };
        }
        // ============================================================
        // RAPPORTS — Historique paginé avec recherche
        // ============================================================

        public async Task<List<HistoriqueMouvementDTO>> GetHistoriqueAsync(
            DateTime? dateDebut = null, DateTime? dateFin = null,
            int? articleId = null, int? depotId = null, string? typeMouvement = null)
        {
            var list = new List<HistoriqueMouvementDTO>();
            using var conn = new SqlConnection(_connectionString);
            var sql = "SELECT * FROM v_Stock_HistoriqueMouvements WHERE 1=1";
            if (dateDebut.HasValue) sql += " AND DateMouvement >= @DateDebut";
            if (dateFin.HasValue) sql += " AND DateMouvement <= @DateFin";
            if (articleId.HasValue) sql += " AND ArticleId = @ArticleId";
            if (depotId.HasValue) sql += " AND DepotId = @DepotId";
            if (!string.IsNullOrEmpty(typeMouvement)) sql += " AND TypeMouvement = @TypeMouvement";
            sql += " ORDER BY DateMouvement DESC";
            using var cmd = new SqlCommand(sql, conn);
            if (dateDebut.HasValue) cmd.Parameters.AddWithValue("@DateDebut", dateDebut.Value);
            if (dateFin.HasValue) cmd.Parameters.AddWithValue("@DateFin", dateFin.Value);
            if (articleId.HasValue) cmd.Parameters.AddWithValue("@ArticleId", articleId.Value);
            if (depotId.HasValue) cmd.Parameters.AddWithValue("@DepotId", depotId.Value);
            if (!string.IsNullOrEmpty(typeMouvement)) cmd.Parameters.AddWithValue("@TypeMouvement", typeMouvement);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapToHistoriqueMouvement(reader));
            return list;
        }

        /// <summary>
        /// Historique paginé avec recherche sur ArticleNom, ArticleReference, NumeroDemande.
        /// </summary>
        public async Task<PagedResult<HistoriqueMouvementDTO>> GetHistoriquePagedAsync(MouvementSearchParams p)
        {
            var (page, pageSize, offset) = NormalisePagination(p.Page, p.PageSize);
            var hasSearch = !string.IsNullOrWhiteSpace(p.Search);

            var where = "WHERE 1=1";
            if (p.DateDebut.HasValue) where += " AND DateMouvement >= @DateDebut";
            if (p.DateFin.HasValue) where += " AND DateMouvement <= @DateFin";
            if (p.ArticleId.HasValue) where += " AND ArticleId = @ArticleId";
            if (p.DepotId.HasValue) where += " AND DepotId = @DepotId";
            if (!string.IsNullOrEmpty(p.TypeMouvement)) where += " AND TypeMouvement = @TypeMouvement";
            if (hasSearch) where += " AND (ArticleNom LIKE @Search OR ArticleReference LIKE @Search OR NumeroDemande LIKE @Search OR OperateurNom LIKE @Search)";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            void AddParams(SqlCommand cmd)
            {
                if (p.DateDebut.HasValue) cmd.Parameters.AddWithValue("@DateDebut", p.DateDebut.Value);
                if (p.DateFin.HasValue) cmd.Parameters.AddWithValue("@DateFin", p.DateFin.Value);
                if (p.ArticleId.HasValue) cmd.Parameters.AddWithValue("@ArticleId", p.ArticleId.Value);
                if (p.DepotId.HasValue) cmd.Parameters.AddWithValue("@DepotId", p.DepotId.Value);
                if (!string.IsNullOrEmpty(p.TypeMouvement)) cmd.Parameters.AddWithValue("@TypeMouvement", p.TypeMouvement);
                if (hasSearch) cmd.Parameters.AddWithValue("@Search", $"%{p.Search}%");
            }

            int total;
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM v_Stock_HistoriqueMouvements {where}", conn))
            {
                AddParams(cmd);
                total = (int)(await cmd.ExecuteScalarAsync())!;
            }

            var list = new List<HistoriqueMouvementDTO>();
            var sql = $@"SELECT * FROM v_Stock_HistoriqueMouvements {where}
                         ORDER BY DateMouvement DESC
                         OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            using (var cmd = new SqlCommand(sql, conn))
            {
                AddParams(cmd);
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) list.Add(MapToHistoriqueMouvement(reader));
            }
            return new PagedResult<HistoriqueMouvementDTO> { Items = list, TotalItems = total, Page = page, PageSize = pageSize };
        }

        public async Task<List<DemandeParProjetDTO>> GetDemandesParProjetAsync(int? projetId = null)
        {
            var list = new List<DemandeParProjetDTO>();
            using var conn = new SqlConnection(_connectionString);
            var sql = "SELECT * FROM v_Stock_DemandesParProjet WHERE 1=1";
            if (projetId.HasValue) sql += " AND ProjetId = @ProjetId";
            sql += " ORDER BY DateDemande DESC";
            using var cmd = new SqlCommand(sql, conn);
            if (projetId.HasValue) cmd.Parameters.AddWithValue("@ProjetId", projetId.Value);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapToDemandeParProjet(reader));
            return list;
        }

        public async Task<List<RapportFournisseurDTO>> GetRapportFournisseursAsync()
        {
            var list = new List<RapportFournisseurDTO>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM v_Stock_RapportFournisseurs ORDER BY MontantTotalHT DESC", conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapToRapportFournisseur(reader));
            return list;
        }

        public async Task<StockStatistiquesDTO> GetStatistiquesAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT
                    (SELECT COUNT(*) FROM Stock_Articles WHERE Actif = 1) AS TotalArticles,
                    (SELECT COUNT(*) FROM Stock_Depots WHERE Actif = 1) AS TotalDepots,
                    (SELECT ISNULL(SUM(QuantiteDisponible * a.PrixUnitaireMoyen), 0)
                     FROM Stock_Inventaire i INNER JOIN Stock_Articles a ON i.ArticleId = a.Id) AS ValeurTotaleStock,
                    (SELECT COUNT(*) FROM v_Stock_AlertesMinimum) AS ArticlesEnAlerte,
                    (SELECT COUNT(*) FROM v_Stock_AlertesMinimum WHERE TypeAlerte = 'Rupture') AS ArticlesEnRupture,
                    (SELECT COUNT(*) FROM Stock_Demandes WHERE Statut = 'EnAttente') AS DemandesEnAttente,
                    (SELECT COUNT(*) FROM Stock_Demandes WHERE Statut = 'EnTraitement') AS DemandesEnTraitement,
                    (SELECT COUNT(*) FROM Stock_Demandes WHERE Statut = 'AttenteValidation') AS DemandesAttenteValidation,
                    (SELECT COUNT(*) FROM Stock_Demandes WHERE Statut = 'AttenteLivraison') AS DemandesAttenteLivraison,
                    (SELECT COUNT(*) FROM Stock_Demandes WHERE Statut = 'LivraisonPartielle') AS DemandesLivraisonPartielle,
                    (SELECT COUNT(*) FROM Stock_Demandes WHERE Statut = 'Dotee') AS DemandesDotees,
                    (SELECT COUNT(*) FROM Stock_Demandes WHERE Statut = 'AttenteComptabilite') AS DemandesAttenteComptabilite,
                    (SELECT COUNT(*) FROM Stock_Mouvements WHERE MONTH(DateMouvement)=MONTH(GETDATE()) AND YEAR(DateMouvement)=YEAR(GETDATE())) AS MouvementsDuMois", conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return new StockStatistiquesDTO
                {
                    TotalArticles = reader.GetInt32("TotalArticles"),
                    TotalDepots = reader.GetInt32("TotalDepots"),
                    ValeurTotaleStock = reader.GetDecimal("ValeurTotaleStock"),
                    ArticlesEnAlerte = reader.GetInt32("ArticlesEnAlerte"),
                    ArticlesEnRupture = reader.GetInt32("ArticlesEnRupture"),
                    DemandesEnAttente = reader.GetInt32("DemandesEnAttente"),
                    DemandesEnTraitement = reader.GetInt32("DemandesEnTraitement"),
                    DemandesAttenteValidation = reader.GetInt32("DemandesAttenteValidation"),
                    DemandesAttenteLivraison = reader.GetInt32("DemandesAttenteLivraison"),
                    DemandesLivraisonPartielle = reader.GetInt32("DemandesLivraisonPartielle"),
                    DemandesDotees = reader.GetInt32("DemandesDotees"),
                    DemandesAttenteComptabilite = reader.GetInt32("DemandesAttenteComptabilite"),
                    MouvementsDuMois = reader.GetInt32("MouvementsDuMois")
                };
            return new StockStatistiquesDTO();
        }

        // ============================================================
        // MÉTHODES PRIVÉES UTILITAIRES (inchangées)
        // ============================================================

        private async Task<string> GenererNumeroDemande(SqlConnection conn, SqlTransaction transaction)
        {
            var annee = DateTime.UtcNow.Year;
            using var cmd = new SqlCommand(@"
                SELECT ISNULL(MAX(CAST(SUBSTRING(Numero, 10, 4) AS INT)), 0) + 1
                FROM Stock_Demandes WHERE Numero LIKE @Prefix", conn, transaction);
            cmd.Parameters.AddWithValue("@Prefix", $"DEM-{annee}-%");
            return $"DEM-{annee}-{Convert.ToInt32(await cmd.ExecuteScalarAsync()):D4}";
        }

        private async Task UpsertTraitement(SqlConnection conn, SqlTransaction transaction, int demandeId,
            SauvegarderTraitementRequest req, string statut)
        {
            using var cmd = new SqlCommand(@"
                IF EXISTS (SELECT 1 FROM Stock_Traitements WHERE DemandeId = @DemandeId)
                    UPDATE Stock_Traitements SET
                        FournisseurId = @FournisseurId, NomFournisseurLibre = @NomFournisseurLibre,
                        NumeroDevis = @NumeroDevis, MontantDevisHT = @MontantDevisHT,
                        MontantDevisTTC = @MontantDevisTTC, DateDevis = @DateDevis,
                        FichierDevisPath = @FichierDevisPath, DelaiLivraison = @DelaiLivraison,
                        ConditionsPaiement = @ConditionsPaiement, Notes = @Notes,
                        StatutTraitement = @StatutTraitement, TraitePar = @TraitePar,
                        DateModification = GETUTCDATE()
                    WHERE DemandeId = @DemandeId
                ELSE
                    INSERT INTO Stock_Traitements (DemandeId, FournisseurId, NomFournisseurLibre,
                        NumeroDevis, MontantDevisHT, MontantDevisTTC, DateDevis, FichierDevisPath,
                        DelaiLivraison, ConditionsPaiement, Notes, StatutTraitement, TraitePar,
                        DateCreation, DateModification)
                    VALUES (@DemandeId, @FournisseurId, @NomFournisseurLibre,
                        @NumeroDevis, @MontantDevisHT, @MontantDevisTTC, @DateDevis, @FichierDevisPath,
                        @DelaiLivraison, @ConditionsPaiement, @Notes, @StatutTraitement, @TraitePar,
                        GETUTCDATE(), GETUTCDATE())", conn, transaction);
            cmd.Parameters.AddWithValue("@DemandeId", demandeId);
            cmd.Parameters.AddWithValue("@FournisseurId", req.FournisseurId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NomFournisseurLibre", req.NomFournisseurLibre ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NumeroDevis", req.NumeroDevis ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MontantDevisHT", req.MontantDevisHT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MontantDevisTTC", req.MontantDevisTTC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateDevis", req.DateDevis ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FichierDevisPath", req.FichierDevisPath ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DelaiLivraison", req.DelaiLivraison ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ConditionsPaiement", req.ConditionsPaiement ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Notes", req.Notes ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@StatutTraitement", statut);
            cmd.Parameters.AddWithValue("@TraitePar", req.TraitePar);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task MajPrixArticlesDemande(SqlConnection conn, SqlTransaction transaction, List<MajPrixArticleRequest> lignes)
        {
            foreach (var ligne in lignes)
            {
                if (ligne.Source == "CommandeReste")
                {
                    // Reliquat : créer une nouvelle ligne Stock_DemandeArticles de type Commande
                    // La DesignationLibre et l'ArticleId sont copiés depuis la ligne parente
                    using var cmdParent = new SqlCommand(
                        "SELECT ArticleId, DesignationLibre, Unite, DemandeId FROM Stock_DemandeArticles WHERE Id = @Id",
                        conn, transaction);
                    cmdParent.Parameters.AddWithValue("@Id", ligne.DemandeArticleId);
                    using var rdr = await cmdParent.ExecuteReaderAsync();
                    if (!await rdr.ReadAsync()) { rdr.Close(); continue; }
                    var artId = rdr.IsDBNull("ArticleId") ? (object)DBNull.Value : rdr.GetInt32("ArticleId");
                    var designLib = rdr.IsDBNull("DesignationLibre") ? (object)DBNull.Value : rdr.GetString("DesignationLibre");
                    var unite = rdr.GetString("Unite");
                    var demandeId = rdr.GetInt32("DemandeId");
                    rdr.Close();

                    using var cmdIns = new SqlCommand(@"
                        INSERT INTO Stock_DemandeArticles
                            (DemandeId, ArticleId, DesignationLibre, Unite,
                             QuantiteDemandee, QuantiteValidee, PrixUnitaireDevis, PrixTotalLigne,
                             Source, EstLivre, DateCreation)
                        VALUES
                            (@DemandeId, @ArticleId, @DesignationLibre, @Unite,
                             @Qte, @Qte, @Prix, @Prix * @Qte,
                             'CommandeReste', 0, GETUTCDATE())", conn, transaction);
                    cmdIns.Parameters.AddWithValue("@DemandeId", demandeId);
                    cmdIns.Parameters.AddWithValue("@ArticleId", artId);
                    cmdIns.Parameters.AddWithValue("@DesignationLibre", designLib);
                    cmdIns.Parameters.AddWithValue("@Unite", unite);
                    cmdIns.Parameters.AddWithValue("@Qte", ligne.QuantiteValidee);
                    cmdIns.Parameters.AddWithValue("@Prix", ligne.PrixUnitaireDevis);
                    await cmdIns.ExecuteNonQueryAsync();
                }
                else if (ligne.Source == "Stock")
                {
                    // Mise à jour de la ligne parente : source Stock, quantité totale = somme des dotations
                    using var cmd = new SqlCommand(@"
                        UPDATE Stock_DemandeArticles SET
                            Source            = 'Stock',
                            QuantiteValidee   = @QuantiteValidee,
                            PrixUnitaireDevis = 0,
                            PrixTotalLigne    = 0,
                            DepotDotationId   = @DepotDotationId,
                            QuantiteDotee     = @QuantiteDotee
                        WHERE Id = @Id", conn, transaction);
                    cmd.Parameters.AddWithValue("@Id", ligne.DemandeArticleId);
                    cmd.Parameters.AddWithValue("@QuantiteValidee", ligne.QuantiteValidee);
                    cmd.Parameters.AddWithValue("@DepotDotationId", ligne.DepotDotationId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@QuantiteDotee", ligne.QuantiteDotee ?? ligne.QuantiteValidee);
                    await cmd.ExecuteNonQueryAsync();

                    // Upsert dans Stock_DemandeArticleDotations (une ligne par dépôt)
                    if (ligne.DepotDotationId.HasValue)
                    {
                        using var cmdDot = new SqlCommand(@"
                            IF EXISTS (SELECT 1 FROM Stock_DemandeArticleDotations
                                       WHERE DemandeArticleId = @ArtId AND DepotId = @DepotId AND EstLivre = 0)
                                UPDATE Stock_DemandeArticleDotations SET QuantiteDotee = @Qte
                                WHERE DemandeArticleId = @ArtId AND DepotId = @DepotId AND EstLivre = 0
                            ELSE IF NOT EXISTS (SELECT 1 FROM Stock_DemandeArticleDotations
                                                WHERE DemandeArticleId = @ArtId AND DepotId = @DepotId)
                                INSERT INTO Stock_DemandeArticleDotations
                                    (DemandeArticleId, DepotId, QuantiteDotee, EstLivre, DateCreation)
                                VALUES (@ArtId, @DepotId, @Qte, 0, GETUTCDATE())", conn, transaction);
                        cmdDot.Parameters.AddWithValue("@ArtId", ligne.DemandeArticleId);
                        cmdDot.Parameters.AddWithValue("@DepotId", ligne.DepotDotationId.Value);
                        cmdDot.Parameters.AddWithValue("@Qte", ligne.QuantiteDotee ?? ligne.QuantiteValidee);
                        await cmdDot.ExecuteNonQueryAsync();
                    }
                }
                else // Commande
                {
                    using var cmd = new SqlCommand(@"
                        UPDATE Stock_DemandeArticles SET
                            Source            = 'Commande',
                            DepotDotationId   = NULL,
                            QuantiteDotee     = NULL,
                            QuantiteValidee   = @QuantiteValidee,
                            PrixUnitaireDevis = @PrixUnitaireDevis,
                            PrixTotalLigne    = @QuantiteValidee * @PrixUnitaireDevis,
                            EstLivre          = 0,
                            UserValidationLivraisonId = NULL,
                            DateLivraisonConfirmee    = NULL
                        WHERE Id = @Id", conn, transaction);
                    cmd.Parameters.AddWithValue("@Id", ligne.DemandeArticleId);
                    cmd.Parameters.AddWithValue("@QuantiteValidee", ligne.QuantiteValidee);
                    cmd.Parameters.AddWithValue("@PrixUnitaireDevis", ligne.PrixUnitaireDevis);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task MajStatutDemande(SqlConnection conn, SqlTransaction transaction,
            int demandeId, string statut, DateTime? dateLivraison = null)
        {
            var sql = "UPDATE Stock_Demandes SET Statut = @Statut, DateModification = GETUTCDATE()";
            if (statut == "EnTraitement") sql += ", DateDebutTraitement = ISNULL(DateDebutTraitement, GETUTCDATE())";
            if (dateLivraison.HasValue) sql += ", DateLivraisonReelle = @DateLivraisonReelle";
            sql += " WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, conn, transaction);
            cmd.Parameters.AddWithValue("@Id", demandeId);
            cmd.Parameters.AddWithValue("@Statut", statut);
            if (dateLivraison.HasValue) cmd.Parameters.AddWithValue("@DateLivraisonReelle", dateLivraison.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<decimal> GetQuantiteInventaire(SqlConnection conn, SqlTransaction transaction, int articleId, int depotId)
        {
            using var cmd = new SqlCommand("SELECT ISNULL(QuantiteDisponible, 0) FROM Stock_Inventaire WHERE ArticleId = @ArticleId AND DepotId = @DepotId", conn, transaction);
            cmd.Parameters.AddWithValue("@ArticleId", articleId);
            cmd.Parameters.AddWithValue("@DepotId", depotId);
            var r = await cmd.ExecuteScalarAsync();
            return r == null || r == DBNull.Value ? 0 : Convert.ToDecimal(r);
        }

        private async Task<decimal> GetOuInitialiserInventaire(SqlConnection conn, SqlTransaction transaction, int articleId, int depotId)
        {
            using var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Stock_Inventaire WHERE ArticleId = @ArticleId AND DepotId = @DepotId", conn, transaction);
            checkCmd.Parameters.AddWithValue("@ArticleId", articleId);
            checkCmd.Parameters.AddWithValue("@DepotId", depotId);
            if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) == 0)
            {
                using var ins = new SqlCommand("INSERT INTO Stock_Inventaire (ArticleId, DepotId, QuantiteDisponible, QuantiteReservee, DateCreation, DateModification) VALUES (@ArticleId, @DepotId, 0, 0, GETUTCDATE(), GETUTCDATE())", conn, transaction);
                ins.Parameters.AddWithValue("@ArticleId", articleId);
                ins.Parameters.AddWithValue("@DepotId", depotId);
                await ins.ExecuteNonQueryAsync();
                return 0;
            }
            return await GetQuantiteInventaire(conn, transaction, articleId, depotId);
        }

        private async Task MajInventaire(SqlConnection conn, SqlTransaction transaction, int articleId, int depotId, decimal nouvelleQuantite, decimal? quantiteReservee)
        {
            var sql = "UPDATE Stock_Inventaire SET QuantiteDisponible = @Quantite, DateDernierMouvement = GETUTCDATE(), DateModification = GETUTCDATE()";
            if (quantiteReservee.HasValue) sql += ", QuantiteReservee = @QuantiteReservee";
            sql += " WHERE ArticleId = @ArticleId AND DepotId = @DepotId";
            using var cmd = new SqlCommand(sql, conn, transaction);
            cmd.Parameters.AddWithValue("@ArticleId", articleId);
            cmd.Parameters.AddWithValue("@DepotId", depotId);
            cmd.Parameters.AddWithValue("@Quantite", nouvelleQuantite);
            if (quantiteReservee.HasValue) cmd.Parameters.AddWithValue("@QuantiteReservee", quantiteReservee.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task InsererMouvement(SqlConnection conn, SqlTransaction transaction, StockMouvement m)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO Stock_Mouvements (ArticleId, DepotId, TypeMouvement, Quantite,
                    QuantiteAvant, QuantiteApres, PrixUnitaire, MontantTotal, Reference,
                    DemandeId, ProjetId, EtapeProjetId, DepotDestinationId, MotifSortie,
                    OperateurId, DateMouvement, Notes, DateCreation)
                VALUES (@ArticleId, @DepotId, @TypeMouvement, @Quantite,
                    @QuantiteAvant, @QuantiteApres, @PrixUnitaire, @MontantTotal, @Reference,
                    @DemandeId, @ProjetId, @EtapeProjetId, @DepotDestinationId, @MotifSortie,
                    @OperateurId, GETUTCDATE(), @Notes, GETUTCDATE())", conn, transaction);
            cmd.Parameters.AddWithValue("@ArticleId", m.ArticleId);
            cmd.Parameters.AddWithValue("@DepotId", m.DepotId);
            cmd.Parameters.AddWithValue("@TypeMouvement", m.TypeMouvement);
            cmd.Parameters.AddWithValue("@Quantite", m.Quantite);
            cmd.Parameters.AddWithValue("@QuantiteAvant", m.QuantiteAvant);
            cmd.Parameters.AddWithValue("@QuantiteApres", m.QuantiteApres);
            cmd.Parameters.AddWithValue("@PrixUnitaire", m.PrixUnitaire ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MontantTotal", m.MontantTotal ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Reference", m.Reference ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DemandeId", m.DemandeId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ProjetId", m.ProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EtapeProjetId", m.EtapeProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DepotDestinationId", m.DepotDestinationId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MotifSortie", m.MotifSortie ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OperateurId", m.OperateurId);
            cmd.Parameters.AddWithValue("@Notes", m.Notes ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Variante de InsererMouvement qui retourne l'Id inséré.
        /// Utilisée lorsqu'on a besoin de lier le mouvement à une dotation ou d'inclure son Id dans la réponse.
        /// </summary>
        private async Task<int> InsererMouvementReturnIdAsync(SqlConnection conn, SqlTransaction transaction, StockMouvement m)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO Stock_Mouvements (ArticleId, DepotId, TypeMouvement, Quantite,
                    QuantiteAvant, QuantiteApres, PrixUnitaire, MontantTotal, Reference,
                    DemandeId, ProjetId, EtapeProjetId, DepotDestinationId, MotifSortie,
                    OperateurId, DateMouvement, Notes, DateCreation)
                VALUES (@ArticleId, @DepotId, @TypeMouvement, @Quantite,
                    @QuantiteAvant, @QuantiteApres, @PrixUnitaire, @MontantTotal, @Reference,
                    @DemandeId, @ProjetId, @EtapeProjetId, @DepotDestinationId, @MotifSortie,
                    @OperateurId, GETUTCDATE(), @Notes, GETUTCDATE());
                SELECT SCOPE_IDENTITY();", conn, transaction);
            cmd.Parameters.AddWithValue("@ArticleId", m.ArticleId);
            cmd.Parameters.AddWithValue("@DepotId", m.DepotId);
            cmd.Parameters.AddWithValue("@TypeMouvement", m.TypeMouvement);
            cmd.Parameters.AddWithValue("@Quantite", m.Quantite);
            cmd.Parameters.AddWithValue("@QuantiteAvant", m.QuantiteAvant);
            cmd.Parameters.AddWithValue("@QuantiteApres", m.QuantiteApres);
            cmd.Parameters.AddWithValue("@PrixUnitaire", m.PrixUnitaire ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MontantTotal", m.MontantTotal ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Reference", m.Reference ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DemandeId", m.DemandeId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ProjetId", m.ProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EtapeProjetId", m.EtapeProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DepotDestinationId", m.DepotDestinationId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MotifSortie", m.MotifSortie ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OperateurId", m.OperateurId);
            cmd.Parameters.AddWithValue("@Notes", m.Notes ?? (object)DBNull.Value);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        /// <summary>
        /// Retourne l'Id et le Nom du dépôt par défaut actif.
        /// Lance InvalidOperationException si aucun dépôt par défaut n'est configuré.
        /// </summary>
        private async Task<(int Id, string Nom)> GetDepotParDefautAsync(
            SqlConnection conn, SqlTransaction transaction)
        {
            using var cmd = new SqlCommand(
                "SELECT TOP 1 Id, Nom FROM Stock_Depots WHERE EstParDefaut = 1 AND Actif = 1",
                conn, transaction);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                throw new InvalidOperationException(
                    "Aucun dépôt par défaut configuré. Veuillez en définir un via le paramétrage des dépôts.");
            return (r.GetInt32("Id"), r.GetString("Nom"));
        }

        /// <summary>
        /// Retourne le DepotDemandeId de la demande, ou null si non renseigné.
        /// </summary>
        private async Task<int?> GetDepotDemandeIdAsync(
            SqlConnection conn, SqlTransaction transaction, int demandeId)
        {
            using var cmd = new SqlCommand(
                "SELECT DepotDemandeId FROM Stock_Demandes WHERE Id = @Id",
                conn, transaction);
            cmd.Parameters.AddWithValue("@Id", demandeId);
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
        }

        private async Task MajPrixUnitaireMoyen(SqlConnection conn, SqlTransaction transaction, int articleId, decimal quantiteEntree, decimal prixEntree)
        {
            using var cmd = new SqlCommand(@"
                UPDATE Stock_Articles SET
                    PrixUnitaireMoyen = (
                        (ISNULL((SELECT SUM(QuantiteDisponible) FROM Stock_Inventaire WHERE ArticleId = @ArticleId), 0) - @QteEntree)
                        * PrixUnitaireMoyen + @QteEntree * @PrixEntree
                    ) / NULLIF(ISNULL((SELECT SUM(QuantiteDisponible) FROM Stock_Inventaire WHERE ArticleId = @ArticleId), 0), 0),
                    DateModification = GETUTCDATE()
                WHERE Id = @ArticleId", conn, transaction);
            cmd.Parameters.AddWithValue("@ArticleId", articleId);
            cmd.Parameters.AddWithValue("@QteEntree", quantiteEntree);
            cmd.Parameters.AddWithValue("@PrixEntree", prixEntree);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<List<StockDemandeArticleDetailDTO>> GetDemandeArticlesAsync(SqlConnection conn, int demandeId)
        {
            var list = new List<StockDemandeArticleDetailDTO>();
            using var cmd = new SqlCommand(@"
                SELECT da.*,
                    a.Reference  AS ArticleReference,
                    a.Nom        AS ArticleCatNom,
                    a.Description As ArticleDescription,
                    dep.Nom      AS DepotDotationNom,
                    uv.Prenom + ' ' + uv.Nom AS UserValidationLivraisonNom
                FROM Stock_DemandeArticles da
                LEFT JOIN Stock_Articles  a   ON da.ArticleId              = a.Id
                LEFT JOIN Stock_Depots    dep ON da.DepotDotationId        = dep.Id
                LEFT JOIN Utilisateurs    uv  ON da.UserValidationLivraisonId = uv.Id
                WHERE da.DemandeId = @DemandeId
                ORDER BY da.Id", conn);
            cmd.Parameters.AddWithValue("@DemandeId", demandeId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(new StockDemandeArticleDetailDTO
                {
                    Id = reader.GetInt32("Id"),
                    ArticleId = reader.IsDBNull("ArticleId") ? null : reader.GetInt32("ArticleId"),
                    ArticleReference = reader.IsDBNull("ArticleReference") ? null : reader.GetString("ArticleReference"),
                    ArticleNom = reader.IsDBNull("DesignationLibre")
                                         ? (reader.IsDBNull("ArticleCatNom") ? "" : reader.GetString("ArticleCatNom"))
                                         : reader.GetString("DesignationLibre"),
                    ArticleDescription = reader.IsDBNull("ArticleDescription") ? null : reader.GetString("ArticleDescription"),

                    QuantiteDemandee = reader.GetDecimal("QuantiteDemandee"),
                    QuantiteValidee = reader.IsDBNull("QuantiteValidee") ? null : reader.GetDecimal("QuantiteValidee"),
                    PrixUnitaireDevis = reader.IsDBNull("PrixUnitaireDevis") ? null : reader.GetDecimal("PrixUnitaireDevis"),
                    PrixTotalLigne = reader.IsDBNull("PrixTotalLigne") ? null : reader.GetDecimal("PrixTotalLigne"),
                    Notes = reader.IsDBNull("Notes") ? null : reader.GetString("Notes"),
                    Source = reader.IsDBNull("Source") ? "Commande" : reader.GetString("Source"),
                    DepotDotationId = reader.IsDBNull("DepotDotationId") ? null : reader.GetInt32("DepotDotationId"),
                    DepotDotationNom = reader.IsDBNull("DepotDotationNom") ? null : reader.GetString("DepotDotationNom"),
                    QuantiteDotee = reader.IsDBNull("QuantiteDotee") ? null : reader.GetDecimal("QuantiteDotee"),
                    EstLivre = !reader.IsDBNull("EstLivre") && reader.GetBoolean("EstLivre"),
                    UserValidationLivraisonId = reader.IsDBNull("UserValidationLivraisonId") ? null : reader.GetInt32("UserValidationLivraisonId"),
                    UserValidationLivraisonNom = reader.IsDBNull("UserValidationLivraisonNom") ? null : reader.GetString("UserValidationLivraisonNom"),
                    DateLivraisonConfirmee = reader.IsDBNull("DateLivraisonConfirmee") ? null : reader.GetDateTime("DateLivraisonConfirmee"),
                });
            reader.Close();

            // Charger les dotations multi-dépôt pour les articles source Stock
            foreach (var art in list.Where(a => a.Source == "Stock"))
                art.Dotations = await GetArticleDotationsAsync(conn, art.Id);

            return list;
        }

        private async Task<List<StockDemandeArticleDotationDTO>> GetArticleDotationsAsync(SqlConnection conn, int demandeArticleId)
        {
            var list = new List<StockDemandeArticleDotationDTO>();
            using var cmd = new SqlCommand(@"
                SELECT d.*, dep.Nom AS DepotNom, dep.Code AS DepotCode,
                    u.Prenom + ' ' + u.Nom AS UserValidationNom
                FROM Stock_DemandeArticleDotations d
                JOIN  Stock_Depots    dep ON d.DepotId           = dep.Id
                LEFT JOIN Utilisateurs u  ON d.UserValidationId  = u.Id
                WHERE d.DemandeArticleId = @ArtId
                ORDER BY d.Id", conn);
            cmd.Parameters.AddWithValue("@ArtId", demandeArticleId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(new StockDemandeArticleDotationDTO
                {
                    Id = reader.GetInt32("Id"),
                    DemandeArticleId = reader.GetInt32("DemandeArticleId"),
                    DepotId = reader.GetInt32("DepotId"),
                    DepotNom = reader.GetString("DepotNom"),
                    DepotCode = reader.GetString("DepotCode"),
                    QuantiteDotee = reader.GetDecimal("QuantiteDotee"),
                    EstLivre = !reader.IsDBNull("EstLivre") && reader.GetBoolean("EstLivre"),
                    UserValidationNom = reader.IsDBNull("UserValidationNom") ? null : reader.GetString("UserValidationNom"),
                    DateLivraisonConfirmee = reader.IsDBNull("DateLivraisonConfirmee") ? null : reader.GetDateTime("DateLivraisonConfirmee"),
                    MouvementId = reader.IsDBNull("MouvementId") ? null : reader.GetInt32("MouvementId"),
                });
            return list;
        }

        private async Task<List<MouvementDemandeDTO>> GetDemandeMouvementsAsync(SqlConnection conn, int demandeId)
        {
            var list = new List<MouvementDemandeDTO>();
            using var cmd = new SqlCommand(@"
                SELECT m.Id, m.DateMouvement, m.TypeMouvement, m.ArticleId,
                    m.Quantite, m.QuantiteAvant, m.QuantiteApres, m.PrixUnitaire,
                    m.MotifSortie, m.Notes,m.DepotId,
                    a.Nom   AS ArticleNom, a.Reference AS ArticleReference, a.Unite,
                    dep.Nom AS DepotNom,
                    op.Prenom + ' ' + op.Nom AS OperateurNom,
                    dot.Id  AS DotationId
                FROM Stock_Mouvements m
                JOIN  Stock_Articles  a   ON m.ArticleId    = a.Id
                JOIN  Stock_Depots    dep ON m.DepotId       = dep.Id
                JOIN  Utilisateurs    op  ON m.OperateurId   = op.Id
                LEFT JOIN Stock_DemandeArticleDotations dot ON dot.MouvementId = m.Id
                WHERE m.DemandeId = @DemandeId
                ORDER BY m.DateMouvement DESC", conn);
            cmd.Parameters.AddWithValue("@DemandeId", demandeId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(new MouvementDemandeDTO
                {
                    Id = reader.GetInt32("Id"),
                    DateMouvement = reader.GetDateTime("DateMouvement"),
                    TypeMouvement = reader.GetString("TypeMouvement"),
                    ArticleId = reader.GetInt32("ArticleId"),
                    ArticleNom = reader.GetString("ArticleNom"),
                    ArticleReference = reader.GetString("ArticleReference"),
                    Unite = reader.GetString("Unite"),
                    DepotId = reader.GetInt32("DepotId"),
                    DepotNom = reader.GetString("DepotNom"),
                    Quantite = reader.GetDecimal("Quantite"),
                    QuantiteAvant = reader.GetDecimal("QuantiteAvant"),
                    QuantiteApres = reader.GetDecimal("QuantiteApres"),
                    PrixUnitaire = reader.IsDBNull("PrixUnitaire") ? null : reader.GetDecimal("PrixUnitaire"),
                    MotifSortie = reader.IsDBNull("MotifSortie") ? null : reader.GetString("MotifSortie"),
                    OperateurNom = reader.GetString("OperateurNom"),
                    Notes = reader.IsDBNull("Notes") ? null : reader.GetString("Notes"),
                    DotationId = reader.IsDBNull("DotationId") ? null : reader.GetInt32("DotationId"),
                });
            return list;
        }

        public async Task<StockDepot?> GetDepotBydefault()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
           SELECT d.*, u.Prenom + ' ' + u.Nom AS ResponsableNom
           FROM Stock_Depots d LEFT JOIN Utilisateurs u ON d.ResponsableId = u.Id
           WHERE d.EstParDefaut = 1", conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return MapToDepot(reader);
            return null;
        }

        // ============================================================
        // MAPPERS (inchangés)
        // ============================================================

        private StockCategorie MapToCategorie(SqlDataReader r) => new()
        {
            Id = r.GetInt32("Id"),
            Code = r.GetString("Code"),
            Nom = r.GetString("Nom"),
            Description = r.IsDBNull("Description") ? null : r.GetString("Description"),
            Couleur = r.GetString("Couleur"),
            Actif = r.GetBoolean("Actif"),
            DateCreation = r.GetDateTime("DateCreation"),
            DateModification = r.GetDateTime("DateModification")
        };

        private StockDepot MapToDepot(SqlDataReader r) => new()
        {
            Id = r.GetInt32("Id"),
            Code = r.GetString("Code"),
            Nom = r.GetString("Nom"),
            Description = r.IsDBNull("Description") ? null : r.GetString("Description"),
            Adresse = r.IsDBNull("Adresse") ? null : r.GetString("Adresse"),
            Ville = r.IsDBNull("Ville") ? null : r.GetString("Ville"),
            ResponsableId = r.IsDBNull("ResponsableId") ? null : r.GetInt32("ResponsableId"),
            Actif = r.GetBoolean("Actif"),
            EstParDefaut = !r.IsDBNull("EstParDefaut") && r.GetBoolean("EstParDefaut"),
            DateCreation = r.GetDateTime("DateCreation"),
            DateModification = r.GetDateTime("DateModification")
        };

        private StockFournisseur MapToFournisseur(SqlDataReader r) => new()
        {
            Id = r.GetInt32("Id"),
            Code = r.GetString("Code"),
            Nom = r.GetString("Nom"),
            Contact = r.IsDBNull("Contact") ? null : r.GetString("Contact"),
            Telephone = r.IsDBNull("Telephone") ? null : r.GetString("Telephone"),
            Email = r.IsDBNull("Email") ? null : r.GetString("Email"),
            Adresse = r.IsDBNull("Adresse") ? null : r.GetString("Adresse"),
            Ville = r.IsDBNull("Ville") ? null : r.GetString("Ville"),
            NoteEvaluation = r.IsDBNull("NoteEvaluation") ? null : r.GetInt32("NoteEvaluation"),
            Actif = r.GetBoolean("Actif"),
            DateCreation = r.GetDateTime("DateCreation"),
            DateModification = r.GetDateTime("DateModification")
        };

        private StockArticle MapToArticle(SqlDataReader r) => new()
        {
            Id = r.GetInt32("Id"),
            Reference = r.GetString("Reference"),
            Nom = r.GetString("Nom"),
            Description = r.IsDBNull("Description") ? null : r.GetString("Description"),
            CategorieId = r.GetInt32("CategorieId"),
            Unite = r.GetString("Unite"),
            PrixUnitaireMoyen = r.GetDecimal("PrixUnitaireMoyen"),
            SeuilMinimum = r.GetDecimal("SeuilMinimum"),
            SeuilMaximum = r.IsDBNull("SeuilMaximum") ? null : r.GetDecimal("SeuilMaximum"),
            FournisseurPreferentielId = r.IsDBNull("FournisseurPreferentielId") ? null : r.GetInt32("FournisseurPreferentielId"),
            Actif = r.GetBoolean("Actif"),
            DateCreation = r.GetDateTime("DateCreation"),
            DateModification = r.GetDateTime("DateModification"),
            Categorie = new StockCategorie { Id = r.GetInt32("CategorieId"), Nom = r.IsDBNull("CategorieNom") ? "" : r.GetString("CategorieNom"), Couleur = r.IsDBNull("CategorieCouleur") ? "#2563eb" : r.GetString("CategorieCouleur") }
        };

        private EtatStockDTO MapToEtatStock(SqlDataReader r) => new()
        {
            ArticleId = r.GetInt32("ArticleId"),
            Reference = r.GetString("Reference"),
            ArticleNom = r.GetString("ArticleNom"),
            Unite = r.GetString("Unite"),
            SeuilMinimum = r.GetDecimal("SeuilMinimum"),
            SeuilMaximum = r.IsDBNull("SeuilMaximum") ? null : r.GetDecimal("SeuilMaximum"),
            PrixUnitaireMoyen = r.GetDecimal("PrixUnitaireMoyen"),
            CategorieId = r.GetInt32("CategorieId"),
            CategorieNom = r.GetString("CategorieNom"),
            CategorieCouleur = r.GetString("CategorieCouleur"),
            DepotId = r.GetInt32("DepotId"),
            DepotCode = r.GetString("DepotCode"),
            DepotNom = r.GetString("DepotNom"),
            QuantiteDisponible = r.GetDecimal("QuantiteDisponible"),
            QuantiteReservee = r.GetDecimal("QuantiteReservee"),
            QuantiteLibre = r.GetDecimal("QuantiteLibre"),
            ValeurStock = r.GetDecimal("ValeurStock"),
            NiveauAlerte = r.GetString("NiveauAlerte"),
            EnAlerte = r.GetInt32("EnAlerte") == 1,
            DateDernierMouvement = r.IsDBNull("DateDernierMouvement") ? null : r.GetDateTime("DateDernierMouvement")
        };

        private AlerteStockDTO MapToAlerteStock(SqlDataReader r) => new()
        {
            ArticleId = r.GetInt32("ArticleId"),
            Reference = r.GetString("Reference"),
            ArticleNom = r.GetString("ArticleNom"),
            Unite = r.GetString("Unite"),
            SeuilMinimum = r.GetDecimal("SeuilMinimum"),
            CategorieNom = r.GetString("CategorieNom"),
            StockTotal = r.GetDecimal("StockTotal"),
            QuantiteManquante = r.GetDecimal("QuantiteManquante"),
            PrixUnitaireMoyen = r.GetDecimal("PrixUnitaireMoyen"),
            ValeurAReapprovisionner = r.GetDecimal("ValeurAReapprovisionner"),
            TypeAlerte = r.GetString("TypeAlerte"),
            FournisseurId = r.IsDBNull("FournisseurId") ? null : r.GetInt32("FournisseurId"),
            FournisseurPreferentiel = r.IsDBNull("FournisseurPreferentiel") ? null : r.GetString("FournisseurPreferentiel"),
            TelFournisseur = r.IsDBNull("TelFournisseur") ? null : r.GetString("TelFournisseur"),
            EmailFournisseur = r.IsDBNull("EmailFournisseur") ? null : r.GetString("EmailFournisseur")
        };

        private StockDemandeDetailDTO MapToDemandeDetail(SqlDataReader r)
        {
            var dem = new StockDemandeDetailDTO
            {
                Id = r.GetInt32("Id"),
                Numero = r.GetString("Numero"),
                NomDemandeur = r.GetString("NomDemandeur"),
                PosteDemandeur = r.GetString("PosteDemandeur"),
                TypeDestination = r.GetString("TypeDestination"),
                ProjetId = r.IsDBNull("ProjetId") ? null : r.GetInt32("ProjetId"),
                ProjetNom = r.IsDBNull("ProjetNom") ? null : r.GetString("ProjetNom"),
                ProjetNumero = r.IsDBNull("ProjetNumero") ? null : r.GetString("ProjetNumero"),
                EtapeProjetId = r.IsDBNull("EtapeProjetId") ? null : r.GetInt32("EtapeProjetId"),
                EtapeNom = r.IsDBNull("EtapeNom") ? null : r.GetString("EtapeNom"),
                Statut = r.GetString("Statut"),
                MotifDemande = r.IsDBNull("MotifDemande") ? null : r.GetString("MotifDemande"),
                MontantTotal = r.GetDecimal("MontantTotal"),
                NotesTraitement = r.IsDBNull("NotesTraitement") ? null : r.GetString("NotesTraitement"),
                NotesValidation = r.IsDBNull("NotesValidation") ? null : r.GetString("NotesValidation"),
                ValidateurNom = r.IsDBNull("ValidateurNom") ? null : r.GetString("ValidateurNom"),
                DateDemande = r.GetDateTime("DateDemande"),
                DateDebutTraitement = r.IsDBNull("DateDebutTraitement") ? null : r.GetDateTime("DateDebutTraitement"),
                DateValidation = r.IsDBNull("DateValidation") ? null : r.GetDateTime("DateValidation"),
                DateLivraisonPrevue = r.IsDBNull("DateLivraisonPrevue") ? null : r.GetDateTime("DateLivraisonPrevue"),
                DateLivraisonReelle = r.IsDBNull("DateLivraisonReelle") ? null : r.GetDateTime("DateLivraisonReelle"),
                // Nouveaux champs
                DemandeurId = r.IsDBNull("DemandeurId") ? null : r.GetInt32("DemandeurId"),
                DepotDemandeId = r.IsDBNull("DepotDemandeId") ? null : r.GetInt32("DepotDemandeId"),
                DepotDemandeNom = r.IsDBNull("DepotDemandeNom") ? null : r.GetString("DepotDemandeNom"),
                DepotDemandeCode = r.IsDBNull("DepotDemandeCode") ? null : r.GetString("DepotDemandeCode"),
            };
            if (!r.IsDBNull("TraitementId"))
                dem.Traitement = new StockTraitementDetailDTO
                {
                    Id = r.GetInt32("TraitementId"),
                    FournisseurId = r.IsDBNull("FournisseurId") ? null : r.GetInt32("FournisseurId"),
                    FournisseurNom = r.IsDBNull("FournisseurNom") ? null : r.GetString("FournisseurNom"),
                    NomFournisseurLibre = r.IsDBNull("NomFournisseurLibre") ? null : r.GetString("NomFournisseurLibre"),
                    NumeroDevis = r.IsDBNull("NumeroDevis") ? null : r.GetString("NumeroDevis"),
                    MontantDevisHT = r.IsDBNull("MontantDevisHT") ? null : r.GetDecimal("MontantDevisHT"),
                    MontantDevisTTC = r.IsDBNull("MontantDevisTTC") ? null : r.GetDecimal("MontantDevisTTC"),
                    DateDevis = r.IsDBNull("DateDevis") ? null : r.GetDateTime("DateDevis"),
                    FichierDevisPath = r.IsDBNull("FichierDevisPath") ? null : r.GetString("FichierDevisPath"),
                    DelaiLivraison = r.IsDBNull("DelaiLivraison") ? null : r.GetString("DelaiLivraison"),
                    ConditionsPaiement = r.IsDBNull("ConditionsPaiement") ? null : r.GetString("ConditionsPaiement"),
                    Notes = r.IsDBNull("TraitementNotes") ? null : r.GetString("TraitementNotes"),
                    StatutTraitement = r.GetString("StatutTraitement"),
                    TraiteParNom = r.IsDBNull("TraiteParNom") ? null : r.GetString("TraiteParNom"),
                    DateModification = r.GetDateTime("TraitementDateMod")
                };
            return dem;
        }

        private HistoriqueMouvementDTO MapToHistoriqueMouvement(SqlDataReader r) => new()
        {
            MouvementId = r.GetInt32("MouvementId"),
            DateMouvement = r.GetDateTime("DateMouvement"),
            TypeMouvement = r.GetString("TypeMouvement"),
            ArticleId = r.GetInt32("ArticleId"),
            ArticleReference = r.GetString("ArticleReference"),
            ArticleNom = r.GetString("ArticleNom"),
            Unite = r.GetString("Unite"),
            CategorieNom = r.GetString("CategorieNom"),
            DepotId = r.GetInt32("DepotId"),
            DepotNom = r.GetString("DepotNom"),
            DepotDestinationNom = r.IsDBNull("DepotDestinationNom") ? null : r.GetString("DepotDestinationNom"),
            Quantite = r.GetDecimal("Quantite"),
            QuantiteAvant = r.GetDecimal("QuantiteAvant"),
            QuantiteApres = r.GetDecimal("QuantiteApres"),
            PrixUnitaire = r.IsDBNull("PrixUnitaire") ? null : r.GetDecimal("PrixUnitaire"),
            MontantTotal = r.IsDBNull("MontantTotal") ? null : r.GetDecimal("MontantTotal"),
            ReferenceMouvement = r.IsDBNull("ReferenceMouvement") ? null : r.GetString("ReferenceMouvement"),
            DemandeId = r.IsDBNull("DemandeId") ? null : r.GetInt32("DemandeId"),
            NumeroDemande = r.IsDBNull("NumeroDemande") ? null : r.GetString("NumeroDemande"),
            ProjetId = r.IsDBNull("ProjetId") ? null : r.GetInt32("ProjetId"),
            ProjetNom = r.IsDBNull("ProjetNom") ? null : r.GetString("ProjetNom"),
            ProjetNumero = r.IsDBNull("ProjetNumero") ? null : r.GetString("ProjetNumero"),
            EtapeId = r.IsDBNull("EtapeId") ? null : r.GetInt32("EtapeId"),
            EtapeNom = r.IsDBNull("EtapeNom") ? null : r.GetString("EtapeNom"),
            OperateurNom = r.GetString("OperateurNom"),
            MotifSortie = r.IsDBNull("MotifSortie") ? null : r.GetString("MotifSortie"),
            Notes = r.IsDBNull("Notes") ? null : r.GetString("Notes"),
            Entree = r.GetDecimal("Entree"),
            Sortie = r.GetDecimal("Sortie")
        };

        private DemandeParProjetDTO MapToDemandeParProjet(SqlDataReader r) => new()
        {
            DemandeId = r.GetInt32("DemandeId"),
            Numero = r.GetString("Numero"),
            Statut = r.GetString("Statut"),
            TypeDestination = r.GetString("TypeDestination"),
            NomDemandeur = r.GetString("NomDemandeur"),
            PosteDemandeur = r.GetString("PosteDemandeur"),
            DateDemande = r.GetDateTime("DateDemande"),
            MontantTotal = r.GetDecimal("MontantTotal"),
            DateValidation = r.IsDBNull("DateValidation") ? null : r.GetDateTime("DateValidation"),
            DateLivraisonPrevue = r.IsDBNull("DateLivraisonPrevue") ? null : r.GetDateTime("DateLivraisonPrevue"),
            DateLivraisonReelle = r.IsDBNull("DateLivraisonReelle") ? null : r.GetDateTime("DateLivraisonReelle"),
            ProjetId = r.IsDBNull("ProjetId") ? null : r.GetInt32("ProjetId"),
            ProjetNumero = r.IsDBNull("ProjetNumero") ? null : r.GetString("ProjetNumero"),
            ProjetNom = r.IsDBNull("ProjetNom") ? null : r.GetString("ProjetNom"),
            EtapeId = r.IsDBNull("EtapeId") ? null : r.GetInt32("EtapeId"),
            EtapeNom = r.IsDBNull("EtapeNom") ? null : r.GetString("EtapeNom"),
            NombreArticles = r.GetInt32("NombreArticles"),
            TotalQteDemandee = r.GetDecimal("TotalQteDemandee"),
            MontantLignes = r.GetDecimal("MontantLignes"),
            DureeTraitement = r.GetInt32("DureeTraitement"),
            ValidateurNom = r.IsDBNull("ValidateurNom") ? null : r.GetString("ValidateurNom"),
            NumeroDevis = r.IsDBNull("NumeroDevis") ? null : r.GetString("NumeroDevis"),
            MontantDevisHT = r.IsDBNull("MontantDevisHT") ? null : r.GetDecimal("MontantDevisHT"),
            FournisseurNom = r.IsDBNull("FournisseurNom") ? null : r.GetString("FournisseurNom")
        };

        private RapportFournisseurDTO MapToRapportFournisseur(SqlDataReader r) => new()
        {
            FournisseurId = r.GetInt32("FournisseurId"),
            FournisseurCode = r.GetString("FournisseurCode"),
            FournisseurNom = r.GetString("FournisseurNom"),
            Telephone = r.IsDBNull("Telephone") ? null : r.GetString("Telephone"),
            Email = r.IsDBNull("Email") ? null : r.GetString("Email"),
            Ville = r.IsDBNull("Ville") ? null : r.GetString("Ville"),
            NoteEvaluation = r.GetInt32("NoteEvaluation"),
            NombreCommandes = r.GetInt32("NombreCommandes"),
            MontantTotalHT = r.GetDecimal("MontantTotalHT"),
            MontantMoyenCommande = r.GetDecimal("MontantMoyenCommande"),
            DerniereCommande = r.IsDBNull("DerniereCommande") ? null : r.GetDateTime("DerniereCommande"),
            NombreArticlesPreferentiels = r.GetInt32("NombreArticlesPreferentiels")
        };
    }
}
