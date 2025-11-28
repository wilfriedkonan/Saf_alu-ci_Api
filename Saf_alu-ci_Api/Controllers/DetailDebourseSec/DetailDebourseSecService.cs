using Microsoft.Data.SqlClient;

namespace Saf_alu_ci_Api.Controllers.Dqe
{
    public class DetailDebourseSecService
    {
        private readonly string _connectionString;
        private readonly ILogger<DetailDebourseSecService> _logger;

        public DetailDebourseSecService(
            string connectionString,
            ILogger<DetailDebourseSecService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        // =============================================
        // CRUD DÉTAILS DÉBOURSÉ SEC
        // =============================================

        /// <summary>
        /// Récupère tous les détails de déboursé d'un item
        /// </summary>
        public async Task<List<DQEDetailDebourseSec>> GetByItemIdAsync(int itemId)
        {
            var details = new List<DQEDetailDebourseSec>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(@"
                    SELECT 
                        Id, ItemId, TypeDepense, Designation, Description, Ordre,
                        Unite, Quantite, PrixUnitaireHT, MontantHT, Coefficient,
                        ReferenceExterne, Notes, DateCreation, DateModification, Actif
                    FROM DQE_DetailDebourseSec
                    WHERE ItemId = @ItemId AND Actif = 1
                    ORDER BY Ordre, TypeDepense", conn);

                cmd.Parameters.AddWithValue("@ItemId", itemId);

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    details.Add(MapDetailFromReader(reader));
                }

                return details;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la récupération des détails de l'item {itemId}");
                throw;
            }
        }

        /// <summary>
        /// Récupère un détail par son ID
        /// </summary>
        public async Task<DQEDetailDebourseSec?> GetByIdAsync(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(@"
                    SELECT 
                        Id, ItemId, TypeDepense, Designation, Description, Ordre,
                        Unite, Quantite, PrixUnitaireHT, MontantHT, Coefficient,
                        ReferenceExterne, Notes, DateCreation, DateModification, Actif
                    FROM DQE_DetailDebourseSec
                    WHERE Id = @Id AND Actif = 1", conn);

                cmd.Parameters.AddWithValue("@Id", id);

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapDetailFromReader(reader);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la récupération du détail {id}");
                throw;
            }
        }

