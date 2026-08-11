using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.SousTraitants;
using Saf_alu_ci_Api.Controllers.Tresorerie;
using Saf_alu_ci_Api.Controllers.Utilisateurs;
using System.Data;
using System.Transactions;

namespace Saf_alu_ci_Api.Controllers.Projets
{
    public class ProjetService
    {
        private readonly string _connectionString;
        public ProjetService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // SIGNATURE : ajouter userId et userRole
        public async Task<List<Projet>> GetAllAsync(int? userId = null, string? userRole = null)
        {
            var projets = new List<Projet>();

            // Rôles qui voient TOUS les projets
            var rolesAdmin = new[] { "super_admin", "admin", "comptable" };
            bool filtrerParChef = userId.HasValue
                && !string.IsNullOrEmpty(userRole)
                && !rolesAdmin.Contains(userRole);

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
            SELECT p.*,
                   c.Nom as ClientNom,
                   c.RaisonSociale as ClientRaisonSociale,
                   c.Email as ClientEmail,
                   c.Telephone as ClientTelephone,
                   c.Adresse as ClientAdresse,
                   u.Prenom as ChefProjetPrenom,
                   u.Nom as ChefProjetNom,
                   conv.Prenom as DqeConvertedByPrenom,
                   conv.Nom as DqeConvertedByNom,
                   ISNULL((SELECT SUM(CoutReel) FROM EtapesProjets
                           WHERE ProjetId = p.Id AND EstActif = 1), 0) as CoutReelCalcule
            FROM Projets p
            LEFT JOIN Clients c    ON p.ClientId          = c.Id
            LEFT JOIN Utilisateurs u    ON p.ChefProjetId = u.Id
            LEFT JOIN Utilisateurs conv ON p.DqeConvertedById = conv.Id
            WHERE p.Actif = 1
              AND (
                  @FiltrerParChef = 0
                  OR p.ChefProjetId = @UserId
                  OR EXISTS (
                      SELECT 1 FROM ProjetsChefsProjet pcp
                      WHERE pcp.ProjetId = p.Id AND pcp.UtilisateurId = @UserId
                  )
              )
            ORDER BY p.DateCreation DESC", conn);

            cmd.Parameters.AddWithValue("@FiltrerParChef", filtrerParChef ? 1 : 0);
            cmd.Parameters.AddWithValue("@UserId", userId ?? (object)DBNull.Value);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var projet = MapToProjet(reader);
                projet.CoutReel = reader.GetDecimal("CoutReelCalcule");
                projets.Add(projet);
            }
            reader.Close();

            foreach (var projet in projets)
            {
                projet.Etapes = await GetEtapesProjetAsync(conn, projet.Id);
                // 🆕 Charger les chefs
                projet.ChefsProjet = await LoadChefsProjetAsync(conn, projet.Id);

                if (projet.Etapes != null && projet.Etapes.Any())
                {
                    var etapesActives = projet.Etapes.Where(e => e.EstActif).ToList();
                    if (etapesActives.Any())
                    {
                        var totalAvancement = etapesActives.Sum(x => x.PourcentageAvancement);
                        var totalEtape = etapesActives.Count(x => x.LinkedDqeLotName == null);
                        projet.PourcentageAvancement = totalEtape > 0
                            ? Convert.ToInt32(totalAvancement / totalEtape) : 0;
                        projet.DepenseGlobale = Convert.ToDecimal(etapesActives.Sum(x => x.Depense));
                    }
                }
            }

            return projets;
        }
        public async Task<Projet?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT p.*, 
                       c.Nom as ClientNom, c.RaisonSociale as ClientRaisonSociale, c.Email as ClientEmail,c.Telephone as ClientTelephone, c.Adresse as ClientAdresse, 
                       u.Prenom as ChefProjetPrenom, u.Nom as ChefProjetNom,
                       conv.Prenom as DqeConvertedByPrenom, conv.Nom as DqeConvertedByNom
                FROM Projets p
                LEFT JOIN Clients c ON p.ClientId = c.Id
                LEFT JOIN Utilisateurs u ON p.ChefProjetId = u.Id
                LEFT JOIN Utilisateurs conv ON p.DqeConvertedById = conv.Id
                WHERE p.Id = @Id AND p.Actif = 1", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var projet = MapToProjet(reader);
                reader.Close();

                // Charger les étapes
                projet.Etapes = await GetEtapesProjetAsync(conn, id);
                projet.ChefsProjet = await LoadChefsProjetAsync(conn, projet.Id);
                // Recalculer le CoutReel depuis les étapes pour garantir la cohérence
                if (projet.Etapes != null && projet.Etapes.Any())
                {
                    projet.CoutReel = projet.Etapes.Sum(e => e.CoutReel);
                    var nbEtap = projet.Etapes.Where(x => x.LinkedDqeLotName == null).Count();
                    projet.PourcentageAvancement = (projet.Etapes.Sum(x => x.PourcentageAvancement) / nbEtap);

                }

