using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.Dqe;
using Saf_alu_ci_Api.Controllers.Projets;
using Saf_alu_ci_Api.Controllers.Utilisateurs;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.Dqe
{
    public class DQEService
    {
        private readonly string _connectionString;

        public DQEService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ========================================
        // MÉTHODES CRUD DE BASE
        // ========================================

        /// <summary>
        /// Récupère tous les DQE avec filtres optionnels
        /// </summary>
        public async Task<List<DQEListItemDTO>> GetAllAsync(string? statut = null, bool? isConverted = null)
        {
            var dqeList = new List<DQEListItemDTO>();

            using var conn = new SqlConnection(_connectionString);

            var query = @"
                SELECT 
                    d.Id, d.Reference, d.Nom, d.Statut, d.TotalRevenueHT, d.DateCreation,
                    d.IsConverted, d.LinkedProjectId, d.LinkedProjectNumber, d.ConvertedAt,
                    c.Id as ClientId,
                    CASE 
                        WHEN c.Nom IS NOT NULL AND c.Nom != '' THEN c.Nom
                        ELSE LTRIM(RTRIM(ISNULL(c.Nom, '') + ' ' + ISNULL(c.Nom, '')))
                    END as ClientNom,
                    (SELECT COUNT(*) FROM DQE_Lots WHERE DqeId = d.Id) as LotsCount
                FROM DQE d
                LEFT JOIN Clients c ON d.ClientId = c.Id
                WHERE d.Actif = 1";

            if (!string.IsNullOrEmpty(statut))
            {
                query += " AND d.Statut = @Statut";
            }

            if (isConverted.HasValue)
            {
                query += " AND d.IsConverted = @IsConverted";
            }

            query += " ORDER BY d.DateCreation DESC";

            using var cmd = new SqlCommand(query, conn);

            if (!string.IsNullOrEmpty(statut))
            {
                cmd.Parameters.AddWithValue("@Statut", statut);
            }

            if (isConverted.HasValue)
            {
                cmd.Parameters.AddWithValue("@IsConverted", isConverted.Value);
            }

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var dqe = new DQEListItemDTO
                {
                    Id = reader.GetInt32("Id"),
                    Reference = reader.GetString("Reference"),
                    Nom = reader.GetString("Nom"),
                    Statut = reader.GetString("Statut"),
                    TotalRevenueHT = reader.GetDecimal("TotalRevenueHT"),
                    LotsCount = reader.GetInt32("LotsCount"),
                    DateCreation = reader.GetDateTime("DateCreation"),
                    ClientId = reader.GetInt32("ClientId"),
                    ClientNom = reader.GetString("ClientNom"),
                    IsConverted = reader.GetBoolean("IsConverted") ? reader.GetBoolean("IsConverted") : false,
                    LinkedProjectId = reader.IsDBNull("LinkedProjectId") ? null : reader.GetInt32("LinkedProjectId"),
                    LinkedProjectNumber = reader.IsDBNull("LinkedProjectNumber") ? null : reader.GetString("LinkedProjectNumber"),
                    ConvertedAt = reader.IsDBNull("ConvertedAt") ? null : reader.GetDateTime("ConvertedAt"),
                    ConversionStatus = DetermineConversionStatus(
                        reader.GetString("Statut"),
                        reader.GetBoolean("IsConverted")
                    )
                };

                dqeList.Add(dqe);
            }

            return dqeList;
        }

        /// <summary>
        /// Récupère un DQE par son ID avec toute sa structure hiérarchique
        /// </summary>
        public async Task<DQEDetailDTO?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Récupérer le DQE principal
            var dqe = await GetDQEMainInfoAsync(conn, id);
            if (dqe == null) return null;

            // Récupérer les lots avec leur structure complète
            dqe.Lots = await GetDQELotsWithStructureAsync(conn, id);

            return dqe;
        }

        /// <summary>
        /// Crée un nouveau DQE avec sa structure complète
        /// </summary>
        public async Task<int> CreateAsync(CreateDQERequest request, int utilisateurId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                var reference = await GenerateReferenceAsync(conn, transaction);
                var dqeId = await CreateDQEMainAsync(conn, transaction, request, reference, utilisateurId);

                if (request.Lots != null && request.Lots.Any())
                {
                    await CreateDQEStructureAsync(conn, transaction, dqeId, request.Lots);
                }

                // ✅ NOUVEAU: Recalculer automatiquement les totaux
                await RecalculateDQETotalsAsync(conn, transaction, dqeId);

                transaction.Commit();
                return dqeId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        /// <summary>
        /// Met à jour les informations générales d'un DQE
        /// </summary>
        public async Task UpdateAsync(int id, UpdateDQERequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Mettre à jour les informations générales du DQE
                using var cmd = new SqlCommand(@"
            UPDATE DQE SET
                Nom = @Nom,
                Description = @Description,
                ClientId = @ClientId,
                DevisId = @DevisId,
                TauxTVA = @TauxTVA,
                Statut = @Statut,
                DateModification = @DateModification
            WHERE Id = @Id AND Actif = 1", conn, transaction);

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Nom", request.Nom);
                cmd.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ClientId", request.ClientId);
                cmd.Parameters.AddWithValue("@DevisId", request.DevisId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@TauxTVA", request.TauxTVA);
                cmd.Parameters.AddWithValue("@Statut", request.Statut);
                cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

                await cmd.ExecuteNonQueryAsync();

                // 2. ✅ NOUVEAU: Si des lots sont fournis, recréer toute la structure
                if (request.Lots != null && request.Lots.Any())
                {
                    // Supprimer l'ancienne structure (cascade via FK)
                    await DeleteDQEStructureAsync(conn, transaction, id);

                    // Recréer la nouvelle structure
                    await CreateDQEStructureAsync(conn, transaction, id, request.Lots);
                }

                // 3. Recalculer les totaux
                await RecalculateDQETotalsAsync(conn, transaction, id);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }                 /// Suppression logique d'un DQE
                          /// </summary>
        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE DQE SET Actif = 0, DateModification = @DateModification 
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ========================================
        // MÉTHODES DE VALIDATION
        // ========================================

        /// <summary>
        /// Valide un DQE (changement de statut à "validé")
        /// </summary>
        public async Task<bool> ValidateAsync(int id, int validePar)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE DQE SET
                    Statut = 'validé',
                    DateValidation = @DateValidation,
                    ValidePar = @ValidePar,
                    DateModification = @DateModification
                WHERE Id = @Id AND Statut != 'validé' AND Actif = 1", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DateValidation", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@ValidePar", validePar);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            return rowsAffected > 0;
        }

        /// <summary>
        /// Vérifie si un DQE peut être converti en projet
        /// </summary>
        public async Task<(bool canConvert, string reason)> CanConvertToProjectAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT Statut, IsConverted, 
                       (SELECT COUNT(*) FROM DQE_Lots WHERE DqeId = @Id) as LotsCount
                FROM DQE 
                WHERE Id = @Id AND Actif = 1", conn);

            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return (false, "DQE introuvable");
            }

            var statut = reader.GetString(0);
            var isConverted = reader.GetBoolean(1);
            var lotsCount = reader.GetInt32(2);

            if (isConverted)
            {
                return (false, "Ce DQE a déjà été converti en projet");
            }

            if (statut != "validé")
            {
                return (false, "Seuls les DQE validés peuvent être convertis");
            }

            if (lotsCount == 0)
            {
                return (false, "Le DQE doit contenir au moins un lot");
            }

            return (true, "Le DQE peut être converti");
        }

        // ========================================
        // MÉTHODES DE GÉNÉRATION
        // ========================================

        /// <summary>
        /// Génère une référence DQE unique (ex: DQE-2024-023)
        /// </summary>
        private async Task<string> GenerateReferenceAsync(SqlConnection conn, SqlTransaction transaction)
        {
            var annee = DateTime.UtcNow.Year;

            using var cmd = new SqlCommand(@"
                SELECT ISNULL(MAX(CAST(RIGHT(Reference, 3) AS INT)), 0) + 1
                FROM DQE 
                WHERE Reference LIKE @Pattern", conn, transaction);

            cmd.Parameters.AddWithValue("@Pattern", $"DQE-{annee}-%");

            var prochainNumero = (int)await cmd.ExecuteScalarAsync();
            return $"DQE-{annee}-{prochainNumero:000}";
        }

        // ========================================
        // MÉTHODES PRIVÉES - CRÉATION
        // ========================================

        private async Task<int> CreateDQEMainAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            CreateDQERequest request,
            string reference,
            int utilisateurId)
        {
            using var cmd = new SqlCommand(@"
    INSERT INTO DQE (
        Reference, Nom, Description, ClientId, DevisId, Statut,
        TotalRevenueHT, TauxTVA, MontantTVA, TotalTTC,
        DateCreation, DateModification, UtilisateurCreation, UtilisateurModification, Actif
    ) VALUES (
        @Reference, @Nom, @Description, @ClientId, @DevisId, @Statut,
        0, @TauxTVA, 0, 0,
        @DateCreation, @DateModification, @UtilisateurCreation, @UtilisateurModification, 1
    );
    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

            cmd.Parameters.AddWithValue("@Reference", reference);
            cmd.Parameters.AddWithValue("@Nom", request.Nom);
            cmd.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ClientId", request.ClientId);
            cmd.Parameters.AddWithValue("@DevisId", request.DevisId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Statut", "brouillon");
            cmd.Parameters.AddWithValue("@TauxTVA", request.TauxTVA);
            cmd.Parameters.AddWithValue("@DateCreation", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", utilisateurId);
            cmd.Parameters.AddWithValue("@UtilisateurModification", utilisateurId);

            return (int)await cmd.ExecuteScalarAsync();
        }

        private async Task CreateDQEStructureAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int dqeId,
            List<CreateDQELotRequest> lots)
        {
            foreach (var lot in lots)
            {
                // Créer le lot
                var lotId = await CreateLotAsync(conn, transaction, dqeId, lot);

                // Créer les chapitres du lot
                if (lot.Chapters != null && lot.Chapters.Any())
                {
                    foreach (var chapter in lot.Chapters)
                    {
                        var chapterId = await CreateChapterAsync(conn, transaction, lotId, chapter);

                        // Créer les postes du chapitre
                        if (chapter.Items != null && chapter.Items.Any())
                        {
                            await CreateItemsAsync(conn, transaction, chapterId, chapter.Items);
                        }
                    }
                }
            }
        }

        private async Task<int> CreateLotAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int dqeId,
            CreateDQELotRequest lot)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO DQE_Lots (DqeId, Code, Nom, Description, Ordre, TotalRevenueHT, PourcentageTotal)
                VALUES (@DqeId, @Code, @Nom, @Description, @Ordre, 0, 0);
                SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

            cmd.Parameters.AddWithValue("@DqeId", dqeId);
            cmd.Parameters.AddWithValue("@Code", lot.Code);
            cmd.Parameters.AddWithValue("@Nom", lot.Nom);
            cmd.Parameters.AddWithValue("@Description", lot.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ordre", lot.Ordre);

            return (int)await cmd.ExecuteScalarAsync();
        }

        private async Task<int> CreateChapterAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int lotId,
            CreateDQEChapterRequest chapter)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO DQE_Chapters (LotId, Code, Nom, Description, Ordre, TotalRevenueHT)
                VALUES (@LotId, @Code, @Nom, @Description, @Ordre, 0);
                SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

            cmd.Parameters.AddWithValue("@LotId", lotId);
            cmd.Parameters.AddWithValue("@Code", chapter.Code);
            cmd.Parameters.AddWithValue("@Nom", chapter.Nom);
            cmd.Parameters.AddWithValue("@Description", chapter.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ordre", chapter.Ordre);

            return (int)await cmd.ExecuteScalarAsync();
        }

        private async Task CreateItemsAsync(
             SqlConnection conn,
             SqlTransaction transaction,
             int chapterId,
             List<CreateDQEItemRequest> items)
        {
            foreach (var item in items)
            {
                using var cmd = new SqlCommand(@"
            INSERT INTO DQE_Items (
                ChapterId, Code, Designation, Description, Ordre,
                Unite, Quantite, PrixUnitaireHT, TotalRevenueHT, DeboursseSec
            ) VALUES (
                @ChapterId, @Code, @Designation, @Description, @Ordre,
                @Unite, @Quantite, @PrixUnitaireHT, @Quantite * @PrixUnitaireHT, @DeboursseSec
            )", conn, transaction);

                cmd.Parameters.AddWithValue("@ChapterId", chapterId);
                cmd.Parameters.AddWithValue("@Code", item.Code ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Designation", item.Designation ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", item.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Ordre", item.Ordre);
                cmd.Parameters.AddWithValue("@Unite", item.Unite ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Quantite", item.Quantite);
                cmd.Parameters.AddWithValue("@PrixUnitaireHT", item.PrixUnitaireHT);
                cmd.Parameters.AddWithValue("@DeboursseSec", item.DeboursseSec);

                await cmd.ExecuteNonQueryAsync();
            }
        }


        // ========================================
        // MÉTHODES PRIVÉES - LECTURE
        // ========================================

        private async Task<DQEDetailDTO?> GetDQEMainInfoAsync(SqlConnection conn, int id)
        {
            using var cmd = new SqlCommand(@"
                SELECT 
                    d.*,
                    c.Id as ClientId,
                    CASE 
                        WHEN c.Nom IS NOT NULL AND c.Nom != '' THEN c.Nom
                        ELSE LTRIM(RTRIM(ISNULL(c.RaisonSociale, '') + ' ' + ISNULL(c.Nom, '')))
                    END as ClientNom,
                    p.Id as ProjectId, p.Numero as ProjectNumero, p.Nom as ProjectNom, 
                    p.Statut as ProjectStatut, p.PourcentageAvancement as ProjectAvancement,
                    u.Prenom as ConvertedByPrenom, u.Nom as ConvertedByNom
                FROM DQE d
                LEFT JOIN Clients c ON d.ClientId = c.Id
                LEFT JOIN Projets p ON d.LinkedProjectId = p.Id
                LEFT JOIN Utilisateurs u ON d.ConvertedById = u.Id
                WHERE d.Id = @Id AND d.Actif = 1", conn);

            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync()) return null;

            var dqe = new DQEDetailDTO
            {
                Id = reader.GetInt32("Id"),
                Reference = reader.GetString("Reference"),
                Nom = reader.GetString("Nom"),
                Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                Statut = reader.GetString("Statut"),
                TotalRevenueHT = reader.GetDecimal("TotalRevenueHT"),
                TauxTVA = reader.GetDecimal("TauxTVA"),
                MontantTVA = reader.GetDecimal("MontantTVA"),
                TotalTTC = reader.GetDecimal("TotalTTC"),
                DateValidation = reader.IsDBNull("DateValidation") ? null : reader.GetDateTime("DateValidation"),
                ValidePar = reader.IsDBNull("ValidePar") ? null : reader.GetInt32("ValidePar"),
                DateCreation = reader.GetDateTime("DateCreation"),
                IsConverted = reader.GetBoolean("IsConverted"),
                Client = new ClientDTO
                {
                    Id = reader.GetInt32("ClientId"),
                    Nom = reader.GetString("ClientNom")
                }
            };

            // Infos projet lié si converti
            if (dqe.IsConverted && !reader.IsDBNull("ProjectId"))
            {
                dqe.LinkedProject = new ProjectLinkDTO
                {
                    Id = reader.GetInt32("ProjectId"),
                    Numero = reader.GetString("ProjectNumero"),
                    Nom = reader.GetString("ProjectNom"),
                    Statut = reader.GetString("ProjectStatut"),
                    PourcentageAvancement = reader.GetInt32("ProjectAvancement"),
                    ConvertedAt = reader.GetDateTime("ConvertedAt"),
                    ConvertedBy = !reader.IsDBNull("ConvertedByPrenom") ?
                        $"{reader.GetString("ConvertedByPrenom")} {reader.GetString("ConvertedByNom")}" : ""
                };
            }

            return dqe;
        }

        private async Task<List<DQELotDTO>> GetDQELotsWithStructureAsync(SqlConnection conn, int dqeId)
        {
            var lots = new List<DQELotDTO>();

            using var cmd = new SqlCommand(@"
                SELECT 
                    l.*,
                    (SELECT COUNT(*) FROM DQE_Chapters WHERE LotId = l.Id) as ChaptersCount,
                    (SELECT COUNT(*) FROM DQE_Items i 
                     INNER JOIN DQE_Chapters c ON i.ChapterId = c.Id 
                     WHERE c.LotId = l.Id) as ItemsCount
                FROM DQE_Lots l
                WHERE l.DqeId = @DqeId
                ORDER BY l.Ordre", conn);

            cmd.Parameters.AddWithValue("@DqeId", dqeId);

            // ✅ FIX: Utiliser un bloc using explicite pour fermer le reader avant les requêtes imbriquées
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    lots.Add(new DQELotDTO
                    {
                        Id = reader.GetInt32("Id"),
                        Code = reader.GetString("Code"),
                        Nom = reader.GetString("Nom"),
                        Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                        Ordre = reader.GetInt32("Ordre"),
                        TotalRevenueHT = reader.GetDecimal("TotalRevenueHT"),
                        PourcentageTotal = reader.GetDecimal("PourcentageTotal"),
                        ChaptersCount = reader.GetInt32("ChaptersCount"),
                        ItemsCount = reader.GetInt32("ItemsCount")
                    });
                }

            } // ✅ Le reader est maintenant fermé ici

            // ✅ Maintenant on peut faire les requêtes imbriquées sans conflit
            // Charger les chapitres pour chaque lot
            foreach (var lot in lots)
            {
                lot.Chapters = await GetDQEChaptersWithItemsAsync(conn, lot.Id);
            }

            return lots;
        }

        private async Task<List<DQEChapterDTO>> GetDQEChaptersWithItemsAsync(SqlConnection conn, int lotId)
        {
            var chapters = new List<DQEChapterDTO>();

            using var cmd = new SqlCommand(@"
                SELECT 
                    c.*,
                    (SELECT COUNT(*) FROM DQE_Items WHERE ChapterId = c.Id) as ItemsCount
                FROM DQE_Chapters c
                WHERE c.LotId = @LotId
                ORDER BY c.Ordre", conn);

            cmd.Parameters.AddWithValue("@LotId", lotId);

            // ✅ FIX: Même correction - fermer le reader avant les requêtes imbriquées
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    chapters.Add(new DQEChapterDTO
                    {
                        Id = reader.GetInt32("Id"),
                        Code = reader.GetString("Code"),
                        Nom = reader.GetString("Nom"),
                        Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                        Ordre = reader.GetInt32("Ordre"),
                        TotalRevenueHT = reader.GetDecimal("TotalRevenueHT"),
                        ItemsCount = reader.GetInt32("ItemsCount")
                    });
                }

            } // ✅ Le reader est fermé avant les requêtes imbriquées

            // ✅ Maintenant on peut charger les items sans conflit
            // Charger les items pour chaque chapitre
            foreach (var chapter in chapters)
            {
                chapter.Items = await GetDQEItemsAsync(conn, chapter.Id);
            }

            return chapters;
        }

        private async Task<List<DQEItemDTO>> GetDQEItemsAsync(SqlConnection conn, int chapterId)
        {
            var items = new List<DQEItemDTO>();

            using var cmd = new SqlCommand(@"
                SELECT *
                FROM DQE_Items
                WHERE ChapterId = @ChapterId
                ORDER BY Ordre", conn);

            cmd.Parameters.AddWithValue("@ChapterId", chapterId);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                items.Add(new DQEItemDTO
                {
                    Id = reader.GetInt32("Id"),
                    Code = reader.GetString("Code"),
                    Designation = reader.GetString("Designation"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Ordre = reader.GetInt32("Ordre"),
                    Unite = reader.GetString("Unite"),
                    Quantite = reader.GetDecimal("Quantite"),
                    PrixUnitaireHT = reader.GetDecimal("PrixUnitaireHT"),
                    TotalRevenueHT = reader.GetDecimal("TotalRevenueHT"),
                    DeboursseSec = reader.GetDecimal("DeboursseSec")
                });
            }

            return items;
        }

        // ========================================
        // MÉTHODES UTILITAIRES
        // ========================================

        private string DetermineConversionStatus(string statut, bool isConverted)
        {
            if (isConverted) return "converti";
            if (statut == "validé") return "convertible";
            return "non_convertible";
        }
        private async Task DeleteDQEStructureAsync(SqlConnection conn, SqlTransaction transaction, int dqeId)
        {
            // Supprimer les items
            using (var cmd1 = new SqlCommand(@"
        DELETE i
        FROM DQE_Items i
        INNER JOIN DQE_Chapters c ON i.ChapterId = c.Id
        INNER JOIN DQE_Lots l ON c.LotId = l.Id
        WHERE l.DqeId = @DqeId", conn, transaction))
            {
                cmd1.Parameters.AddWithValue("@DqeId", dqeId);
                await cmd1.ExecuteNonQueryAsync();
            }

            // Supprimer les chapitres
            using (var cmd2 = new SqlCommand(@"
        DELETE c
        FROM DQE_Chapters c
        INNER JOIN DQE_Lots l ON c.LotId = l.Id
        WHERE l.DqeId = @DqeId", conn, transaction))
            {
                cmd2.Parameters.AddWithValue("@DqeId", dqeId);
                await cmd2.ExecuteNonQueryAsync();
            }

            // Supprimer les lots
            using (var cmd3 = new SqlCommand(@"
        DELETE FROM DQE_Lots WHERE DqeId = @DqeId", conn, transaction))
            {
                cmd3.Parameters.AddWithValue("@DqeId", dqeId);
                await cmd3.ExecuteNonQueryAsync();
            }
        }
        private async Task RecalculateDQETotalsAsync(SqlConnection conn, SqlTransaction transaction, int dqeId)
        {
            // Étape 1: Recalculer les totaux des Chapitres (somme des items)
            using (var cmd1 = new SqlCommand(@"
        UPDATE c
        SET c.TotalRevenueHT = ISNULL(totals.Total, 0)
        FROM DQE_Chapters c
        LEFT JOIN (
            SELECT ChapterId, SUM(TotalRevenueHT) as Total
            FROM DQE_Items
            GROUP BY ChapterId
        ) totals ON c.Id = totals.ChapterId
        WHERE c.LotId IN (
            SELECT Id FROM DQE_Lots WHERE DqeId = @DqeId
        )", conn, transaction))
            {
                cmd1.Parameters.AddWithValue("@DqeId", dqeId);
                await cmd1.ExecuteNonQueryAsync();
            }

            // Étape 2: Recalculer les totaux et pourcentages des Lots (somme des chapitres)
            using (var cmd2 = new SqlCommand(@"
        -- Calculer le total global du DQE
        DECLARE @TotalDQE DECIMAL(18,2);
        
        SELECT @TotalDQE = ISNULL(SUM(c.TotalRevenueHT), 0)
        FROM DQE_Lots l
        INNER JOIN DQE_Chapters c ON c.LotId = l.Id
        WHERE l.DqeId = @DqeId;

        -- Mettre à jour chaque lot avec son total et pourcentage
        UPDATE l
        SET 
            l.TotalRevenueHT = ISNULL(totals.Total, 0),
            l.PourcentageTotal = CASE 
                WHEN @TotalDQE > 0 THEN (ISNULL(totals.Total, 0) / @TotalDQE) * 100
                ELSE 0
            END
        FROM DQE_Lots l
        LEFT JOIN (
            SELECT LotId, SUM(TotalRevenueHT) as Total
            FROM DQE_Chapters
            GROUP BY LotId
        ) totals ON l.Id = totals.LotId
        WHERE l.DqeId = @DqeId;", conn, transaction))
            {
                cmd2.Parameters.AddWithValue("@DqeId", dqeId);
                await cmd2.ExecuteNonQueryAsync();
            }

            // Étape 3: Récupérer le taux de TVA
            decimal tauxTVA;
            using (var cmd3 = new SqlCommand(@"
        SELECT TauxTVA FROM DQE WHERE Id = @DqeId", conn, transaction))
            {
                cmd3.Parameters.AddWithValue("@DqeId", dqeId);
                tauxTVA = (decimal)await cmd3.ExecuteScalarAsync();
            }

            // Étape 4: Mettre à jour les totaux du DQE principal (TotalHT, MontantTVA, TotalTTC)
            using (var cmd4 = new SqlCommand(@"
        DECLARE @TotalHT DECIMAL(18,2);
        DECLARE @MontantTVA DECIMAL(18,2);
        DECLARE @TotalTTC DECIMAL(18,2);

        -- Calculer le total HT (somme des lots)
        SELECT @TotalHT = ISNULL(SUM(TotalRevenueHT), 0)
        FROM DQE_Lots
        WHERE DqeId = @DqeId;

        -- Calculer la TVA
        SET @MontantTVA = @TotalHT * (@TauxTVA / 100.0);

        -- Calculer le total TTC
        SET @TotalTTC = @TotalHT + @MontantTVA;

        -- Mettre à jour le DQE
        UPDATE DQE
        SET 
            TotalRevenueHT = @TotalHT,
            MontantTVA = @MontantTVA,
            TotalTTC = @TotalTTC,
            DateModification = GETUTCDATE()
        WHERE Id = @DqeId;", conn, transaction))
            {
                cmd4.Parameters.AddWithValue("@DqeId", dqeId);
                cmd4.Parameters.AddWithValue("@TauxTVA", tauxTVA);
                await cmd4.ExecuteNonQueryAsync();
            }
        }
    }
}