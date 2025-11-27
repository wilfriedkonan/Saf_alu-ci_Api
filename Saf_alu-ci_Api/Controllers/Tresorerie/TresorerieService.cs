using Microsoft.Data.SqlClient;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.Tresorerie
{
    public class TresorerieService
    {
        private readonly string _connectionString;

        public TresorerieService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // =============================================
        // GESTION DES COMPTES
        // =============================================

        public async Task<List<Compte>> GetAllComptesAsync()
        {
            var comptes = new List<Compte>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM Comptes WHERE Actif = 1 ORDER BY TypeCompte, Nom", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                comptes.Add(MapToCompte(reader));
            }

            return comptes;
        }

        public async Task<Compte?> GetCompteByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM Comptes WHERE Id = @Id AND Actif = 1", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return MapToCompte(reader);
            }

            return null;
        }

        public async Task<int> CreateCompteAsync(Compte compte)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO Comptes (Nom, TypeCompte, Numero, Banque, SoldeInitial, SoldeActuel, DateCreation, Actif)
                VALUES (@Nom, @TypeCompte, @Numero, @Banque, @SoldeInitial, @SoldeActuel, @DateCreation, @Actif);
                SELECT CAST(SCOPE_IDENTITY() as int)", conn);

            AddCompteParameters(cmd, compte);

            await conn.OpenAsync();
            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task UpdateCompteAsync(Compte compte)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Comptes SET 
                    Nom = @Nom, 
                    Numero = @Numero, 
                    Banque = @Banque
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", compte.Id);
            cmd.Parameters.AddWithValue("@Nom", compte.Nom);
            cmd.Parameters.AddWithValue("@Numero", compte.Numero ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Banque", compte.Banque ?? (object)DBNull.Value);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteCompteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("UPDATE Comptes SET Actif = 0 WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> VerifierSoldeSuffisantAsync(int compteId, decimal montant)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT SoldeActuel FROM Comptes WHERE Id = @Id AND Actif = 1", conn);
            cmd.Parameters.AddWithValue("@Id", compteId);

            await conn.OpenAsync();
            var solde = await cmd.ExecuteScalarAsync();

            return solde != null && (decimal)solde >= montant;
        }

        // =============================================
        // GESTION DES MOUVEMENTS
        // =============================================

        public async Task<List<MouvementFinancier>> GetMouvementsAsync(
            int? compteId = null,
            int nbJours = 30,
            string? typeMouvement = null,
            string? categorie = null,
            DateTime? dateDebut = null,
            DateTime? dateFin = null)
        {
            var mouvements = new List<MouvementFinancier>();

            using var conn = new SqlConnection(_connectionString);

            var sql = @"
                SELECT mf.*, c.Nom as CompteNom, cd.Nom as CompteDestinationNom
                FROM MouvementsFinanciers mf
                LEFT JOIN Comptes c ON mf.CompteId = c.Id
                LEFT JOIN Comptes cd ON mf.CompteDestinationId = cd.Id
                WHERE 1=1";

            var parameters = new List<SqlParameter>();

            // Filtrage par date
            if (dateDebut.HasValue)
            {
                sql += " AND mf.DateMouvement >= @DateDebut";
                parameters.Add(new SqlParameter("@DateDebut", dateDebut.Value));
            }
            else
            {
                sql += " AND mf.DateMouvement >= DATEADD(DAY, -@NbJours, GETDATE())";
                parameters.Add(new SqlParameter("@NbJours", nbJours));
            }

            if (dateFin.HasValue)
            {
                sql += " AND mf.DateMouvement <= @DateFin";
                parameters.Add(new SqlParameter("@DateFin", dateFin.Value));
            }

            // Filtrage par compte
            if (compteId.HasValue)
            {
                sql += " AND (mf.CompteId = @CompteId OR mf.CompteDestinationId = @CompteId)";
                parameters.Add(new SqlParameter("@CompteId", compteId.Value));
            }

            // Filtrage par type de mouvement
            if (!string.IsNullOrEmpty(typeMouvement))
            {
                sql += " AND mf.TypeMouvement = @TypeMouvement";
                parameters.Add(new SqlParameter("@TypeMouvement", typeMouvement));
            }

            // Filtrage par catégorie
            if (!string.IsNullOrEmpty(categorie))
            {
                sql += " AND mf.Categorie LIKE @Categorie";
                parameters.Add(new SqlParameter("@Categorie", $"%{categorie}%"));
            }

            sql += " ORDER BY mf.DateMouvement DESC, mf.DateSaisie DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters.ToArray());

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                mouvements.Add(MapToMouvementFinancier(reader));
            }

            return mouvements;
        }

        public async Task<MouvementFinancier?> GetMouvementByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT mf.*, c.Nom as CompteNom, cd.Nom as CompteDestinationNom
                FROM MouvementsFinanciers mf
                LEFT JOIN Comptes c ON mf.CompteId = c.Id
                LEFT JOIN Comptes cd ON mf.CompteDestinationId = cd.Id
                WHERE mf.Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return MapToMouvementFinancier(reader);
            }

            return null;
        }

        public async Task<int> CreateMouvementAsync(MouvementFinancier mouvement)
        {
            using var conn = new SqlConnection(_connectionString);

            // Vérification du solde pour les sorties et virements
            if ((mouvement.TypeMouvement == "Sortie" || mouvement.TypeMouvement == "Virement") && mouvement.Montant > 0)
            {
                var soldeSuffisant = await VerifierSoldeSuffisantAsync(mouvement.CompteId, mouvement.Montant);
                if (!soldeSuffisant)
                {
                    throw new InvalidOperationException("Solde insuffisant pour effectuer cette opération");
                }
            }

            using var cmd = new SqlCommand(@"
                INSERT INTO MouvementsFinanciers (CompteId, TypeMouvement, Categorie, FactureId, ProjetId, SousTraitantId, EtapeProjetId,
                                               Libelle, Description, Montant, DateMouvement, DateSaisie, ModePaiement,
                                               Reference, CompteDestinationId, UtilisateurCreation)
                VALUES (@CompteId, @TypeMouvement, @Categorie, @FactureId, @ProjetId, @SousTraitantId, @EtapeProjetId,
                       @Libelle, @Description, @Montant, @DateMouvement, @DateSaisie, @ModePaiement,
                       @Reference, @CompteDestinationId, @UtilisateurCreation);
                SELECT CAST(SCOPE_IDENTITY() as int)", conn);

            AddMouvementParameters(cmd, mouvement);

            await conn.OpenAsync();
            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task<bool> CreateVirementAsync(VirementRequest virement, int utilisateurId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Vérifier que les comptes existent et sont différents
                if (virement.CompteSourceId == virement.CompteDestinationId)
                {
                    throw new InvalidOperationException("Les comptes source et destination doivent être différents");
                }

                // Vérifier que le compte source a suffisamment de fonds
                using var checkCmd = new SqlCommand("SELECT SoldeActuel FROM Comptes WHERE Id = @CompteId AND Actif = 1", conn, transaction);
                checkCmd.Parameters.AddWithValue("@CompteId", virement.CompteSourceId);
                var soldeResult = await checkCmd.ExecuteScalarAsync();

                if (soldeResult == null)
                {
                    throw new InvalidOperationException("Compte source introuvable");
                }

                var soldeActuel = (decimal)soldeResult;
                if (soldeActuel < virement.Montant)
                {
                    return false; // Solde insuffisant
                }

                // Vérifier que le compte destination existe
                using var checkDestCmd = new SqlCommand("SELECT COUNT(*) FROM Comptes WHERE Id = @CompteId AND Actif = 1", conn, transaction);
                checkDestCmd.Parameters.AddWithValue("@CompteId", virement.CompteDestinationId);
                var compteDestExiste = (int)await checkDestCmd.ExecuteScalarAsync() > 0;

                if (!compteDestExiste)
                {
                    throw new InvalidOperationException("Compte destination introuvable");
                }

                // Créer le mouvement de virement
                var mouvement = new MouvementFinancier
                {
                    CompteId = virement.CompteSourceId,
                    TypeMouvement = "Virement",
                    Categorie = "Virement interne",
                    Libelle = virement.Libelle,
                    Description = virement.Description,
                    Montant = virement.Montant,
                    DateMouvement = virement.DateMouvement,
                    DateSaisie = DateTime.UtcNow,
                    Reference = virement.Reference,
                    CompteDestinationId = virement.CompteDestinationId,
                    UtilisateurCreation = utilisateurId
                };

                using var cmd = new SqlCommand(@"
                    INSERT INTO MouvementsFinanciers (CompteId, TypeMouvement, Categorie, Libelle, Description, Montant,
                                                   DateMouvement, DateSaisie, Reference, CompteDestinationId, UtilisateurCreation)
                    VALUES (@CompteId, @TypeMouvement, @Categorie, @Libelle, @Description, @Montant,
                           @DateMouvement, @DateSaisie, @Reference, @CompteDestinationId, @UtilisateurCreation)", conn, transaction);

                AddMouvementParameters(cmd, mouvement);
                await cmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task CorrigerSoldeAsync(int compteId, CorrectionSoldeRequest correction, int utilisateurId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Récupérer le solde actuel
                using var getSoldeCmd = new SqlCommand("SELECT SoldeActuel FROM Comptes WHERE Id = @Id", conn, transaction);
                getSoldeCmd.Parameters.AddWithValue("@Id", compteId);
                var soldeActuel = (decimal)await getSoldeCmd.ExecuteScalarAsync();

                var ecart = correction.NouveauSolde - soldeActuel;

                if (Math.Abs(ecart) > 0.01m) // Seulement si différence significative
                {
                    // Créer un mouvement de correction
                    var mouvement = new MouvementFinancier
                    {
                        CompteId = compteId,
                        TypeMouvement = ecart > 0 ? "Entree" : "Sortie",
                        Categorie = "Correction de solde",
                        Libelle = $"Correction de solde - {correction.MotifCorrection}",
                        Description = correction.MotifCorrection,
                        Montant = Math.Abs(ecart),
                        DateMouvement = DateTime.UtcNow,
                        DateSaisie = DateTime.UtcNow,
                        Reference = correction.Reference,
                        UtilisateurCreation = utilisateurId
                    };

                    using var cmd = new SqlCommand(@"
                        INSERT INTO MouvementsFinanciers (CompteId, TypeMouvement, Categorie, Libelle, Description, Montant,
                                                       DateMouvement, DateSaisie, Reference, UtilisateurCreation)
                        VALUES (@CompteId, @TypeMouvement, @Categorie, @Libelle, @Description, @Montant,
                               @DateMouvement, @DateSaisie, @Reference, @UtilisateurCreation)", conn, transaction);

                    AddMouvementParameters(cmd, mouvement);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =============================================
        // STATISTIQUES ET REPORTING
        // =============================================

        public async Task<TresorerieStats> GetStatsAsync()
        {
            var stats = new TresorerieStats();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Solde total
            using var soldeCmd = new SqlCommand("SELECT ISNULL(SUM(SoldeActuel), 0) FROM Comptes WHERE Actif = 1", conn);
            stats.SoldeTotal = (decimal)await soldeCmd.ExecuteScalarAsync();

            // Entrées/Sorties du mois
            var debutMois = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            using var entreesCmd = new SqlCommand(@"
                SELECT ISNULL(SUM(Montant), 0) 
                FROM MouvementsFinanciers 
                WHERE TypeMouvement = 'Entree' AND DateMouvement >= @DebutMois", conn);
            entreesCmd.Parameters.AddWithValue("@DebutMois", debutMois);
            stats.EntreesMois = (decimal)await entreesCmd.ExecuteScalarAsync();

            using var sortiesCmd = new SqlCommand(@"
                SELECT ISNULL(SUM(Montant), 0) 
                FROM MouvementsFinanciers 
                WHERE TypeMouvement = 'Sortie' AND DateMouvement >= @DebutMois", conn);
            sortiesCmd.Parameters.AddWithValue("@DebutMois", debutMois);
            stats.SortiesMois = (decimal)await sortiesCmd.ExecuteScalarAsync();

            stats.BeneficeMois = stats.EntreesMois - stats.SortiesMois;

            // Entrées/Sorties de l'année
            var debutAnnee = new DateTime(DateTime.Now.Year, 1, 1);

            using var entreesAnneeCmd = new SqlCommand(@"
                SELECT ISNULL(SUM(Montant), 0) 
                FROM MouvementsFinanciers 
                WHERE TypeMouvement = 'Entree' AND DateMouvement >= @DebutAnnee", conn);
            entreesAnneeCmd.Parameters.AddWithValue("@DebutAnnee", debutAnnee);
            stats.EntreesAnnee = (decimal)await entreesAnneeCmd.ExecuteScalarAsync();

            using var sortiesAnneeCmd = new SqlCommand(@"
                SELECT ISNULL(SUM(Montant), 0) 
                FROM MouvementsFinanciers 
                WHERE TypeMouvement = 'Sortie' AND DateMouvement >= @DebutAnnee", conn);
            sortiesAnneeCmd.Parameters.AddWithValue("@DebutAnnee", debutAnnee);
            stats.SortiesAnnee = (decimal)await sortiesAnneeCmd.ExecuteScalarAsync();

            stats.BeneficeAnnee = stats.EntreesAnnee - stats.SortiesAnnee;

            // Données pour les graphiques
            stats.FluxMensuels = await GetFluxMensuelsAsync(conn);
            stats.FluxSemestre = await GetFluxSemestrielsAsync(conn);
            stats.BeneficesParProjet = await GetBeneficesParProjetAsync(conn);
            stats.RepartitionParCategorie = await GetRepartitionParCategorieAsync(conn);
            stats.EvolutionSoldes = await GetEvolutionSoldesAsync(conn);
            // Indicateurs avancés
            stats.Indicateurs = await GetIndicateursAsync(conn);

            return stats;
        }

        public async Task<Dictionary<string, object>> GetTableauDeBordAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var tableau = new Dictionary<string, object>();

            // Résumé des comptes
            using var comptesCmd = new SqlCommand(@"
                SELECT 
                    COUNT(*) as NbComptes,
                    SUM(SoldeActuel) as SoldeTotal,
                    AVG(SoldeActuel) as SoldeMoyen,
                    MIN(SoldeActuel) as SoldeMin,
                    MAX(SoldeActuel) as SoldeMax
                FROM Comptes WHERE Actif = 1", conn);

            using var comptesReader = await comptesCmd.ExecuteReaderAsync();
            if (await comptesReader.ReadAsync())
            {
                tableau["comptes"] = new
                {
                    nombre = comptesReader.GetInt32("NbComptes"),
                    soldeTotal = comptesReader.GetDecimal("SoldeTotal"),
                    soldeMoyen = comptesReader.IsDBNull("SoldeMoyen") ? 0 : comptesReader.GetDecimal("SoldeMoyen"),
                    soldeMin = comptesReader.GetDecimal("SoldeMin"),
                    soldeMax = comptesReader.GetDecimal("SoldeMax")
                };
            }
            comptesReader.Close();

            // Activité récente (7 derniers jours)
            using var activiteCmd = new SqlCommand(@"
                SELECT 
                    COUNT(*) as NbMouvements,
                    SUM(CASE WHEN TypeMouvement = 'Entree' THEN Montant ELSE 0 END) as TotalEntrees,
                    SUM(CASE WHEN TypeMouvement = 'Sortie' THEN Montant ELSE 0 END) as TotalSorties,
                    COUNT(DISTINCT CompteId) as ComptesActifs
                FROM MouvementsFinanciers 
                WHERE DateMouvement >= DATEADD(DAY, -7, GETDATE())", conn);

            using var activiteReader = await activiteCmd.ExecuteReaderAsync();
            if (await activiteReader.ReadAsync())
            {
                tableau["activiteRecente"] = new
                {
                    nombreMouvements = activiteReader.GetInt32("NbMouvements"),
                    totalEntrees = activiteReader.GetDecimal("TotalEntrees"),
                    totalSorties = activiteReader.GetDecimal("TotalSorties"),
                    comptesActifs = activiteReader.GetInt32("ComptesActifs")
                };
            }
            activiteReader.Close();

            // Top 5 des catégories
            using var categoriesCmd = new SqlCommand(@"
                SELECT TOP 5 
                    ISNULL(Categorie, 'Non catégorisé') as Categorie,
                    COUNT(*) as NbMouvements,
                    SUM(Montant) as TotalMontant
                FROM MouvementsFinanciers 
                WHERE DateMouvement >= DATEADD(DAY, -30, GETDATE())
                GROUP BY Categorie
                ORDER BY SUM(Montant) DESC", conn);

            var topCategories = new List<object>();
            using var categoriesReader = await categoriesCmd.ExecuteReaderAsync();
            while (await categoriesReader.ReadAsync())
            {
                topCategories.Add(new
                {
                    categorie = categoriesReader.GetString("Categorie"),
                    nombreMouvements = categoriesReader.GetInt32("NbMouvements"),
                    totalMontant = categoriesReader.GetDecimal("TotalMontant")
                });
            }
            tableau["topCategories"] = topCategories;
            categoriesReader.Close();

            return tableau;
        }

        public async Task<List<string>> GetCategoriesUtiliseesAsync()
        {
            var categories = new List<string>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT DISTINCT Categorie 
                FROM MouvementsFinanciers 
                WHERE Categorie IS NOT NULL AND Categorie != ''
                ORDER BY Categorie", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                categories.Add(reader.GetString("Categorie"));
            }

            return categories;
        }

        public async Task<Dictionary<string, decimal>> GetSoldesParTypeAsync()
        {
            var soldes = new Dictionary<string, decimal>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT TypeCompte, SUM(SoldeActuel) as SoldeTotal
                FROM Comptes 
                WHERE Actif = 1 
                GROUP BY TypeCompte", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                soldes[reader.GetString("TypeCompte")] = reader.GetDecimal("SoldeTotal");
            }

            return soldes;
        }

        // =============================================
        // MÉTHODES PRIVÉES
        // =============================================

        private async Task<List<ChartData>> GetFluxMensuelsAsync(SqlConnection conn)
        {
            var data = new List<ChartData>();

            using var cmd = new SqlCommand(@"
                SELECT 
                    FORMAT(DateMouvement, 'yyyy-MM') as Mois,
                    TypeMouvement,
                    SUM(Montant) as Total
                FROM MouvementsFinanciers 
                WHERE DateMouvement >= DATEADD(MONTH, -12, GETDATE())
                    AND TypeMouvement IN ('Entree', 'Sortie')
                GROUP BY FORMAT(DateMouvement, 'yyyy-MM'), TypeMouvement
                ORDER BY Mois", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var mois = reader.GetString("Mois");
                var type = reader.GetString("TypeMouvement");
                var total = reader.GetDecimal("Total");

                data.Add(new ChartData
                {
                    Label = $"{mois}-{type}",
                    Value = total,
                    Color = type == "Entree" ? "#10b981" : "#ef4444"
                });
            }

            return data;
        }

        private async Task<List<ChartData>> GetBeneficesParProjetAsync(SqlConnection conn)
        {
            var data = new List<ChartData>();

            using var cmd = new SqlCommand(@"
                SELECT 
                    p.Nom as ProjetNom,
                    dbo.fn_CalculerBeneficeProjet(p.Id) as Benefice
                FROM Projets p
                WHERE p.Actif = 1 AND dbo.fn_CalculerBeneficeProjet(p.Id) != 0
                ORDER BY Benefice DESC", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var benefice = reader.GetDecimal("Benefice");
                data.Add(new ChartData
                {
                    Label = reader.GetString("ProjetNom"),
                    Value = benefice,
                    Color = benefice >= 0 ? "#10b981" : "#ef4444"
                });
            }

            return data;
        }
        /// <summary>
        /// Récupère les flux mensuels (entrées, sorties, résultat net) des 6 derniers mois
        /// </summary>
        private async Task<List<ChartData>> GetFluxSemestrielsAsync(SqlConnection conn)
        {
            var data = new List<ChartData>();

            using var cmd = new SqlCommand(@"
        -- CTE pour les 6 derniers mois complets
        WITH Mois AS (
            SELECT DISTINCT FORMAT(DateMouvement, 'yyyy-MM') as Mois
            FROM MouvementsFinanciers
            WHERE DateMouvement >= DATEADD(MONTH, -6, GETDATE())
                AND DateMouvement <= GETDATE()
            UNION
            -- Ajouter les mois manquants s'il n'y a pas de mouvements
            SELECT FORMAT(DATEADD(MONTH, -n, GETDATE()), 'yyyy-MM')
            FROM (VALUES (0),(1),(2),(3),(4),(5)) AS Numbers(n)
        ),
        -- Calculer les totaux par mois et type
        FluxMensuels AS (
            SELECT 
                FORMAT(DateMouvement, 'yyyy-MM') as Mois,
                TypeMouvement,
                SUM(Montant) as Total
            FROM MouvementsFinanciers 
            WHERE DateMouvement >= DATEADD(MONTH, -6, GETDATE())
                AND DateMouvement <= GETDATE()
                AND TypeMouvement IN ('Entree', 'Sortie')
            GROUP BY FORMAT(DateMouvement, 'yyyy-MM'), TypeMouvement
        ),
        -- Pivoter les données pour avoir entrées et sorties sur la même ligne
        FluxPivot AS (
            SELECT 
                m.Mois,
                ISNULL(MAX(CASE WHEN fm.TypeMouvement = 'Entree' THEN fm.Total END), 0) as Entrees,
                ISNULL(MAX(CASE WHEN fm.TypeMouvement = 'Sortie' THEN fm.Total END), 0) as Sorties
            FROM Mois m
            LEFT JOIN FluxMensuels fm ON m.Mois = fm.Mois
            GROUP BY m.Mois
        )
        SELECT 
            Mois,
            Entrees,
            Sorties,
            (Entrees - Sorties) as ResultatNet
        FROM FluxPivot
        ORDER BY Mois ASC", conn);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var mois = reader.GetString(0); // Mois au format yyyy-MM
                var entrees = reader.GetDecimal(1);
                var sorties = reader.GetDecimal(2);
                var resultatNet = reader.GetDecimal(3);

                // Formater le label en français (ex: "Jan 2025")
                var dateLabel = DateTime.ParseExact(mois + "-01", "yyyy-MM-dd", null);
                var labelFormate = dateLabel.ToString("MMM yyyy", new System.Globalization.CultureInfo("fr-FR"));

                data.Add(new ChartData
                {
                    Label = labelFormate, // "Nov 2025", "Déc 2025", etc.
                    Value = resultatNet, // Résultat net pour l'axe principal
                    Color = resultatNet >= 0 ? "#10b981" : "#ef4444", // Vert si positif, rouge si négatif
                    MetaDonnees = new Dictionary<string, object>
            {
                { "mois", mois }, // Format original yyyy-MM pour le tri
                { "entrees", entrees },
                { "sorties", sorties },
                { "resultatNet", resultatNet }
            }
                });
            }

            return data;
        }
        private async Task<List<ChartData>> GetRepartitionParCategorieAsync(SqlConnection conn)
        {
            var data = new List<ChartData>();

            using var cmd = new SqlCommand(@"
                SELECT 
                    ISNULL(Categorie, 'Non catégorisé') as Categorie,
                    SUM(Montant) as Total,
                    COUNT(*) as NbMouvements
                FROM MouvementsFinanciers 
                WHERE DateMouvement >= DATEADD(MONTH, -3, GETDATE())
                    AND TypeMouvement IN ('Entree', 'Sortie')
                GROUP BY Categorie
                ORDER BY Total DESC", conn);

            var couleurs = new[] { "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899", "#6b7280" };
            int colorIndex = 0;

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                data.Add(new ChartData
                {
                    Label = reader.GetString("Categorie"),
                    Value = reader.GetDecimal("Total"),
                    Color = couleurs[colorIndex % couleurs.Length]
                });
                colorIndex++;
            }

            return data;
        }

        private async Task<List<ChartData>> GetEvolutionSoldesAsync(SqlConnection conn)
        {
            var data = new List<ChartData>();

            using var cmd = new SqlCommand(@"
                WITH EvolutionSoldes AS (
                    SELECT 
                        CAST(DateMouvement AS DATE) as DateMvt,
                        SUM(SUM(CASE WHEN TypeMouvement = 'Entree' THEN Montant 
                                    WHEN TypeMouvement = 'Sortie' THEN -Montant 
                                    ELSE 0 END)) OVER (ORDER BY CAST(DateMouvement AS DATE)) as SoldeCumule
                    FROM MouvementsFinanciers 
                    WHERE DateMouvement >= DATEADD(DAY, -30, GETDATE())
                    GROUP BY CAST(DateMouvement AS DATE)
                )
                SELECT 
                    FORMAT(DateMvt, 'yyyy-MM-dd') as Date, 
                    SoldeCumule 
                FROM EvolutionSoldes
                ORDER BY DateMvt", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                data.Add(new ChartData
                {
                    Label = reader.GetString(0),
                    Value = reader.GetDecimal(1),
                    Color = "#3b82f6"
                });
            }

            return data;
        }
        private async Task<TresorerieIndicateurs> GetIndicateursAsync(SqlConnection conn)
        {
            var indicateurs = new TresorerieIndicateurs();

            // Taux de croissance mensuel
            using var croissanceCmd = new SqlCommand(@"
                WITH SoldesMensuels AS (
                    SELECT 
                        MONTH(DateMouvement) as Mois,
                        SUM(CASE WHEN TypeMouvement = 'Entree' THEN Montant ELSE -Montant END) as FluxNet
                    FROM MouvementsFinanciers 
                    WHERE YEAR(DateMouvement) = YEAR(GETDATE())
                        AND TypeMouvement IN ('Entree', 'Sortie')
                    GROUP BY MONTH(DateMouvement)
                )
                SELECT 
                    AVG(FluxNet) as MoyenneFluxMensuel
                FROM SoldesMensuels", conn);

            using var croissanceReader = await croissanceCmd.ExecuteReaderAsync();
            if (await croissanceReader.ReadAsync())
            {
                var moyenneFlux = croissanceReader.IsDBNull("MoyenneFluxMensuel") ? 0m : croissanceReader.GetDecimal("MoyenneFluxMensuel");
                indicateurs.TauxCroissanceMensuel = moyenneFlux;
            }
            croissanceReader.Close();

            // Moyenne des mouvements par jour
            using var moyenneCmd = new SqlCommand(@"
                SELECT 
                    COUNT(*) * 1.0 / NULLIF(DATEDIFF(DAY, MIN(DateMouvement), MAX(DateMouvement)), 0) as MoyenneParJour
                FROM MouvementsFinanciers 
                WHERE DateMouvement >= DATEADD(DAY, -30, GETDATE())", conn);
            var moyenneResult = await moyenneCmd.ExecuteScalarAsync();
            indicateurs.MoyenneMouvementsParJour = moyenneResult != null && moyenneResult != DBNull.Value ? Convert.ToDecimal(moyenneResult) : 0;

            // Plus grosse entrée et sortie
            using var extremesCmd = new SqlCommand(@"
                SELECT 
                    (SELECT ISNULL(MAX(Montant), 0) FROM MouvementsFinanciers WHERE TypeMouvement = 'Entree' AND DateMouvement >= DATEADD(DAY, -30, GETDATE())) as PlusGrosseEntree,
                    (SELECT ISNULL(MAX(Montant), 0) FROM MouvementsFinanciers WHERE TypeMouvement = 'Sortie' AND DateMouvement >= DATEADD(DAY, -30, GETDATE())) as PlusGrosseSortie", conn);
            using var extremesReader = await extremesCmd.ExecuteReaderAsync();
            if (await extremesReader.ReadAsync())
            {
                indicateurs.PlusGrosseEntree = extremesReader.GetDecimal("PlusGrosseEntree");
                indicateurs.PlusGrosseSortie = extremesReader.GetDecimal("PlusGrosseSortie");
            }
            extremesReader.Close();

            // Compte le plus utilisé
            using var compteCmd = new SqlCommand(@"
                SELECT TOP 1 c.Nom
                FROM MouvementsFinanciers mf
                JOIN Comptes c ON mf.CompteId = c.Id
                WHERE mf.DateMouvement >= DATEADD(DAY, -30, GETDATE())
                GROUP BY c.Nom
                ORDER BY COUNT(*) DESC", conn);
            var compteResult = await compteCmd.ExecuteScalarAsync();
            indicateurs.CompteLesPlusUtilise = compteResult?.ToString();

            // Nombre de mouvements du mois
            using var nbMouvCmd = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM MouvementsFinanciers 
                WHERE MONTH(DateMouvement) = MONTH(GETDATE()) AND YEAR(DateMouvement) = YEAR(GETDATE())", conn);
            indicateurs.NombreMouvementsMois = (int)await nbMouvCmd.ExecuteScalarAsync();

            return indicateurs;
        }

        private void AddCompteParameters(SqlCommand cmd, Compte compte)
        {
            cmd.Parameters.AddWithValue("@Nom", compte.Nom);
            cmd.Parameters.AddWithValue("@TypeCompte", compte.TypeCompte);
            cmd.Parameters.AddWithValue("@Numero", compte.Numero ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Banque", compte.Banque ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SoldeInitial", compte.SoldeInitial);
            cmd.Parameters.AddWithValue("@SoldeActuel", compte.SoldeActuel);
            cmd.Parameters.AddWithValue("@DateCreation", compte.DateCreation);
            cmd.Parameters.AddWithValue("@Actif", compte.Actif);
        }

        private void AddMouvementParameters(SqlCommand cmd, MouvementFinancier mouvement)
        {
            cmd.Parameters.AddWithValue("@CompteId", mouvement.CompteId);
            cmd.Parameters.AddWithValue("@TypeMouvement", mouvement.TypeMouvement);
            cmd.Parameters.AddWithValue("@Categorie", mouvement.Categorie ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FactureId", mouvement.FactureId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ProjetId", mouvement.ProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SousTraitantId", mouvement.SousTraitantId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EtapeProjetId", mouvement.EtapeProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Libelle", mouvement.Libelle);
            cmd.Parameters.AddWithValue("@Description", mouvement.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Montant", mouvement.Montant);
            cmd.Parameters.AddWithValue("@DateMouvement", mouvement.DateMouvement);
            cmd.Parameters.AddWithValue("@DateSaisie", mouvement.DateSaisie);
            cmd.Parameters.AddWithValue("@ModePaiement", mouvement.ModePaiement ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Reference", mouvement.Reference ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CompteDestinationId", mouvement.CompteDestinationId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", mouvement.UtilisateurCreation);
        }

        private Compte MapToCompte(SqlDataReader reader)
        {
            return new Compte
            {
                Id = reader.GetInt32("Id"),
                Nom = reader.GetString("Nom"),
                TypeCompte = reader.GetString("TypeCompte"),
                Numero = reader.IsDBNull("Numero") ? null : reader.GetString("Numero"),
                Banque = reader.IsDBNull("Banque") ? null : reader.GetString("Banque"),
                SoldeInitial = reader.GetDecimal("SoldeInitial"),
                SoldeActuel = reader.GetDecimal("SoldeActuel"),
                DateCreation = reader.GetDateTime("DateCreation"),
                Actif = reader.GetBoolean("Actif")
            };
        }

        private MouvementFinancier MapToMouvementFinancier(SqlDataReader reader)
        {
            return new MouvementFinancier
            {
                Id = reader.GetInt32("Id"),
                CompteId = reader.GetInt32("CompteId"),
                TypeMouvement = reader.GetString("TypeMouvement"),
                Categorie = reader.IsDBNull("Categorie") ? null : reader.GetString("Categorie"),
                FactureId = reader.IsDBNull("FactureId") ? null : reader.GetInt32("FactureId"),
                ProjetId = reader.IsDBNull("ProjetId") ? null : reader.GetInt32("ProjetId"),
                SousTraitantId = reader.IsDBNull("SousTraitantId") ? null : reader.GetInt32("SousTraitantId"),
                EtapeProjetId = reader.IsDBNull("EtapeProjetId") ? null : reader.GetInt32("EtapeProjetId"),
                Libelle = reader.GetString("Libelle"),
                Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                Montant = reader.GetDecimal("Montant"),
                DateMouvement = reader.GetDateTime("DateMouvement"),
                DateSaisie = reader.GetDateTime("DateSaisie"),
                ModePaiement = reader.IsDBNull("ModePaiement") ? null : reader.GetString("ModePaiement"),
                Reference = reader.IsDBNull("Reference") ? null : reader.GetString("Reference"),
                CompteDestinationId = reader.IsDBNull("CompteDestinationId") ? null : reader.GetInt32("CompteDestinationId"),
                UtilisateurCreation = reader.GetInt32("UtilisateurCreation"),

                Compte = new Compte
                {
                    Nom = reader.GetString("CompteNom")
                }

            };
        }
    }
}