                return projet;
            }

            return null;
        }

        // ========================================
        // NOUVELLES MÉTHODES - PROJETS DEPUIS DQE
        // ========================================

        /// <summary>
        /// Récupère tous les projets créés depuis un DQE
        /// </summary>
        public async Task<List<Projet>> GetProjectsFromDQEAsync()
        {
            var projets = new List<Projet>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT p.*, 
                       c.Nom as ClientNom, c.RaisonSociale as ClientRaisonSociale,
                       u.Prenom as ChefProjetPrenom, u.Nom as ChefProjetNom,
                       conv.Prenom as DqeConvertedByPrenom, conv.Nom as DqeConvertedByNom,
                       ISNULL((SELECT SUM(CoutReel) FROM EtapesProjets WHERE ProjetId = p.Id), 0) as CoutReelCalcule
                FROM Projets p
                LEFT JOIN Clients c ON p.ClientId = c.Id
                LEFT JOIN Utilisateurs u ON p.ChefProjetId = u.Id
                LEFT JOIN Utilisateurs conv ON p.DqeConvertedById = conv.Id
                WHERE p.Actif = 1 
                  AND p.IsFromDqeConversion = 1
                ORDER BY p.DqeConvertedAt DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var projet = MapToProjet(reader);
                // Utiliser le CoutReel calculé depuis les étapes
                projet.CoutReel = reader.GetDecimal("CoutReelCalcule");
                projets.Add(projet);
            }

            return projets;
        }

        // ========================================
        // MÉTHODES EXISTANTES (mises à jour)
        // ========================================

        public async Task<List<TypeProjet>> GetAllTypesAsync()
        {
            var types = new List<TypeProjet>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM TypesProjets WHERE Actif = 1 ORDER BY Nom", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                types.Add(new TypeProjet
                {
                    Id = reader.GetInt32("Id"),
                    Nom = reader.GetString("Nom"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Couleur = reader.GetString("Couleur"),
                    Actif = reader.GetBoolean("Actif")
                });
            }

            return types;
        }

        public async Task<string> GenerateNumeroAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("sp_GenererNumeroProjet", conn);
            cmd.CommandType = System.Data.CommandType.StoredProcedure;

            var outputParam = new SqlParameter("@NouveauNumero", System.Data.SqlDbType.NVarChar, 20)
            {
                Direction = System.Data.ParameterDirection.Output
            };
            cmd.Parameters.Add(outputParam);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return outputParam.Value.ToString();
        }

        public async Task<int> CreateAsync(Projet projet, List<int>? chefProjetIds = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Générer le numéro automatiquement
                if (string.IsNullOrEmpty(projet.Numero))
                {
                    projet.Numero = await GenerateNumeroWithTransactionAsync(conn, transaction);
                }

                // Calculer le CoutReel total depuis les étapes
                if (projet.Etapes != null && projet.Etapes.Any())
                {
                    projet.CoutReel = projet.Etapes.Sum(e => e.CoutReel);
                }

                // Créer le projet
                using var cmd = new SqlCommand(@"
                    INSERT INTO Projets (Numero, Nom, Description, ClientId,DevisId, Statut,
                                       DateDebut, DateFinPrevue, BudgetInitial, BudgetRevise, CoutReel, DepenseGlobale,
                                       AdresseChantier, CodePostalChantier, VilleChantier, PourcentageAvancement,
                                       ChefProjetId, CompteId, DepotId, DateCreation, DateModification, UtilisateurCreation, Actif,
                                       LinkedDqeId, LinkedDqeReference, LinkedDqeName, LinkedDqeBudgetHT,
                                       IsFromDqeConversion, DqeConvertedAt, DqeConvertedById)
                    VALUES (@Numero, @Nom, @Description, @ClientId, @DevisId, @Statut,
                           @DateDebut, @DateFinPrevue, @BudgetInitial, @BudgetRevise, @CoutReel, @DepenseGlobale,
                           @AdresseChantier, @CodePostalChantier, @VilleChantier, @PourcentageAvancement,
                           @ChefProjetId, @CompteId, @DepotId, @DateCreation, @DateModification, @UtilisateurCreation, @Actif,
                           @LinkedDqeId, @LinkedDqeReference, @LinkedDqeName, @LinkedDqeBudgetHT,
                           @IsFromDqeConversion, @DqeConvertedAt, @DqeConvertedById);
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                AddProjetParameters(cmd, projet);
                var projetId = (int)await cmd.ExecuteScalarAsync();

                // Ajouter les étapes
                if (projet.Etapes != null && projet.Etapes.Any())
                {
                    await CreateEtapesAsync(conn, transaction, projetId, projet.Etapes);
                }
                var chefIds = chefProjetIds ?? new List<int>();
                if (!chefIds.Any() && projet.ChefProjetId.HasValue)
                    chefIds = new List<int> { projet.ChefProjetId.Value };

                if (chefIds.Any())
                    await SyncChefsProjetAsync(conn, transaction, projetId, chefIds);

                transaction.Commit();
                return projetId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Met à jour un projet - UNIQUEMENT les champs fournis (non null)
        /// </summary>
        public async Task<bool> UpdateAsync(int id, UpdateProjetRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Vérifier que le projet existe
                var existing = await GetByIdForUpdateAsync(conn, transaction, id);
                if (existing == null)
                {
                    throw new Exception("Projet non trouvé");
                }

                // 2. Construire la requête SQL dynamique
                var setClause = new List<string>();
                var cmd = new SqlCommand { Connection = conn, Transaction = transaction };

                // Ajouter les champs à mettre à jour UNIQUEMENT s'ils sont fournis
                if (!string.IsNullOrEmpty(request.Nom))
                {
                    setClause.Add("Nom = @Nom");
                    cmd.Parameters.AddWithValue("@Nom", request.Nom);
                }

                if (request.Description != null) // Permet de définir à null si besoin
                {
                    setClause.Add("Description = @Description");
                    cmd.Parameters.AddWithValue("@Description", request.Description);
                }

                if (request.ClientId.HasValue)
                {
                    setClause.Add("ClientId = @ClientId");
                    cmd.Parameters.AddWithValue("@ClientId", request.ClientId.Value);
                }

                //if (request.TypeProjetId.HasValue)
                //{
                //    setClause.Add("TypeProjetId = @TypeProjetId");
                //    cmd.Parameters.AddWithValue("@TypeProjetId", request.TypeProjetId.Value);
                //}

                if (request.DevisId.HasValue)
                {
                    setClause.Add("DevisId = @DevisId");
                    cmd.Parameters.AddWithValue("@DevisId", request.DevisId);
                }

                if (request.DateDebut.HasValue)
                {
                    setClause.Add("DateDebut = @DateDebut");
                    cmd.Parameters.AddWithValue("@DateDebut", request.DateDebut.Value);
                }

                if (request.DateFinPrevue.HasValue)
                {
                    setClause.Add("DateFinPrevue = @DateFinPrevue");
                    cmd.Parameters.AddWithValue("@DateFinPrevue", request.DateFinPrevue.Value);
                }

                if (request.BudgetInitial.HasValue)
                {
                    setClause.Add("BudgetInitial = @BudgetInitial");
                    cmd.Parameters.AddWithValue("@BudgetInitial", request.BudgetInitial.Value);
                }

                if (request.BudgetRevise.HasValue)
                {
                    setClause.Add("BudgetRevise = @BudgetRevise");
                    cmd.Parameters.AddWithValue("@BudgetRevise", request.BudgetRevise.Value);
                }

                if (!string.IsNullOrEmpty(request.Statut))
                {
                    // Valider le statut
                    var statutsValides = new[] { "Planification", "EnCours", "Suspendu", "Termine", "Annule" };
                    if (!statutsValides.Contains(request.Statut))
                    {
                        throw new ArgumentException($"Statut invalide. Valeurs autorisées : {string.Join(", ", statutsValides)}");
                    }
                    setClause.Add("Statut = @Statut");
                    cmd.Parameters.AddWithValue("@Statut", request.Statut);
                }

                if (request.AdresseChantier != null)
                {
                    setClause.Add("AdresseChantier = @AdresseChantier");
                    cmd.Parameters.AddWithValue("@AdresseChantier", request.AdresseChantier);
                }

                if (request.CodePostalChantier != null)
                {
                    setClause.Add("CodePostalChantier = @CodePostalChantier");
                    cmd.Parameters.AddWithValue("@CodePostalChantier", request.CodePostalChantier);
                }

                if (request.VilleChantier != null)
                {
                    setClause.Add("VilleChantier = @VilleChantier");
                    cmd.Parameters.AddWithValue("@VilleChantier", request.VilleChantier);
                }

                if (request.ChefProjetId.HasValue)
                {
                    setClause.Add("ChefProjetId = @ChefProjetId");
                    cmd.Parameters.AddWithValue("@ChefProjetId", request.ChefProjetId);
                }

                if (request.CompteId.HasValue)
                {
                    setClause.Add("CompteId = @CompteId");
                    cmd.Parameters.AddWithValue("@CompteId", request.CompteId);
                }

                if (request.DepotId.HasValue)
                {
                    setClause.Add("DepotId = @DepotId");
                    cmd.Parameters.AddWithValue("@DepotId", request.DepotId);
                }

                if (request.PourcentageAvancement.HasValue)
                {
                    if (request.PourcentageAvancement.Value < 0 || request.PourcentageAvancement.Value > 100)
                    {
                        throw new ArgumentException("Le pourcentage d'avancement doit être entre 0 et 100");
                    }
                    setClause.Add("PourcentageAvancement = @PourcentageAvancement");
                    cmd.Parameters.AddWithValue("@PourcentageAvancement", request.PourcentageAvancement.Value);
                }

                // Toujours mettre à jour DateModification
                setClause.Add("DateModification = @DateModification");
                cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

                // Si aucun champ à mettre à jour, retourner
                if (setClause.Count == 1) // Seulement DateModification
                {
                    transaction.Commit();
                    return true;
                }

                // 3. Exécuter la mise à jour
                cmd.CommandText = $@"
            UPDATE Projets 
            SET {string.Join(", ", setClause)}
            WHERE Id = @Id AND Actif = 1";

                cmd.Parameters.AddWithValue("@Id", id);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    throw new Exception("Aucune ligne mise à jour");
                }

                // 4. Gérer les étapes si fournies
                if (request.Etapes != null && request.Etapes.Any())
                {
                    await UpdateEtapesAsync(conn, transaction, id, request.Etapes);
                }
                if (request.ChefProjetIds != null)
                {
                    // Si ChefProjetId legacy fourni aussi, s'assurer qu'il est dans la liste
                    var chefIds = request.ChefProjetIds.ToList();
                    if (request.ChefProjetId.HasValue && !chefIds.Contains(request.ChefProjetId.Value))
                        chefIds.Insert(0, request.ChefProjetId.Value);

                    await SyncChefsProjetAsync(conn, transaction, id, chefIds);
                }
                else if (request.ChefProjetId.HasValue)
                {
                    // Rétrocompatibilité : si seulement ChefProjetId fourni, l'ajouter comme principal
                    await SyncChefsProjetAsync(conn, transaction, id, new List<int> { request.ChefProjetId.Value });
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        private async Task UpdateEtapesAsync(
            SqlConnection conn, SqlTransaction transaction,
            int projetId, List<UpdateEtapeProjetRequest> etapes)
        {
            foreach (var etape in etapes)
            {
                // ============================================
                // Résoudre IdSousTraitant et TypeResponsable
                // en priorité depuis SousTraitantIds (nouveau)
                // puis depuis IdSousTraitant (legacy)
                // ============================================
                int? idSousTraitantResolu = null;
                string typeResponsableResolu = "Interne";

                if (etape.SousTraitantIds != null && etape.SousTraitantIds.Any())
                {
                    // Nouveau champ multi-ST : le premier devient le "principal"
                    idSousTraitantResolu = etape.SousTraitantIds[0];
                    typeResponsableResolu = "SousTraitant";
                }
                //else if (etape.IdSousTraitant.HasValue)
                //{
                //    // Rétrocompatibilité : un seul ST envoyé
                //    idSousTraitantResolu = etape.IdSousTraitant.Value;
                //    typeResponsableResolu = "SousTraitant";
                //}
                // sinon : Interne, idSousTraitantResolu reste null

                if (etape.Id.HasValue)
                {
                    // ============================================
                    // MISE À JOUR D'UNE ÉTAPE EXISTANTE
                    // ============================================

                    var setClause = new List<string>();
                    var cmd = new SqlCommand { Connection = conn, Transaction = transaction };

                    if (etape.EstActif != false)
                    {
                        if (!string.IsNullOrEmpty(etape.Nom))
                        {
                            setClause.Add("Nom = @Nom");
                            cmd.Parameters.AddWithValue("@Nom", etape.Nom);
                        }

                        if (etape.Description != null)
                        {
                            setClause.Add("Description = @Description");
                            cmd.Parameters.AddWithValue("@Description", etape.Description);
                        }

                        if (etape.DateDebut.HasValue)
                        {
                            setClause.Add("DateDebut = @DateDebut");
                            cmd.Parameters.AddWithValue("@DateDebut", etape.DateDebut.Value);
                        }

                        if (etape.DateFinPrevue.HasValue)
                        {
                            setClause.Add("DateFinPrevue = @DateFinPrevue");
                            cmd.Parameters.AddWithValue("@DateFinPrevue", etape.DateFinPrevue.Value);
                        }

                        if (etape.BudgetPrevu.HasValue)
                        {
                            setClause.Add("BudgetPrevu = @BudgetPrevu");
                            cmd.Parameters.AddWithValue("@BudgetPrevu", etape.BudgetPrevu.Value);
                        }

                        if (etape.CoutReel.HasValue)
                        {
                            setClause.Add("CoutReel = @CoutReel");
                            cmd.Parameters.AddWithValue("@CoutReel", etape.CoutReel.Value);
                        }

                        if (!string.IsNullOrEmpty(etape.Statut))
                        {
                            setClause.Add("Statut = @Statut");
                            cmd.Parameters.AddWithValue("@Statut", etape.Statut);
                        }

                        if (etape.ResponsableId.HasValue)
                        {
                            setClause.Add("ResponsableId = @ResponsableId");
                            cmd.Parameters.AddWithValue("@ResponsableId", etape.ResponsableId);
                        }

                        if (!string.IsNullOrEmpty(etape.TypeResponsable))
                        {
                            setClause.Add("TypeResponsable = @TypeResponsable");
                            cmd.Parameters.AddWithValue("@TypeResponsable", etape.TypeResponsable);
                        }
                    }

                    // Soft delete
                    if (etape.EstActif == false)
                    {
                        setClause.Add("EstActif = @EstActif");
                        cmd.Parameters.AddWithValue("@EstActif", false);
                    }

                    // 🆕 IdSousTraitant résolu + TypeResponsable
                    // On l'applique seulement si SousTraitantIds OU IdSousTraitant a été fourni
                    // (null explicite = pas de champ envoyé → on ne touche pas à la colonne legacy)
                    if (etape.SousTraitantIds != null)
                    {
                        setClause.Add("IdSousTraitant = @IdSousTraitant");
                        cmd.Parameters.AddWithValue(
                            "@IdSousTraitant",
                            idSousTraitantResolu.HasValue ? (object)idSousTraitantResolu.Value : DBNull.Value);

                        // Ne pas écraser TypeResponsable si déjà ajouté via etape.TypeResponsable
                        if (!setClause.Contains("TypeResponsable = @TypeResponsable"))
                        {
                            setClause.Add("TypeResponsable = @TypeResponsable");
                            cmd.Parameters.AddWithValue("@TypeResponsable", typeResponsableResolu);
                        }
                    }

                    // Toujours mettre à jour DateModification
                    setClause.Add("DateModification = @DateModification");
                    cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

                    if (setClause.Any())
                    {
                        cmd.CommandText = $@"
                    UPDATE EtapesProjets
                    SET {string.Join(", ", setClause)}
                    WHERE Id = @EtapeId AND ProjetId = @ProjetId";

                        cmd.Parameters.AddWithValue("@EtapeId", etape.Id.Value);
                        cmd.Parameters.AddWithValue("@ProjetId", projetId);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 🆕 Synchroniser EtapesSousTraitants si SousTraitantIds est fourni
                    if (etape.SousTraitantIds != null)
                    {
                        await SyncEtapeSousTraitantsAsync(conn, transaction, etape.Id.Value, etape.SousTraitantIds);
                    }
                }
                else
                {
                    // ============================================
                    // CRÉATION D'UNE NOUVELLE ÉTAPE
                    // ============================================

                    var cmd = new SqlCommand(@"
                INSERT INTO EtapesProjets (
                    ProjetId, Nom, Description, DateDebut, DateFinPrevue,
                    BudgetPrevu, CoutReel, Statut, ResponsableId, TypeResponsable,
                    IdSousTraitant, Ordre, PourcentageAvancement, EstActif,
                    DateCreation, DateModification
                )
                VALUES (
                    @ProjetId, @Nom, @Description, @DateDebut, @DateFinPrevue,
                    @BudgetPrevu, @CoutReel, @Statut, @ResponsableId, @TypeResponsable,
                    @IdSousTraitant,
                    (SELECT ISNULL(MAX(Ordre), 0) + 1 FROM EtapesProjets WHERE ProjetId = @ProjetId),
                    0, @EstActif,
                    @DateCreation, @DateModification
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT)",
                        conn, transaction);

                    cmd.Parameters.AddWithValue("@ProjetId", projetId);
                    cmd.Parameters.AddWithValue("@Nom", etape.Nom ?? "");
                    cmd.Parameters.AddWithValue("@Description", etape.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateDebut", etape.DateDebut ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateFinPrevue", etape.DateFinPrevue ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@BudgetPrevu", etape.BudgetPrevu ?? 0);
                    cmd.Parameters.AddWithValue("@CoutReel", etape.CoutReel ?? 0);
                    cmd.Parameters.AddWithValue("@Statut", etape.Statut ?? "NonCommence");
                    cmd.Parameters.AddWithValue("@ResponsableId", etape.ResponsableId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TypeResponsable", typeResponsableResolu);
                    cmd.Parameters.AddWithValue("@IdSousTraitant",
                        idSousTraitantResolu.HasValue ? (object)idSousTraitantResolu.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@EstActif", etape.EstActif ?? true);
                    cmd.Parameters.AddWithValue("@DateCreation", DateTime.UtcNow);
                    cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

                    // 🆕 Récupérer l'Id de la nouvelle étape (nécessaire pour la table junction)
                    var newEtapeId = (int)await cmd.ExecuteScalarAsync();

                    // 🆕 Insérer les sous-traitants dans EtapesSousTraitants
                    if (etape.SousTraitantIds != null && etape.SousTraitantIds.Any())
                    {
                        await SyncEtapeSousTraitantsAsync(conn, transaction, newEtapeId, etape.SousTraitantIds);
                    }
                }
            }
        }

        /// <summary>
        /// Remplace en bloc les sous-traitants d'une étape dans EtapesSousTraitants.
        /// Appelée à chaque mise à jour ou création d'une étape portant une liste SousTraitantIds.
        /// </summary>
        private async Task SyncEtapeSousTraitantsAsync(
            SqlConnection conn, SqlTransaction transaction,
            int etapeId, List<int> sousTraitantIds)
        {
            // 1. Supprimer les anciennes liaisons
            using (var deleteCmd = new SqlCommand(
                "DELETE FROM EtapesSousTraitants WHERE EtapeProjetId = @EtapeId",
                conn, transaction))
            {
                deleteCmd.Parameters.AddWithValue("@EtapeId", etapeId);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            // 2. Insérer la nouvelle liste (en ignorant les doublons éventuels)
            foreach (var stId in sousTraitantIds.Distinct())
            {
                using var insertCmd = new SqlCommand(@"
            INSERT INTO EtapesSousTraitants (EtapeProjetId, SousTraitantId, Statut, DateCreation, DateModification)
            VALUES (@EtapeId, @SousTraitantId, 'EnAttente', @Now, @Now)",
                    conn, transaction);

                insertCmd.Parameters.AddWithValue("@EtapeId", etapeId);
                insertCmd.Parameters.AddWithValue("@SousTraitantId", stId);
                insertCmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);

                await insertCmd.ExecuteNonQueryAsync();
            }
        }
        private async Task<Projet?> GetByIdForUpdateAsync(SqlConnection conn, SqlTransaction transaction, int id)
        {
            using var cmd = new SqlCommand(@"
        SELECT Id, Nom, Statut, Actif 
        FROM Projets 
        WHERE Id = @Id AND Actif = 1", conn, transaction);

            cmd.Parameters.AddWithValue("@Id", id);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new Projet
                {
                    Id = reader.GetInt32("Id"),
                    Nom = reader.GetString("Nom"),
                    Statut = reader.GetString("Statut"),
                    Actif = reader.GetBoolean("Actif")
                };
            }

            return null;
        }
        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("UPDATE Projets SET Actif = 0 WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateStatutAsync(int id, string statut)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Projets 
                SET Statut = @Statut, DateModification = @DateModification 
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Statut", statut);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateAvancementAsync(int id, int pourcentage)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Projets 
                SET PourcentageAvancement = @Pourcentage, DateModification = @DateModification 
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Pourcentage", pourcentage);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<Projet>> GetProjetsEnRetardAsync()
        {
            var projets = new List<Projet>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT p.*, 
                       c.Nom as ClientNom, c.RaisonSociale as ClientRaisonSociale,
                       u.Prenom as ChefProjetPrenom, u.Nom as ChefProjetNom,
                       conv.Prenom as DqeConvertedByPrenom, conv.Nom as DqeConvertedByNom,
                       ISNULL((SELECT SUM(CoutReel) FROM EtapesProjets WHERE ProjetId = p.Id), 0) as CoutReelCalcule
                FROM Projets p
                LEFT JOIN Clients c ON p.ClientId = c.Id
                LEFT JOIN Utilisateurs u ON p.ChefProjetId = u.Id
                LEFT JOIN Utilisateurs conv ON p.DqeConvertedById = conv.Id
                WHERE p.Actif = 1 
                  AND p.DateFinPrevue < GETDATE()
                  AND p.PourcentageAvancement < 100
                  AND p.Statut != 'Termine'
                ORDER BY p.DateFinPrevue ASC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var projet = MapToProjet(reader);
                // Utiliser le CoutReel calculé depuis les étapes
                projet.CoutReel = reader.GetDecimal("CoutReelCalcule");
                projets.Add(projet);
            }

            return projets;
        }

        public async Task<List<EtapeProjet>> GetEtapesProjetAsync(int projetId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return await GetEtapesProjetAsync(conn, projetId);
        }

        public async Task UpdateEtapeAvancementAsync(int etapeId, UpdateAvancementRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            //using var cmd = new SqlCommand(@"
            //    UPDATE EtapesProjets 
            //    SET PourcentageAvancement = @Pourcentage , Statut = @Statut
            //    WHERE Id = @Id", conn);

            //cmd.Parameters.AddWithValue("@Id", etapeId);
            //cmd.Parameters.AddWithValue("@Pourcentage", request.PourcentageAvancement);

            //cmd.Parameters.AddWithValue("@Statut", request.Statut);

            //await conn.OpenAsync();
            //await cmd.ExecuteNonQueryAsync();



            var setClause = new List<string>();
            var cmd = new SqlCommand { Connection = conn, /*Transaction = transaction*/ };

            if (request.PourcentageAvancement >= 1)
            {
                setClause.Add("PourcentageAvancement = @PourcentageAvancement");
                cmd.Parameters.AddWithValue("@PourcentageAvancement", request.PourcentageAvancement);
            }
            if (!string.IsNullOrEmpty(request.Statut))
            {
                setClause.Add("Statut = @Statut");
                cmd.Parameters.AddWithValue("@Statut", request.Statut);
            }
            if (setClause.Any())
            {
                cmd.CommandText = $@"
                    UPDATE EtapesProjets 
                    SET {string.Join(", ", setClause)}
                    WHERE Id = @Id";

                cmd.Parameters.AddWithValue("@Id", etapeId);
                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            // TODO: Ajouter dans une table d'historique si besoin (Note, Commentaire)
        }

        /// <summary>
        /// Recalcule le CoutReel du projet en faisant la somme des CoutReel de toutes ses étapes
        /// </summary>
        public async Task RecalculateCoutReelAsync(int projetId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Projets 
                SET CoutReel = (
                    SELECT ISNULL(SUM(CoutReel), 0) 
                    FROM EtapesProjets 
                    WHERE ProjetId = @ProjetId
                ),
                DateModification = @DateModification
                WHERE Id = @ProjetId", conn);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Met à jour la dépense d'une étape (débit ou crédit depuis la trésorerie)
        /// </summary>
        public async Task UpdateEtapeDepenseAsync(int etapeId, UpdateDepenseRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Récupérer l'étape pour obtenir la dépense actuelle et le ProjetId
                decimal depenseActuelle = 0;
                int projetId = 0;

                using (var selectCmd = new SqlCommand(@"
                    SELECT Depense, ProjetId FROM EtapesProjets WHERE Id = @Id", conn, transaction))
                {
                    selectCmd.Parameters.AddWithValue("@Id", etapeId);
                    using var reader = await selectCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        depenseActuelle = reader.GetDecimal(0);
                        projetId = reader.GetInt32(1);
                    }
                    else
                    {
                        throw new Exception($"Étape avec l'ID {etapeId} non trouvée");
                    }
                }

                // Calculer la nouvelle dépense selon le type d'opération
                decimal nouvelleDepense = depenseActuelle;
                if (request.TypeOperation.ToUpper() == "DEBIT")
                {
                    nouvelleDepense += request.Montant; // Ajouter au débit
                }
                else if (request.TypeOperation.ToUpper() == "CREDIT")
                {
                    nouvelleDepense -= request.Montant; // Soustraire au crédit
                }
                else
                {
                    throw new ArgumentException("TypeOperation doit être 'Debit' ou 'Credit'");
                }

                // Mettre à jour la dépense de l'étape
                using (var updateEtapeCmd = new SqlCommand(@"
                    UPDATE EtapesProjets 
                    SET Depense = @Depense
                    WHERE Id = @Id", conn, transaction))
                {
                    updateEtapeCmd.Parameters.AddWithValue("@Id", etapeId);
                    updateEtapeCmd.Parameters.AddWithValue("@Depense", nouvelleDepense);
                    await updateEtapeCmd.ExecuteNonQueryAsync();
                }

                // Mettre à jour DepenseGlobale du projet (somme des dépenses de toutes les étapes)
                using (var updateProjetCmd = new SqlCommand(@"
                    UPDATE Projets 
                    SET DepenseGlobale = (
                        SELECT ISNULL(SUM(Depense), 0) 
                        FROM EtapesProjets 
                        WHERE ProjetId = @ProjetId
                    ),
                    DateModification = @DateModification
                    WHERE Id = @ProjetId", conn, transaction))
                {
                    updateProjetCmd.Parameters.AddWithValue("@ProjetId", projetId);
                    updateProjetCmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
                    await updateProjetCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Récupère le montant total des dépenses d'une étape
        /// </summary>
        public async Task<decimal> GetEtapeDepenseAsync(int etapeId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT Depense FROM EtapesProjets WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", etapeId);
            await conn.OpenAsync();

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? (decimal)result : 0;
        }

        /// <summary>
        /// Récupère le total des dépenses d'un projet (somme des dépenses de toutes ses étapes)
        /// </summary>
        public async Task<decimal> GetProjetDepenseTotaleAsync(int projetId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT ISNULL(SUM(Depense), 0) 
                FROM EtapesProjets 
                WHERE ProjetId = @ProjetId", conn);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);
            await conn.OpenAsync();

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? (decimal)result : 0;
        }

        // ========================================
        // MÉTHODES PRIVÉES (mises à jour)
        // ========================================

        private async Task<string> GenerateNumeroWithTransactionAsync(SqlConnection conn, SqlTransaction transaction)
        {
            var annee = DateTime.UtcNow.Year.ToString();
            using var cmd = new SqlCommand($@"
                SELECT ISNULL(MAX(CAST(RIGHT(Numero, 4) AS INT)), 0) + 1
                FROM Projets 
                WHERE Numero LIKE 'PRJ{annee}%'", conn, transaction);

            var prochainNumero = (int)await cmd.ExecuteScalarAsync();
            return $"PRJ{annee}{prochainNumero:0000}";
        }

        private async Task CreateEtapesAsync(SqlConnection conn, SqlTransaction transaction, int projetId, List<EtapeProjet> etapes)
        {
            for (int i = 0; i < etapes.Count; i++)
            {
                // --- Insertion étape (inchangé) ---
                using var cmd = new SqlCommand(@"
            INSERT INTO EtapesProjets (ProjetId, Nom, Description, Ordre, DateDebut, DateFinPrevue, Statut,
                                     PourcentageAvancement, BudgetPrevu, CoutReel, ResponsableId, TypeResponsable,
                                     LinkedDqeLotId, LinkedDqeLotCode, LinkedDqeLotName, LinkedDqeReference, IdSousTraitant)
            VALUES (@ProjetId, @Nom, @Description, @Ordre, @DateDebut, @DateFinPrevue, @Statut,
                   @PourcentageAvancement, @BudgetPrevu, @CoutReel, @ResponsableId, @TypeResponsable,
                   @LinkedDqeLotId, @LinkedDqeLotCode, @LinkedDqeLotName, @LinkedDqeReference, @IdSousTraitant);
            SELECT CAST(SCOPE_IDENTITY() AS INT)", conn, transaction);

                cmd.Parameters.AddWithValue("@ProjetId", projetId);
                cmd.Parameters.AddWithValue("@Nom", etapes[i].Nom);
                cmd.Parameters.AddWithValue("@Description", etapes[i].Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Ordre", i + 1);
                cmd.Parameters.AddWithValue("@DateDebut", etapes[i].DateDebut ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateFinPrevue", etapes[i].DateFinPrevue ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Statut", etapes[i].Statut);
                cmd.Parameters.AddWithValue("@PourcentageAvancement", etapes[i].PourcentageAvancement);
                cmd.Parameters.AddWithValue("@BudgetPrevu", etapes[i].BudgetPrevu);
                cmd.Parameters.AddWithValue("@CoutReel", etapes[i].CoutReel);
                cmd.Parameters.AddWithValue("@ResponsableId", etapes[i].ResponsableId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@TypeResponsable", etapes[i].TypeResponsable);
                cmd.Parameters.AddWithValue("@IdSousTraitant", etapes[i].IdSousTraitant ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedDqeLotId", etapes[i].LinkedDqeLotId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedDqeLotCode", etapes[i].LinkedDqeLotCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedDqeLotName", etapes[i].LinkedDqeLotName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedDqeReference", etapes[i].LinkedDqeReference ?? (object)DBNull.Value);

                // Récupérer l'Id généré (nécessaire pour insérer les sous-traitants)
                var newEtapeId = (int)await cmd.ExecuteScalarAsync();

                // 🆕 Insérer les sous-traitants associés
                if (etapes[i].SousTraitants != null && etapes[i].SousTraitants!.Any())
                {
                    await InsertEtapeSousTraitantsAsync(conn, transaction, newEtapeId, etapes[i].SousTraitants!);
                }
            }
        }
        private async Task<List<EtapeProjet>> GetEtapesProjetAsync(SqlConnection conn, int projetId)
        {
            var etapes = new List<EtapeProjet>();

            // 1️⃣ Charger les étapes
            using var cmd = new SqlCommand(@"
        SELECT ep.*, 
               u.Prenom  AS ResponsablePrenom, 
               u.Nom     AS ResponsableNom,
               st.Id     AS SousTraitantId,
               st.Nom    AS SousTraitantNom,
               st.Email  AS SousTraitantEmail,
               st.Telephone AS SousTraitantTelephone,
               st.NoteMoyenne AS SousTraitantNote
        FROM EtapesProjets ep
        LEFT JOIN Utilisateurs u  ON ep.ResponsableId  = u.Id  AND ep.TypeResponsable = 'Interne'
        LEFT JOIN SousTraitants st ON ep.IdSousTraitant = st.Id
        WHERE ep.ProjetId = @ProjetId AND ep.EstActif = 1
        ORDER BY ep.Ordre, ep.Id", conn);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var etape = new EtapeProjet
                    {
                        Id = reader.GetInt32("Id"),
                        ProjetId = reader.GetInt32("ProjetId"),
                        Nom = reader.GetString("Nom"),
                        Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                        Ordre = reader.GetInt32("Ordre"),
                        Niveau = reader.IsDBNull("Niveau") ? 0 : reader.GetInt32("Niveau"),
                        TypeEtape = reader.IsDBNull("TypeEtape") ? "Lot" : reader.GetString("TypeEtape"),
                        EtapeParentId = reader.IsDBNull("EtapeParentId") ? null : reader.GetInt32("EtapeParentId"),
                        DateDebut = reader.IsDBNull("DateDebut") ? null : reader.GetDateTime("DateDebut"),
                        DateFinPrevue = reader.IsDBNull("DateFinPrevue") ? null : reader.GetDateTime("DateFinPrevue"),
                        DateFinReelle = reader.IsDBNull("DateFinReelle") ? null : reader.GetDateTime("DateFinReelle"),
                        Statut = reader.GetString("Statut"),
                        PourcentageAvancement = reader.GetInt32("PourcentageAvancement"),
                        BudgetPrevu = reader.GetDecimal("BudgetPrevu"),
                        CoutReel = reader.GetDecimal("CoutReel"),
                        Depense = reader.IsDBNull("Depense") ? 0 : reader.GetDecimal("Depense"),
                        ResponsableId = reader.IsDBNull("ResponsableId") ? null : reader.GetInt32("ResponsableId"),
                        TypeResponsable = reader.GetString("TypeResponsable"),
                        EstActif = reader.GetBoolean("EstActif"),
                        IdSousTraitant = reader.IsDBNull("IdSousTraitant") ? null : reader.GetInt32("IdSousTraitant"),
                        LinkedDqeLotId = reader.IsDBNull("LinkedDqeLotId") ? null : reader.GetInt32("LinkedDqeLotId"),
                        LinkedDqeLotCode = reader.IsDBNull("LinkedDqeLotCode") ? null : reader.GetString("LinkedDqeLotCode"),
                        LinkedDqeLotName = reader.IsDBNull("LinkedDqeLotName") ? null : reader.GetString("LinkedDqeLotName"),
                        LinkedDqeReference = reader.IsDBNull("LinkedDqeReference") ? null : reader.GetString("LinkedDqeReference"),
                        // Initialiser la liste (sera remplie après)
                        SousTraitants = new List<EtapeSousTraitant>()
                    };

                    // Navigation legacy (IdSousTraitant)
                    if (!reader.IsDBNull("SousTraitantId"))
                    {
                        etape.SousTraitant = new SousTraitant
                        {
                            Id = reader.GetInt32("SousTraitantId"),
                            Nom = reader.GetString("SousTraitantNom"),
                            Email = reader.IsDBNull("SousTraitantEmail") ? null : reader.GetString("SousTraitantEmail"),
                            Telephone = reader.IsDBNull("SousTraitantTelephone") ? null : reader.GetString("SousTraitantTelephone"),
                            NoteMoyenne = reader.IsDBNull("SousTraitantNote") ? 0 : reader.GetDecimal("SousTraitantNote")
                        };
                    }

                    etapes.Add(etape);
                }
            }

            // 2️⃣ Charger TOUS les sous-traitants liés en une seule requête (évite N+1)
            if (etapes.Any())
            {
                var etapeIds = string.Join(",", etapes.Select(e => e.Id));
                using var stCmd = new SqlCommand($@"
            SELECT est.Id, est.EtapeProjetId, est.SousTraitantId,
                   est.Role, est.Montant, est.DateDebut, est.DateFinPrevue,
                   est.Statut, est.Notes,
                   st.Nom    AS StNom,
                   st.Email  AS StEmail,
                   st.Telephone AS StTelephone,
                   st.NoteMoyenne AS StNote
            FROM EtapesSousTraitants est
            INNER JOIN SousTraitants st ON est.SousTraitantId = st.Id
            WHERE est.EtapeProjetId IN ({etapeIds})
            ORDER BY est.EtapeProjetId, est.Id", conn);

                var etapeDict = etapes.ToDictionary(e => e.Id);

                using var stReader = await stCmd.ExecuteReaderAsync();
                while (await stReader.ReadAsync())
                {
                    var etapeId = stReader.GetInt32("EtapeProjetId");
                    if (!etapeDict.TryGetValue(etapeId, out var etape)) continue;

                    etape.SousTraitants!.Add(new EtapeSousTraitant
                    {
                        Id = stReader.GetInt32("Id"),
                        EtapeProjetId = etapeId,
                        SousTraitantId = stReader.GetInt32("SousTraitantId"),
                        Role = stReader.IsDBNull("Role") ? null : stReader.GetString("Role"),
                        Montant = stReader.IsDBNull("Montant") ? null : stReader.GetDecimal("Montant"),
                        DateDebut = stReader.IsDBNull("DateDebut") ? null : stReader.GetDateTime("DateDebut"),
                        DateFinPrevue = stReader.IsDBNull("DateFinPrevue") ? null : stReader.GetDateTime("DateFinPrevue"),
                        Statut = stReader.GetString("Statut"),
                        Notes = stReader.IsDBNull("Notes") ? null : stReader.GetString("Notes"),
                        SousTraitant = new SousTraitant
                        {
                            Id = stReader.GetInt32("SousTraitantId"),
                            Nom = stReader.GetString("StNom"),
                            Email = stReader.IsDBNull("StEmail") ? null : stReader.GetString("StEmail"),
                            Telephone = stReader.IsDBNull("StTelephone") ? null : stReader.GetString("StTelephone"),
                            NoteMoyenne = stReader.IsDBNull("StNote") ? 0 : stReader.GetDecimal("StNote")
                        }
                    });
                }
            }

            // 3️⃣ Recalculer les dépenses depuis MouvementsFinanciers
            foreach (var etape in etapes)
            {
                etape.Depense = await GetTotalSortiesByEtapeAsync(conn, etape.Id);
            }

            return etapes;
        }
        public async Task<decimal> GetTotalSortiesByEtapeAsync(SqlConnection conn, int idEtape)
        {

            var query = @"
        SELECT SUM(Montant)
        FROM MouvementsFinanciers
        WHERE Actif = 1 AND EtapeProjetId = @IdEtape
          AND TypeMouvement = 'Sortie'";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@IdEtape", idEtape);

            var result = await cmd.ExecuteScalarAsync();

            return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
        }


        private void AddProjetParameters(SqlCommand cmd, Projet projet)
        {
            cmd.Parameters.AddWithValue("@Numero", projet.Numero);
            cmd.Parameters.AddWithValue("@Nom", projet.Nom);
            cmd.Parameters.AddWithValue("@Description", projet.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ClientId", projet.ClientId);
            cmd.Parameters.AddWithValue("@DevisId", projet.DevisId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Statut", projet.Statut);
            cmd.Parameters.AddWithValue("@DateDebut", projet.DateDebut ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateFinPrevue", projet.DateFinPrevue ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateFinRelle", projet.@DateFinRelle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BudgetInitial", projet.BudgetInitial);
            cmd.Parameters.AddWithValue("@BudgetRevise", projet.BudgetRevise);
            cmd.Parameters.AddWithValue("@CoutReel", projet.CoutReel);
            cmd.Parameters.AddWithValue("@DepenseGlobale", projet.DepenseGlobale);
            cmd.Parameters.AddWithValue("@AdresseChantier", projet.AdresseChantier ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CodePostalChantier", projet.CodePostalChantier ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@VilleChantier", projet.VilleChantier ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PourcentageAvancement", projet.PourcentageAvancement);
            cmd.Parameters.AddWithValue("@ChefProjetId", projet.ChefProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateCreation", projet.DateCreation);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", projet.UtilisateurCreation);
            cmd.Parameters.AddWithValue("@Actif", projet.Actif);
            cmd.Parameters.AddWithValue("@CompteId", projet.CompteId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DepotId", projet.DepotId ?? (object)DBNull.Value); 
            // NOUVEAUX PARAMÈTRES DQE
            cmd.Parameters.AddWithValue("@LinkedDqeId", projet.LinkedDqeId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LinkedDqeReference", projet.LinkedDqeReference ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LinkedDqeName", projet.LinkedDqeName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LinkedDqeBudgetHT", projet.LinkedDqeBudgetHT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IsFromDqeConversion", projet.IsFromDqeConversion);
            cmd.Parameters.AddWithValue("@DqeConvertedAt", projet.DqeConvertedAt ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DqeConvertedById", projet.DqeConvertedById ?? (object)DBNull.Value);
        }

        private Projet MapToProjet(SqlDataReader reader)
        {
            return new Projet
            {
                Id = reader.GetInt32("Id"),
                Numero = reader.GetString("Numero"),
                Nom = reader.GetString("Nom"),
                Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                ClientId = reader.GetInt32("ClientId"),
                DevisId = reader.IsDBNull("DevisId") ? null : reader.GetInt32("DevisId"),
                Statut = reader.GetString("Statut"),
                DateDebut = reader.IsDBNull("DateDebut") ? null : reader.GetDateTime("DateDebut"),
                DateFinPrevue = reader.IsDBNull("DateFinPrevue") ? null : reader.GetDateTime("DateFinPrevue"),
                DateFinRelle = reader.IsDBNull("DateFinRelle") ? null : reader.GetDateTime("DateFinRelle"),
                BudgetInitial = reader.GetDecimal("BudgetInitial"),
                BudgetRevise = reader.GetDecimal("BudgetRevise"),
                CoutReel = reader.GetDecimal("CoutReel"),
                DepenseGlobale = reader.IsDBNull("DepenseGlobale") ? 0 : reader.GetDecimal("DepenseGlobale"),
                AdresseChantier = reader.IsDBNull("AdresseChantier") ? null : reader.GetString("AdresseChantier"),
                CodePostalChantier = reader.IsDBNull("CodePostalChantier") ? null : reader.GetString("CodePostalChantier"),
                VilleChantier = reader.IsDBNull("VilleChantier") ? null : reader.GetString("VilleChantier"),
                PourcentageAvancement = reader.GetInt32("PourcentageAvancement"),
                ChefProjetId = reader.IsDBNull("ChefProjetId") ? null : reader.GetInt32("ChefProjetId"),
                CompteId = reader.IsDBNull("CompteId") ? null : reader.GetInt32("CompteId"),
                DepotId = reader.IsDBNull("DepotId") ? null : reader.GetInt32("DepotId"),
                DateCreation = reader.GetDateTime("DateCreation"),
                DateModification = reader.GetDateTime("DateModification"),
                UtilisateurCreation = reader.GetInt32("UtilisateurCreation"),
                Actif = reader.GetBoolean("Actif"),

                // NOUVELLES PROPRIÉTÉS DQE
                LinkedDqeId = reader.IsDBNull("LinkedDqeId") ? null : reader.GetInt32("LinkedDqeId"),
                LinkedDqeReference = reader.IsDBNull("LinkedDqeReference") ? null : reader.GetString("LinkedDqeReference"),
                LinkedDqeName = reader.IsDBNull("LinkedDqeName") ? null : reader.GetString("LinkedDqeName"),
                LinkedDqeBudgetHT = reader.IsDBNull("LinkedDqeBudgetHT") ? null : reader.GetDecimal("LinkedDqeBudgetHT"),
                IsFromDqeConversion = reader.GetBoolean("IsFromDqeConversion"),
                DqeConvertedAt = reader.IsDBNull("DqeConvertedAt") ? null : reader.GetDateTime("DqeConvertedAt"),
                DqeConvertedById = reader.IsDBNull("DqeConvertedById") ? null : reader.GetInt32("DqeConvertedById"),

                Client = new Client
                {
                    Id = reader.GetInt32("ClientId"),
                    Nom = reader.IsDBNull("ClientNom") ? "" : reader.GetString("ClientNom"),
                    RaisonSociale = reader.IsDBNull("ClientRaisonSociale") ? null : reader.GetString("ClientRaisonSociale"),
                    Email = reader.IsDBNull("ClientEmail") ? null : reader.GetString("ClientEmail"),
                    Telephone = reader.IsDBNull("ClientTelephone") ? null : reader.GetString("ClientTelephone"),
                    Adresse = reader.IsDBNull("ClientAdresse") ? null : reader.GetString("ClientAdresse"),
                },
                //TypeProjet = new TypeProjet
                //{
                //    //Id = reader.GetInt32("TypeProjetId"),
                //    Nom = reader.IsDBNull("TypeProjetNom") ? "" : reader.GetString("TypeProjetNom"),
                //    Description = reader.IsDBNull("TypeProjetDescription") ? null : reader.GetString("TypeProjetDescription"),
                //    Couleur = reader.IsDBNull("TypeProjetCouleur") ? "#2563eb" : reader.GetString("TypeProjetCouleur")
                //},
                ChefProjet = reader.IsDBNull("ChefProjetPrenom") ? null : new Utilisateur
                {
                    Id = reader.GetInt32("ChefProjetId"),
                    Prenom = reader.GetString("ChefProjetPrenom"),
                    Nom = reader.GetString("ChefProjetNom")
                },
                DqeConvertedBy = reader.IsDBNull("DqeConvertedByPrenom") ? null : new Utilisateur
                {
                    Id = reader.GetInt32("DqeConvertedById"),
                    Prenom = reader.GetString("DqeConvertedByPrenom"),
                    Nom = reader.GetString("DqeConvertedByNom")
                }
            };
        }

        private Projet MapToProjetGetProjet(SqlDataReader reader)
        {
            return new Projet
            {
                Id = reader.GetInt32("Id"),
                Numero = reader.GetString("Numero"),
                Nom = reader.GetString("Nom"),
                Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                ClientId = reader.GetInt32("ClientId"),
                DevisId = reader.IsDBNull("DevisId") ? null : reader.GetInt32("DevisId"),
                Statut = reader.GetString("Statut"),
                DateDebut = reader.IsDBNull("DateDebut") ? null : reader.GetDateTime("DateDebut"),
                DateFinPrevue = reader.IsDBNull("DateFinPrevue") ? null : reader.GetDateTime("DateFinPrevue"),
                DateFinRelle = reader.IsDBNull("DateFinRelle") ? null : reader.GetDateTime("DateFinRelle"),
                BudgetInitial = reader.GetDecimal("BudgetInitial"),
                BudgetRevise = reader.GetDecimal("BudgetRevise"),
                CoutReel = reader.GetDecimal("CoutReel"),
                DepenseGlobale = reader.IsDBNull("DepenseGlobale") ? 0 : reader.GetDecimal("DepenseGlobale"),
                AdresseChantier = reader.IsDBNull("AdresseChantier") ? null : reader.GetString("AdresseChantier"),
                CodePostalChantier = reader.IsDBNull("CodePostalChantier") ? null : reader.GetString("CodePostalChantier"),
                VilleChantier = reader.IsDBNull("VilleChantier") ? null : reader.GetString("VilleChantier"),
                PourcentageAvancement = reader.GetInt32("PourcentageAvancement"),
                ChefProjetId = reader.IsDBNull("ChefProjetId") ? null : reader.GetInt32("ChefProjetId"),
                DateCreation = reader.GetDateTime("DateCreation"),
                DateModification = reader.GetDateTime("DateModification"),
                UtilisateurCreation = reader.GetInt32("UtilisateurCreation"),
                Actif = reader.GetBoolean("Actif"),

                // NOUVELLES PROPRIÉTÉS DQE
                LinkedDqeId = reader.IsDBNull("LinkedDqeId") ? null : reader.GetInt32("LinkedDqeId"),
                LinkedDqeReference = reader.IsDBNull("LinkedDqeReference") ? null : reader.GetString("LinkedDqeReference"),
                LinkedDqeName = reader.IsDBNull("LinkedDqeName") ? null : reader.GetString("LinkedDqeName"),
                LinkedDqeBudgetHT = reader.IsDBNull("LinkedDqeBudgetHT") ? null : reader.GetDecimal("LinkedDqeBudgetHT"),
                IsFromDqeConversion = reader.GetBoolean("IsFromDqeConversion"),
                DqeConvertedAt = reader.IsDBNull("DqeConvertedAt") ? null : reader.GetDateTime("DqeConvertedAt"),
                DqeConvertedById = reader.IsDBNull("DqeConvertedById") ? null : reader.GetInt32("DqeConvertedById"),

                Client = new Client
                {
                    Nom = reader.IsDBNull("NomClient") ? "" : reader.GetString("NomClient"),
                },
            };
        }

        public async Task<List<Projet>> GetAvailableProjectsForLinkingAsync()
        {
            var projets = new List<Projet>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
        SELECT 
            p.*,
            tp.Nom as TypeProjetNom,c.Nom as NomClient
        FROM Projets p
        LEFT JOIN TypesProjets tp ON p.TypeProjetId = tp.Id
        LEFT JOIN Clients c ON p.ClientId = c.Id
        WHERE p.Actif = 1
        AND p.Statut NOT IN ('Terminé', 'Clôturé')
        AND p.LinkedDqeId IS NULL
        ORDER BY p.DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                projets.Add(MapToProjetGetProjet(reader));
            }

            return projets;
        }

        /// <summary>
        /// Ajoute les étapes d'un DQE à un projet existant
        /// </summary>
        public async Task<bool> AddDQEStagesToExistingProjectAsync(
         int projetId,
         List<EtapeProjet> nouvellesEtapes,
         decimal budgetDQE)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Récupérer le nombre d'étapes existantes pour ajuster l'ordre
                int ordreMax = 0;
                using (var cmd = new SqlCommand(@"
                SELECT ISNULL(MAX(Ordre), 0) 
                FROM EtapesProjets 
                WHERE ProjetId = @ProjetId", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@ProjetId", projetId);
                    ordreMax = (int)await cmd.ExecuteScalarAsync();
                }

                // 2. Insérer les nouvelles étapes avec ordre ajusté
                foreach (var etape in nouvellesEtapes)
                {
                    ordreMax++;
                    etape.Ordre = ordreMax;

                    using var cmd = new SqlCommand(@"
                    INSERT INTO EtapesProjets (
                        ProjetId, Nom, Description, Ordre,
                        EtapeParentId, Niveau, TypeEtape,
                        DateDebut, DateFinPrevue,
                        Statut, PourcentageAvancement,
                        BudgetPrevu, CoutReel, Depense,
                        Unite, QuantitePrevue, PrixUnitairePrevu,
                        ResponsableId, TypeResponsable, IdSousTraitant,
                        LinkedDqeLotId, LinkedDqeLotCode, LinkedDqeLotName,
                        LinkedDqeItemId, LinkedDqeItemCode,
                        LinkedDqeChapterId, LinkedDqeChapterCode,
                        LinkedDqeReference,
                        EstActif, DateCreation, DateModification
                    ) VALUES (
                        @ProjetId, @Nom, @Description, @Ordre,
                        @EtapeParentId, @Niveau, @TypeEtape,
                        @DateDebut, @DateFinPrevue,
                        @Statut, @PourcentageAvancement,
                        @BudgetPrevu, @CoutReel, @Depense,
                        @Unite, @QuantitePrevue, @PrixUnitairePrevu,
                        @ResponsableId, @TypeResponsable, @IdSousTraitant,
                        @LinkedDqeLotId, @LinkedDqeLotCode, @LinkedDqeLotName,
                        @LinkedDqeItemId, @LinkedDqeItemCode,
                        @LinkedDqeChapterId, @LinkedDqeChapterCode,
                        @LinkedDqeReference,
                        @EstActif, @DateCreation, @DateModification
                    )", conn, transaction);

                    // Paramètres obligatoires
                    cmd.Parameters.AddWithValue("@ProjetId", projetId);
                    cmd.Parameters.AddWithValue("@Nom", etape.Nom);
                    cmd.Parameters.AddWithValue("@Description", etape.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Ordre", etape.Ordre);

                    // Hiérarchie
                    cmd.Parameters.AddWithValue("@EtapeParentId", etape.EtapeParentId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Niveau", etape.Niveau);
                    cmd.Parameters.AddWithValue("@TypeEtape", etape.TypeEtape ?? "Lot");

                    // Dates
                    cmd.Parameters.AddWithValue("@DateDebut", etape.DateDebut ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateFinPrevue", etape.DateFinPrevue ?? (object)DBNull.Value);

                    // Statut
                    cmd.Parameters.AddWithValue("@Statut", etape.Statut ?? "NonCommence");
                    cmd.Parameters.AddWithValue("@PourcentageAvancement", etape.PourcentageAvancement);

                    // Budget
                    cmd.Parameters.AddWithValue("@BudgetPrevu", etape.BudgetPrevu);
                    cmd.Parameters.AddWithValue("@CoutReel", etape.CoutReel);
                    cmd.Parameters.AddWithValue("@Depense", etape.Depense);

                    // Quantités (pour sous-étapes)
                    cmd.Parameters.AddWithValue("@Unite", etape.Unite ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@QuantitePrevue", etape.QuantitePrevue ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrixUnitairePrevu", etape.PrixUnitairePrevu ?? (object)DBNull.Value);

                    // Responsable
                    cmd.Parameters.AddWithValue("@ResponsableId", etape.ResponsableId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TypeResponsable", etape.TypeResponsable ?? "Interne");
                    cmd.Parameters.AddWithValue("@IdSousTraitant", etape.IdSousTraitant ?? (object)DBNull.Value);

                    // Traçabilité DQE - Lot
                    cmd.Parameters.AddWithValue("@LinkedDqeLotId", etape.LinkedDqeLotId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LinkedDqeLotCode", etape.LinkedDqeLotCode ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LinkedDqeLotName", etape.LinkedDqeLotName ?? (object)DBNull.Value);

                    // Traçabilité DQE - Item
                    cmd.Parameters.AddWithValue("@LinkedDqeItemId", etape.LinkedDqeItemId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LinkedDqeItemCode", etape.LinkedDqeItemCode ?? (object)DBNull.Value);

                    // Traçabilité DQE - Chapter
                    cmd.Parameters.AddWithValue("@LinkedDqeChapterId", etape.LinkedDqeChapterId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LinkedDqeChapterCode", etape.LinkedDqeChapterCode ?? (object)DBNull.Value);

                    // Référence commune
                    cmd.Parameters.AddWithValue("@LinkedDqeReference", etape.LinkedDqeReference ?? (object)DBNull.Value);

                    // Métadonnées
                    cmd.Parameters.AddWithValue("@EstActif", etape.EstActif);
                    cmd.Parameters.AddWithValue("@DateCreation", DateTime.UtcNow);
                    cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

                    await cmd.ExecuteNonQueryAsync();
                }

                // 3. Mettre à jour le budget du projet
                using (var cmd = new SqlCommand(@"
                UPDATE Projets 
                SET BudgetRevise = BudgetRevise + @BudgetDQE,
                    DateModification = @DateModification
                WHERE Id = @ProjetId", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@ProjetId", projetId);
                    cmd.Parameters.AddWithValue("@BudgetDQE", budgetDQE);
                    cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception($"Erreur lors de l'ajout des étapes DQE au projet: {ex.Message}", ex);
            }
        }
        // ============================================================
        // GESTION DES SOUS-TRAITANTS D'UNE ÉTAPE
        // ============================================================

        /// <summary>
        /// Récupère les sous-traitants affectés à une étape
        /// </summary>
        public async Task<List<EtapeSousTraitant>> GetSousTraitantsByEtapeAsync(int etapeId)
        {
            var liste = new List<EtapeSousTraitant>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
        SELECT est.*, 
               st.Nom AS StNom, st.Email AS StEmail,
               st.Telephone AS StTelephone, st.NoteMoyenne AS StNote
        FROM EtapesSousTraitants est
        INNER JOIN SousTraitants st ON est.SousTraitantId = st.Id
        WHERE est.EtapeProjetId = @EtapeId
        ORDER BY est.Id", conn);

            cmd.Parameters.AddWithValue("@EtapeId", etapeId);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                liste.Add(MapToEtapeSousTraitant(reader));
            }

            return liste;
        }

        /// <summary>
        /// Assigne un sous-traitant à une étape (ou met à jour si déjà assigné)
        /// </summary>
        public async Task<EtapeSousTraitant> AssignSousTraitantToEtapeAsync(int etapeId, AssignSousTraitantEtapeRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // MERGE : insert si absent, update si déjà présent
            using var cmd = new SqlCommand(@"
        MERGE EtapesSousTraitants AS target
        USING (SELECT @EtapeProjetId AS EtapeProjetId, @SousTraitantId AS SousTraitantId) AS source
            ON target.EtapeProjetId = source.EtapeProjetId 
           AND target.SousTraitantId = source.SousTraitantId
        WHEN MATCHED THEN
            UPDATE SET Role = @Role, Montant = @Montant, DateDebut = @DateDebut,
                       DateFinPrevue = @DateFinPrevue, Statut = @Statut, Notes = @Notes,
                       DateModification = GETUTCDATE()
        WHEN NOT MATCHED THEN
            INSERT (EtapeProjetId, SousTraitantId, Role, Montant, DateDebut, DateFinPrevue, Statut, Notes)
            VALUES (@EtapeProjetId, @SousTraitantId, @Role, @Montant, @DateDebut, @DateFinPrevue, @Statut, @Notes)
        OUTPUT inserted.Id;", conn);

            cmd.Parameters.AddWithValue("@EtapeProjetId", etapeId);
            cmd.Parameters.AddWithValue("@SousTraitantId", request.SousTraitantId);
            cmd.Parameters.AddWithValue("@Role", request.Role ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Montant", request.Montant ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateDebut", request.DateDebut ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateFinPrevue", request.DateFinPrevue ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Statut", request.Statut);
            cmd.Parameters.AddWithValue("@Notes", request.Notes ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            // Retourner l'enregistrement complet
            var result = await GetSousTraitantsByEtapeAsync(etapeId);
            return result.First(x => x.SousTraitantId == request.SousTraitantId);
        }

        /// <summary>
        /// Retire un sous-traitant d'une étape
        /// </summary>
        public async Task<bool> RemoveSousTraitantFromEtapeAsync(int etapeId, int sousTraitantId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
        DELETE FROM EtapesSousTraitants
        WHERE EtapeProjetId = @EtapeId AND SousTraitantId = @SousTraitantId", conn);

            cmd.Parameters.AddWithValue("@EtapeId", etapeId);
            cmd.Parameters.AddWithValue("@SousTraitantId", sousTraitantId);

            await conn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        /// <summary>
        /// Remplace en bloc tous les sous-traitants d'une étape
        /// </summary>
        public async Task UpdateEtapeSousTraitantsAsync(int etapeId, UpdateEtapeSousTraitantsRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Supprimer tous les liens existants
                using (var deleteCmd = new SqlCommand(
                    "DELETE FROM EtapesSousTraitants WHERE EtapeProjetId = @EtapeId", conn, transaction))
                {
                    deleteCmd.Parameters.AddWithValue("@EtapeId", etapeId);
                    await deleteCmd.ExecuteNonQueryAsync();
                }

                // Ré-insérer la nouvelle liste
                await InsertEtapeSousTraitantsAsync(conn, transaction, etapeId,
                    request.SousTraitants.Select(r => new EtapeSousTraitant
                    {
                        SousTraitantId = r.SousTraitantId,
                        Role = r.Role,
                        Montant = r.Montant,
                        DateDebut = r.DateDebut,
                        DateFinPrevue = r.DateFinPrevue,
                        Statut = r.Statut,
                        Notes = r.Notes
                    }).ToList());

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ---- Méthodes privées helpers ----

        private async Task InsertEtapeSousTraitantsAsync(
            SqlConnection conn, SqlTransaction transaction,
            int etapeId, List<EtapeSousTraitant> sousTraitants)
        {
            foreach (var st in sousTraitants)
            {
                using var cmd = new SqlCommand(@"
            INSERT INTO EtapesSousTraitants 
                (EtapeProjetId, SousTraitantId, Role, Montant, DateDebut, DateFinPrevue, Statut, Notes)
            VALUES 
                (@EtapeId, @SousTraitantId, @Role, @Montant, @DateDebut, @DateFinPrevue, @Statut, @Notes)",
                    conn, transaction);

                cmd.Parameters.AddWithValue("@EtapeId", etapeId);
                cmd.Parameters.AddWithValue("@SousTraitantId", st.SousTraitantId);
                cmd.Parameters.AddWithValue("@Role", st.Role ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Montant", st.Montant ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateDebut", st.DateDebut ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateFinPrevue", st.DateFinPrevue ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Statut", st.Statut);
                cmd.Parameters.AddWithValue("@Notes", st.Notes ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private EtapeSousTraitant MapToEtapeSousTraitant(SqlDataReader reader)
        {
            return new EtapeSousTraitant
            {
                Id = reader.GetInt32("Id"),
                EtapeProjetId = reader.GetInt32("EtapeProjetId"),
                SousTraitantId = reader.GetInt32("SousTraitantId"),
                Role = reader.IsDBNull("Role") ? null : reader.GetString("Role"),
                Montant = reader.IsDBNull("Montant") ? null : reader.GetDecimal("Montant"),
                DateDebut = reader.IsDBNull("DateDebut") ? null : reader.GetDateTime("DateDebut"),
                DateFinPrevue = reader.IsDBNull("DateFinPrevue") ? null : reader.GetDateTime("DateFinPrevue"),
                Statut = reader.GetString("Statut"),
                Notes = reader.IsDBNull("Notes") ? null : reader.GetString("Notes"),
                DateCreation = reader.GetDateTime("DateCreation"),
                DateModification = reader.GetDateTime("DateModification"),
                SousTraitant = new SousTraitant
                {
                    Id = reader.GetInt32("SousTraitantId"),
                    Nom = reader.GetString("StNom"),
                    Email = reader.IsDBNull("StEmail") ? null : reader.GetString("StEmail"),
                    Telephone = reader.IsDBNull("StTelephone") ? null : reader.GetString("StTelephone"),
                    NoteMoyenne = reader.IsDBNull("StNote") ? 0 : reader.GetDecimal("StNote")
                }
            };
        }

        /// <summary>
        /// Charge la liste des chefs d'un projet depuis la table de jonction.
        /// </summary>
        private async Task<List<ResponsableDTO>> LoadChefsProjetAsync(SqlConnection conn, int projetId)
        {
            var chefs = new List<ResponsableDTO>();
            using var cmd = new SqlCommand(@"
        SELECT u.Id, u.Prenom, u.Nom, pcp.EstChefPrincipal
        FROM ProjetsChefsProjet pcp
        INNER JOIN Utilisateurs u ON pcp.UtilisateurId = u.Id
        WHERE pcp.ProjetId = @ProjetId
        ORDER BY pcp.EstChefPrincipal DESC, u.Nom", conn);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                chefs.Add(new ResponsableDTO
                {
                    Id = reader.GetInt32("Id"),
                    Prenom = reader.GetString("Prenom"),
                    Nom = reader.GetString("Nom"),
                });
            }
            return chefs;
        }

        /// <summary>
        /// Remplace en bloc les chefs d'un projet.
        /// Le premier de la liste devient EstChefPrincipal = 1.
        /// Met également à jour ChefProjetId (legacy) avec le chef principal.
        /// </summary>
        private async Task SyncChefsProjetAsync(
            SqlConnection conn, SqlTransaction transaction,
            int projetId, List<int> chefIds)
        {
            // Supprimer les anciens liens
            using (var del = new SqlCommand(
                "DELETE FROM ProjetsChefsProjet WHERE ProjetId = @ProjetId",
                conn, transaction))
            {
                del.Parameters.AddWithValue("@ProjetId", projetId);
                await del.ExecuteNonQueryAsync();
            }

            // Insérer les nouveaux (distinct pour éviter les doublons si liste mal formée)
            var distinctIds = chefIds.Distinct().ToList();
            for (int i = 0; i < distinctIds.Count; i++)
            {
                using var ins = new SqlCommand(@"
            INSERT INTO ProjetsChefsProjet (ProjetId, UtilisateurId, EstChefPrincipal, DateCreation)
            VALUES (@ProjetId, @UtilisateurId, @EstChefPrincipal, GETUTCDATE())",
                    conn, transaction);

                ins.Parameters.AddWithValue("@ProjetId", projetId);
                ins.Parameters.AddWithValue("@UtilisateurId", distinctIds[i]);
                ins.Parameters.AddWithValue("@EstChefPrincipal", i == 0 ? 1 : 0);
                await ins.ExecuteNonQueryAsync();
            }

            // Mettre à jour ChefProjetId (rétrocompatibilité) avec le chef principal
            using var upd = new SqlCommand(@"
        UPDATE Projets SET ChefProjetId = @ChefPrincipalId
        WHERE Id = @ProjetId",
                conn, transaction);

            upd.Parameters.AddWithValue("@ChefPrincipalId", distinctIds.First());
            upd.Parameters.AddWithValue("@ProjetId", projetId);
            await upd.ExecuteNonQueryAsync();
        }
    }

}