        /// <summary>
        /// Crée un nouveau détail de déboursé
        /// </summary>
        public async Task<DQEDetailDebourseSec> CreateAsync(int itemId, CreateDetailDebourseSecRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Vérifier que l'item existe
                using (var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM DQE_Items WHERE Id = @ItemId AND Actif = 1",
                    conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@ItemId", itemId);
                    var itemExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                    if (!itemExists)
                    {
                        throw new Exception($"Item DQE {itemId} introuvable");
                    }
                }

                // Valider le type de dépense
                if (!TypeDepenseEnum.IsValid(request.TypeDepense))
                {
                    throw new Exception($"Type de dépense invalide: {request.TypeDepense}");
                }

                // Créer le détail
                using var cmd = new SqlCommand(@"
                    INSERT INTO DQE_DetailDebourseSec (
                        ItemId, TypeDepense, Designation, Description, Ordre,
                        Unite, Quantite, PrixUnitaireHT, Coefficient,
                        ReferenceExterne, Notes, DateCreation, Actif
                    ) VALUES (
                        @ItemId, @TypeDepense, @Designation, @Description, @Ordre,
                        @Unite, @Quantite, @PrixUnitaireHT, @Coefficient,
                        @ReferenceExterne, @Notes, @DateCreation, 1
                    );
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@TypeDepense", request.TypeDepense);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Ordre", request.Ordre);
                cmd.Parameters.AddWithValue("@Unite", request.Unite);
                cmd.Parameters.AddWithValue("@Quantite", request.Quantite);
                cmd.Parameters.AddWithValue("@PrixUnitaireHT", request.PrixUnitaireHT);
                cmd.Parameters.AddWithValue("@Coefficient", request.Coefficient ?? 1.00M);
                cmd.Parameters.AddWithValue("@ReferenceExterne", request.ReferenceExterne ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Notes", request.Notes ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateCreation", DateTime.UtcNow);

                var newId = (int)await cmd.ExecuteScalarAsync();

                // Le trigger SQL met à jour automatiquement:
                // 1. detail.MontantHT
                // 2. item.DeboursseSec

                transaction.Commit();

                _logger.LogInformation($"Détail de déboursé créé: ID {newId} pour item {itemId}");

                // Récupérer le détail créé avec le MontantHT calculé
                return await GetByIdAsync(newId)
                    ?? throw new Exception("Erreur lors de la récupération du détail créé");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"Erreur lors de la création du détail pour item {itemId}");
                throw;
            }
        }

        /// <summary>
        /// Met à jour un détail de déboursé
        /// </summary>
        public async Task<DQEDetailDebourseSec> UpdateAsync(int id, UpdateDetailDebourseSecRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Vérifier que le détail existe
                using (var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM DQE_DetailDebourseSec WHERE Id = @Id AND Actif = 1",
                    conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@Id", id);
                    var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                    if (!exists)
                    {
                        throw new Exception($"Détail {id} introuvable");
                    }
                }

                // Construire la requête UPDATE dynamiquement
                var updates = new List<string>();
                var cmd = new SqlCommand("", conn, transaction);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

                if (!string.IsNullOrEmpty(request.TypeDepense))
                {
                    if (!TypeDepenseEnum.IsValid(request.TypeDepense))
                    {
                        throw new Exception($"Type de dépense invalide: {request.TypeDepense}");
                    }
                    updates.Add("TypeDepense = @TypeDepense");
                    cmd.Parameters.AddWithValue("@TypeDepense", request.TypeDepense);
                }

                if (!string.IsNullOrEmpty(request.Designation))
                {
                    updates.Add("Designation = @Designation");
                    cmd.Parameters.AddWithValue("@Designation", request.Designation);
                }

                if (request.Description != null)
                {
                    updates.Add("Description = @Description");
                    cmd.Parameters.AddWithValue("@Description",
                        string.IsNullOrEmpty(request.Description) ? DBNull.Value : request.Description);
                }

                if (request.Ordre.HasValue)
                {
                    updates.Add("Ordre = @Ordre");
                    cmd.Parameters.AddWithValue("@Ordre", request.Ordre.Value);
                }

                if (!string.IsNullOrEmpty(request.Unite))
                {
                    updates.Add("Unite = @Unite");
                    cmd.Parameters.AddWithValue("@Unite", request.Unite);
                }

                if (request.Quantite.HasValue)
                {
                    if (request.Quantite.Value <= 0)
                        throw new Exception("La quantité doit être supérieure à 0");
                    updates.Add("Quantite = @Quantite");
                    cmd.Parameters.AddWithValue("@Quantite", request.Quantite.Value);
                }

                if (request.PrixUnitaireHT.HasValue)
                {
                    if (request.PrixUnitaireHT.Value < 0)
                        throw new Exception("Le prix unitaire doit être >= 0");
                    updates.Add("PrixUnitaireHT = @PrixUnitaireHT");
                    cmd.Parameters.AddWithValue("@PrixUnitaireHT", request.PrixUnitaireHT.Value);
                }

                if (request.Coefficient.HasValue)
                {
                    updates.Add("Coefficient = @Coefficient");
                    cmd.Parameters.AddWithValue("@Coefficient", request.Coefficient.Value);
                }

                if (request.ReferenceExterne != null)
                {
                    updates.Add("ReferenceExterne = @ReferenceExterne");
                    cmd.Parameters.AddWithValue("@ReferenceExterne",
                        string.IsNullOrEmpty(request.ReferenceExterne) ? DBNull.Value : request.ReferenceExterne);
                }

                if (request.Notes != null)
                {
                    updates.Add("Notes = @Notes");
                    cmd.Parameters.AddWithValue("@Notes",
                        string.IsNullOrEmpty(request.Notes) ? DBNull.Value : request.Notes);
                }

                if (updates.Count == 0)
                {
                    throw new Exception("Aucune modification à apporter");
                }

                updates.Add("DateModification = @DateModification");

                cmd.CommandText = $@"
                    UPDATE DQE_DetailDebourseSec 
                    SET {string.Join(", ", updates)}
                    WHERE Id = @Id";

                await cmd.ExecuteNonQueryAsync();
                transaction.Commit();

                _logger.LogInformation($"Détail de déboursé {id} mis à jour");

                return await GetByIdAsync(id)
                    ?? throw new Exception("Erreur lors de la récupération du détail mis à jour");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"Erreur lors de la mise à jour du détail {id}");
                throw;
            }
        }

        /// <summary>
        /// Supprime un détail de déboursé (soft delete)
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                using var cmd = new SqlCommand(@"
                    UPDATE DQE_DetailDebourseSec 
                    SET Actif = 0, DateModification = @DateModification
                    WHERE Id = @Id", conn, transaction);

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                transaction.Commit();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation($"Détail de déboursé {id} supprimé (soft delete)");
                }

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"Erreur lors de la suppression du détail {id}");
                throw;
            }
        }

        // =============================================
        // RÉCAPITULATIFS ET STATISTIQUES
        // =============================================

        /// <summary>
        /// Récupère le récapitulatif des déboursés par type pour un item
        /// </summary>
        public async Task<RecapitulatifDebourseSecResponse> GetRecapitulatifAsync(int itemId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);

                // Récupérer les infos de l'item
                DQEItem? item = null;
                using (var itemCmd = new SqlCommand(@"
                    SELECT Id, Code, Designation, DeboursseSec
                    FROM DQE_Items
                    WHERE Id = @ItemId AND Actif = 1", conn))
                {
                    itemCmd.Parameters.AddWithValue("@ItemId", itemId);
                    await conn.OpenAsync();
                    using var reader = await itemCmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        item = new DQEItem
                        {
                            Id = reader.GetInt32(0),
                            Code = reader.GetString(1),
                            Designation = reader.GetString(2),
                            DeboursseSec = reader.GetDecimal(3)
                        };
                    }
                }

                if (item == null)
                {
                    throw new Exception($"Item {itemId} introuvable");
                }

                // Récupérer les détails groupés par type
                var detailsParType = new List<DetailParType>();
                using (var detailsCmd = new SqlCommand(@"
                    SELECT 
                        TypeDepense,
                        COUNT(*) as NombreLignes,
                        SUM(MontantHT) as MontantTotal
                    FROM DQE_DetailDebourseSec
                    WHERE ItemId = @ItemId AND Actif = 1
                    GROUP BY TypeDepense
                    ORDER BY SUM(MontantHT) DESC", conn))
                {
                    detailsCmd.Parameters.AddWithValue("@ItemId", itemId);
                    using var reader = await detailsCmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var typeDepense = reader.GetString(0);
                        var montantTotal = reader.GetDecimal(2);

                        detailsParType.Add(new DetailParType
                        {
                            TypeDepense = typeDepense,
                            TypeDepenseLabel = TypeDepenseEnum.GetLabel(typeDepense),
                            NombreLignes = reader.GetInt32(1),
                            MontantTotal = montantTotal,
                            PourcentageTotal = item.DeboursseSec > 0
                                ? (montantTotal / item.DeboursseSec * 100)
                                : 0
                        });
                    }
                }

                return new RecapitulatifDebourseSecResponse
                {
                    ItemId = item.Id,
                    ItemCode = item.Code,
                    ItemDesignation = item.Designation,
                    DebourseSecTotal = item.DeboursseSec,
                    DetailParType = detailsParType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors du récapitulatif pour item {itemId}");
                throw;
            }
        }

        /// <summary>
        /// Copie les détails de déboursé d'un item vers un autre
        /// </summary>
        public async Task<List<DQEDetailDebourseSec>> CopyDetailsAsync(int sourceItemId, int targetItemId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Vérifier que les items existent
                using (var checkCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM DQE_Items 
                    WHERE Id IN (@SourceId, @TargetId) AND Actif = 1", conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@SourceId", sourceItemId);
                    checkCmd.Parameters.AddWithValue("@TargetId", targetItemId);
                    var count = (int)await checkCmd.ExecuteScalarAsync();

                    if (count != 2)
                    {
                        throw new Exception("Item source ou cible introuvable");
                    }
                }

                // Copier les détails
                using (var copyCmd = new SqlCommand(@"
                    INSERT INTO DQE_DetailDebourseSec (
                        ItemId, TypeDepense, Designation, Description, Ordre,
                        Unite, Quantite, PrixUnitaireHT, Coefficient,
                        ReferenceExterne, Notes, DateCreation, Actif
                    )
                    SELECT 
                        @TargetItemId, TypeDepense, Designation, Description, Ordre,
                        Unite, Quantite, PrixUnitaireHT, Coefficient,
                        ReferenceExterne, Notes, @DateCreation, 1
                    FROM DQE_DetailDebourseSec
                    WHERE ItemId = @SourceItemId AND Actif = 1", conn, transaction))
                {
                    copyCmd.Parameters.AddWithValue("@SourceItemId", sourceItemId);
                    copyCmd.Parameters.AddWithValue("@TargetItemId", targetItemId);
                    copyCmd.Parameters.AddWithValue("@DateCreation", DateTime.UtcNow);

                    var rowsCopied = await copyCmd.ExecuteNonQueryAsync();
                    transaction.Commit();

                    _logger.LogInformation($"{rowsCopied} détails copiés de l'item {sourceItemId} vers {targetItemId}");
                }

                // Retourner les détails copiés
                return await GetByItemIdAsync(targetItemId);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"Erreur lors de la copie des détails de {sourceItemId} vers {targetItemId}");
                throw;
            }
        }

        /// <summary>
        /// Supprime tous les détails d'un item
        /// </summary>
        public async Task<int> DeleteAllByItemIdAsync(int itemId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                using var cmd = new SqlCommand(@"
                    UPDATE DQE_DetailDebourseSec 
                    SET Actif = 0, DateModification = @DateModification
                    WHERE ItemId = @ItemId AND Actif = 1", conn, transaction);

                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                transaction.Commit();

                _logger.LogInformation($"{rowsAffected} détails supprimés pour l'item {itemId}");
                return rowsAffected;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"Erreur lors de la suppression des détails de l'item {itemId}");
                throw;
            }
        }

        // =============================================
        // MÉTHODES PRIVÉES - MAPPING
        // =============================================

        /// <summary>
        /// Mappe un SqlDataReader vers DQEDetailDebourseSec
        /// </summary>
        private DQEDetailDebourseSec MapDetailFromReader(SqlDataReader reader)
        {
            return new DQEDetailDebourseSec
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                TypeDepense = reader.GetString(reader.GetOrdinal("TypeDepense")),
                Designation = reader.GetString(reader.GetOrdinal("Designation")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Description")),
                Ordre = reader.GetInt32(reader.GetOrdinal("Ordre")),
                Unite = reader.GetString(reader.GetOrdinal("Unite")),
                Quantite = reader.GetDecimal(reader.GetOrdinal("Quantite")),
                PrixUnitaireHT = reader.GetDecimal(reader.GetOrdinal("PrixUnitaireHT")),
                MontantHT = reader.GetDecimal(reader.GetOrdinal("MontantHT")),
                Coefficient = reader.IsDBNull(reader.GetOrdinal("Coefficient"))
                    ? 1.00M
                    : reader.GetDecimal(reader.GetOrdinal("Coefficient")),
                ReferenceExterne = reader.IsDBNull(reader.GetOrdinal("ReferenceExterne"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("ReferenceExterne")),
                Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Notes")),
                DateCreation = reader.GetDateTime(reader.GetOrdinal("DateCreation")),
                DateModification = reader.IsDBNull(reader.GetOrdinal("DateModification"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("DateModification")),
                Actif = reader.GetBoolean(reader.GetOrdinal("Actif"))
            };
        }
    }
}
