using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.SousTraitants;
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

        public async Task<List<Projet>> GetAllAsync()
        {
            var projets = new List<Projet>();

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
               ISNULL((SELECT SUM(CoutReel) FROM EtapesProjets WHERE ProjetId = p.Id AND EstActif = 1), 0) as CoutReelCalcule
        FROM Projets p
        LEFT JOIN Clients c ON p.ClientId = c.Id
        LEFT JOIN Utilisateurs u ON p.ChefProjetId = u.Id
        LEFT JOIN Utilisateurs conv ON p.DqeConvertedById = conv.Id
        WHERE p.Actif = 1
        ORDER BY p.DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            // ✅ CORRECTION 1 : Lire tous les projets d'abord, PUIS charger les étapes
            while (await reader.ReadAsync())
            {
                var projet = MapToProjet(reader);
                projet.CoutReel = reader.GetDecimal("CoutReelCalcule");
                projets.Add(projet);
            }

            // ✅ CORRECTION 2 : Fermer le reader avant de charger les étapes
            reader.Close();

            // ✅ CORRECTION 3 : Charger les étapes pour chaque projet
            foreach (var projet in projets)
            {
                projet.Etapes = await GetEtapesProjetAsync(conn, projet.Id);

                // Calculer le pourcentage d'avancement
                if (projet.Etapes != null && projet.Etapes.Any())
                {
                    var etapesActives = projet.Etapes.Where(e => e.EstActif).ToList();

                    if (etapesActives.Any())
                    {
                        var totalAvancement = etapesActives.Sum(x => x.PourcentageAvancement);
                        var totalEtape = etapesActives.Count;
                        projet.PourcentageAvancement = Convert.ToInt32(totalAvancement / totalEtape);
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

                // Recalculer le CoutReel depuis les étapes pour garantir la cohérence
                if (projet.Etapes != null && projet.Etapes.Any())
                {
                    projet.CoutReel = projet.Etapes.Sum(e => e.CoutReel);
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

        public async Task<int> CreateAsync(Projet projet)
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
                                       ChefProjetId, DateCreation, DateModification, UtilisateurCreation, Actif,
                                       LinkedDqeId, LinkedDqeReference, LinkedDqeName, LinkedDqeBudgetHT,
                                       IsFromDqeConversion, DqeConvertedAt, DqeConvertedById)
                    VALUES (@Numero, @Nom, @Description, @ClientId, @DevisId, @Statut,
                           @DateDebut, @DateFinPrevue, @BudgetInitial, @BudgetRevise, @CoutReel, @DepenseGlobale,
                           @AdresseChantier, @CodePostalChantier, @VilleChantier, @PourcentageAvancement,
                           @ChefProjetId, @DateCreation, @DateModification, @UtilisateurCreation, @Actif,
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

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        private async Task UpdateEtapesAsync(SqlConnection conn, SqlTransaction transaction, int projetId, List<UpdateEtapeProjetRequest> etapes)
        {
            foreach (var etape in etapes)
            {
                if (etape.Id.HasValue)
                {
                    // ========================================
                    // MISE À JOUR D'UNE ÉTAPE EXISTANTE
                    // ========================================

                    var setClause = new List<string>();
                    var cmd = new SqlCommand { Connection = conn, Transaction = transaction };

                    // ✅ CORRECTION: Mettre à jour les autres champs SEULEMENT si l'étape est active

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

                    // ✅ CORRECTION: Gestion correcte du soft delete via estActif
                    if (etape.EstActif == false)
                    {
                        setClause.Add("estActif = @estActif");
                        cmd.Parameters.AddWithValue("@estActif", etape.EstActif);
                    }

                    // ✅ MISE À JOUR DE IdSousTraitant
                    if (etape.IdSousTraitant.HasValue)
                    {
                        setClause.Add("IdSousTraitant = @IdSousTraitant");
                        cmd.Parameters.AddWithValue("@IdSousTraitant", etape.IdSousTraitant.Value);

                        // Si un sous-traitant est assigné, changer TypeResponsable
                        setClause.Add("TypeResponsable = @TypeResponsable");
                        cmd.Parameters.AddWithValue("@TypeResponsable", "SousTraitant");
                    }
                    else
                    {
                        // Pas de sous-traitant, remettre à Interne
                        setClause.Add("IdSousTraitant = @IdSousTraitant");
                        cmd.Parameters.AddWithValue("@IdSousTraitant", DBNull.Value);

                        setClause.Add("TypeResponsable = @TypeResponsable");
                        cmd.Parameters.AddWithValue("@TypeResponsable", "Interne");
                    }

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
                }
                else
                {
                    // ========================================
                    // CRÉATION D'UNE NOUVELLE ÉTAPE
                    // ========================================

                    var cmd = new SqlCommand(@"
                INSERT INTO EtapesProjets (
                    ProjetId, Nom, Description, DateDebut, DateFinPrevue, 
                    BudgetPrevu, CoutReel, Statut, ResponsableId, TypeResponsable, 
                    Ordre, PourcentageAvancement, EstActif
                )
                VALUES (
                    @ProjetId, @Nom, @Description, @DateDebut, @DateFinPrevue, 
                    @BudgetPrevu, @CoutReel, @Statut, @ResponsableId, @TypeResponsable, 
                    (SELECT ISNULL(MAX(Ordre), 0) + 1 FROM EtapesProjets WHERE ProjetId = @ProjetId), 
                    0, @EstActif
                )",
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
                    cmd.Parameters.AddWithValue("@TypeResponsable", etape.IdSousTraitant.HasValue ? "SousTraitant" : "Interne");
                    cmd.Parameters.AddWithValue("@EstActif", etape.EstActif);
                    cmd.Parameters.AddWithValue("@IdSousTraitant", etape.IdSousTraitant ?? (object)DBNull.Value);

                    await cmd.ExecuteNonQueryAsync();
                }
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


            setClause.Add("PourcentageAvancement = @PourcentageAvancement");
            cmd.Parameters.AddWithValue("@PourcentageAvancement", request.PourcentageAvancement);

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
                using var cmd = new SqlCommand(@"
                    INSERT INTO EtapesProjets (ProjetId, Nom, Description, Ordre, DateDebut, DateFinPrevue, Statut,
                                             PourcentageAvancement, BudgetPrevu, CoutReel, ResponsableId, TypeResponsable,
                                             LinkedDqeLotId, LinkedDqeLotCode, LinkedDqeLotName, LinkedDqeReference,IdSousTraitant)
                    VALUES (@ProjetId, @Nom, @Description, @Ordre, @DateDebut, @DateFinPrevue, @Statut,
                           @PourcentageAvancement, @BudgetPrevu, @CoutReel, @ResponsableId, @TypeResponsable,
                           @LinkedDqeLotId, @LinkedDqeLotCode, @LinkedDqeLotName, @LinkedDqeReference,@IdSousTraitant)", conn, transaction);

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

                // NOUVEAUX PARAMÈTRES LOT DQE
                cmd.Parameters.AddWithValue("@LinkedDqeLotId", etapes[i].LinkedDqeLotId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedDqeLotCode", etapes[i].LinkedDqeLotCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedDqeLotName", etapes[i].LinkedDqeLotName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedDqeReference", etapes[i].LinkedDqeReference ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<List<EtapeProjet>> GetEtapesProjetAsync(SqlConnection conn, int projetId)
        {
            var etapes = new List<EtapeProjet>();

            using var cmd = new SqlCommand(@"
        SELECT ep.*, 
               u.Prenom as ResponsablePrenom, 
               u.Nom as ResponsableNom,
               st.Id as SousTraitantId,
               st.Nom as SousTraitantNom,
               st.Email as SousTraitantEmail,
               st.Telephone as SousTraitantTelephone,
               st.NoteMoyenne as SousTraitantNote
        FROM EtapesProjets ep
        LEFT JOIN Utilisateurs u ON ep.ResponsableId = u.Id AND ep.TypeResponsable = 'Interne'
        LEFT JOIN SousTraitants st ON ep.IdSousTraitant = st.Id
        WHERE ep.ProjetId = @ProjetId And ep.EstActif=1
        ORDER BY ep.Ordre", conn);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var etape = new EtapeProjet
                {
                    Id = reader.GetInt32("Id"),
                    ProjetId = reader.GetInt32("ProjetId"),
                    Nom = reader.GetString("Nom"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Ordre = reader.GetInt32("Ordre"),
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

                    // ✅ NOUVEAU CHAMP
                    IdSousTraitant = reader.IsDBNull("IdSousTraitant") ? null : reader.GetInt32("IdSousTraitant"),

                    // Propriétés DQE existantes
                    LinkedDqeLotId = reader.IsDBNull("LinkedDqeLotId") ? null : reader.GetInt32("LinkedDqeLotId"),
                    LinkedDqeLotCode = reader.IsDBNull("LinkedDqeLotCode") ? null : reader.GetString("LinkedDqeLotCode"),
                    LinkedDqeLotName = reader.IsDBNull("LinkedDqeLotName") ? null : reader.GetString("LinkedDqeLotName"),
                    LinkedDqeReference = reader.IsDBNull("LinkedDqeReference") ? null : reader.GetString("LinkedDqeReference")
                };

                // ✅ MAPPER LE SOUS-TRAITANT SI PRÉSENT
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

            return etapes;
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
    }
}