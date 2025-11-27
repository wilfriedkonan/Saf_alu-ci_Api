using Microsoft.Data.SqlClient;
using QuestPDF.Infrastructure;
using System;
using System.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Saf_alu_ci_Api.Controllers.Dashboard
{
    public class DashboardService
    {
        private readonly string _connectionString;

        public DashboardService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // =============================================
        // MÉTHODES EXISTANTES (CONSERVÉES)
        // =============================================

        public async Task<DashboardKPIs> GetKPIsAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM v_KPIDashboard", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new DashboardKPIs
                {
                    ProjetsActifs = reader.GetInt32("ProjetsActifs"),
                    DevisEnAttente = reader.GetInt32("DevisEnAttente"),
                    FacturesImpayes = reader.GetInt32("FacturesImpayes"),
                    RevenusMois = reader.GetDecimal("RevenusMois"),
                    TresorerieTotal = reader.GetDecimal("TresorerieTotal"),
                    objectifFinancier = reader.GetDecimal("objectifFinancier")
                };
            }

            return new DashboardKPIs();
        }

        public async Task<DashboardKPIsCAFacture> GetKPIsCAFactureAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM VW_CA_MoisEnCours", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new DashboardKPIsCAFacture
                {
                    NombreFactureEmise = reader.GetInt32("NombreFactures"),
                    ChiffreAffireHT = reader.GetDecimal("ChiffreAffaireHT"),
                    ChiffreAffireTTC = reader.GetDecimal("ChiffreAffaireTTC"),
                   
                };
            }

            return new DashboardKPIsCAFacture();
        }

        public async Task<List<ProjetActif>> GetProjetsActifsAsync()
        {
            var projets = new List<ProjetActif>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM v_ProjetsActifs ORDER BY DateFinPrevue", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                projets.Add(new ProjetActif
                {
                    Id = reader.GetInt32("Id"),
                    Numero = reader.GetString("Numero"),
                    Nom = reader.GetString("Nom"),
                    Statut = reader.GetString("Statut"),
                    PourcentageAvancement = reader.GetInt32("PourcentageAvancement"),
                    NomClient = reader.GetString("NomClient"),
                    TypeProjet = reader.GetString("TypeProjet"),
                    BudgetRevise = reader.GetDecimal("BudgetRevise"),
                    CoutReel = reader.GetDecimal("CoutReel"),
                    DateDebut = reader.IsDBNull("DateDebut") ? null : reader.GetDateTime("DateDebut"),
                    DateFinPrevue = reader.IsDBNull("DateFinPrevue") ? null : reader.GetDateTime("DateFinPrevue"),
                    ChefProjet = reader.IsDBNull("ChefProjet") ? null : reader.GetString("ChefProjet")
                });
            }

            return projets;
        }

        public async Task<List<ChartData>> GetRevenusParMoisAsync(int nbMois = 6)
        {
            var data = new List<ChartData>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Générer la liste des 12 derniers mois même sans factures
            var monthsQuery = @"
        ;WITH MoisList AS (
            SELECT 
                DATEADD(MONTH, -(@NbMois - 1), DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)) AS Mois
            UNION ALL
            SELECT DATEADD(MONTH, 1, Mois)
            FROM MoisList
            WHERE Mois < DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
        )
        SELECT 
            FORMAT(m.Mois, 'MMM yyyy', 'fr-FR') AS MoisLabel,
            ISNULL(SUM(f.MontantTTC), 0) AS Total
        FROM MoisList m
        LEFT JOIN Factures f 
            ON YEAR(f.DateCreation) = YEAR(m.Mois)
           AND MONTH(f.DateCreation) = MONTH(m.Mois)
           AND f.Statut NOT IN ('Annulee', 'Brouillon')
        GROUP BY m.Mois
        ORDER BY m.Mois;
    ";

            using var cmd = new SqlCommand(monthsQuery, conn);
            cmd.Parameters.AddWithValue("@NbMois", nbMois);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                data.Add(new ChartData
                {
                    Label = reader.GetString(0),     // MoisLabel
                    Value = reader.GetDecimal(1)     // Total
                });
            }

            return data;
        }

        public async Task<List<ChartData>> GetRepartitionProjetsParTypeAsync()
        {
            var data = new List<ChartData>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    tp.Nom as TypeProjet,
                    COUNT(*) as NbProjets
                FROM Projets p
                INNER JOIN TypesProjets tp ON p.TypeProjetId = tp.Id
                WHERE p.Actif = 1
                GROUP BY tp.Nom
                ORDER BY NbProjets DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                data.Add(new ChartData
                {
                    Label = reader.GetString("TypeProjet"),
                    Value = reader.GetInt32("NbProjets")
                });
            }

            return data;
        }

        // =============================================
        // NOUVELLES MÉTHODES - STATISTIQUES PAR RÔLE
        // =============================================

        /// <summary>
        /// Récupère les statistiques globales pour admin/super_admin
        /// Utilise la vue v_KPIDashboard existante et ajoute des calculs
        /// </summary>
        public async Task<DashboardStatsGlobal> GetStatistiquesGlobalesAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Récupérer les KPIs de base depuis la vue existante
            var kpis = await GetKPIsAsync();

            //Chiffre d'affaire sur les factures emises (kpisCaFac) 
            var kpisCaFac = await GetKPIsCAFactureAsync();

            // Calculer le changement par rapport au mois précédent
            using var cmd = new SqlCommand(@"
                DECLARE @DebutMois DATETIME = DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()), 0)
                DECLARE @DebutMoisPrecedent DATETIME = DATEADD(MONTH, -1, @DebutMois)

                -- CA mois précédent
                DECLARE @CaMoisPrecedent DECIMAL(18,2) = (
                    SELECT ISNULL(SUM(MontantTTC), 0) 
                    FROM Factures 
                    WHERE Statut != 'Brouillon' AND Statut != 'Annulee'
                    AND DateCreation >= @DebutMoisPrecedent 
                    AND DateCreation < @DebutMois
                )

                -- Projets actifs mois précédent
                DECLARE @ProjetsActifsMoisPrecedent INT = (
                    SELECT COUNT(*) 
                    FROM Projets 
                    WHERE Statut = 'EnCours' 
                    AND DateCreation < @DebutMois
                )

                -- Solde mois précédent (approximation)
                DECLARE @MouvementsMois DECIMAL(18,2) = (
                    SELECT ISNULL(SUM(CASE 
                        WHEN TypeMouvement = 'Entree' THEN Montant 
                        ELSE -Montant 
                    END), 0)
                    FROM MouvementsFinanciers
                    WHERE DateMouvement >= @DebutMois
                )

                SELECT 
                    @CaMoisPrecedent AS CaMoisPrecedent,
                    @ProjetsActifsMoisPrecedent AS ProjetsActifsMoisPrecedent,
                    @MouvementsMois AS MouvementsMois", conn);

            decimal caMoisPrecedent = 0;
            int projetsActifsMoisPrecedent = 0;
            decimal mouvementsMois = 0;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    caMoisPrecedent = reader.GetDecimal(0);
                    projetsActifsMoisPrecedent = reader.GetInt32(1);
                    mouvementsMois = reader.GetDecimal(2);
                }
            }

            // Calculer les changements
            var changementCA = caMoisPrecedent > 0
                ? ((kpisCaFac.ChiffreAffireTTC - caMoisPrecedent) / caMoisPrecedent) * 100
                : 0;

            var changementProjets = kpis.ProjetsActifs - projetsActifsMoisPrecedent;

            var soldeMoisPrecedent = kpis.TresorerieTotal - mouvementsMois;
            var changementSolde = soldeMoisPrecedent > 0
                ? ((kpis.TresorerieTotal - soldeMoisPrecedent) / soldeMoisPrecedent) * 100
                : 0;
            return new DashboardStatsGlobal
            {
                ChiffreAffaires = new StatKPI
                {
                    Valeur = $"{kpisCaFac.ChiffreAffireTTC:N0}F",
                    Changement = $"{(changementCA >= 0 ? "+" : "")}{changementCA:F1}%",
                    Type = changementCA >= 0 ? "hausse" : "baisse"
                },
                ProjetsActifs = new StatKPI
                {
                    Valeur = kpis.ProjetsActifs.ToString(),
                    Changement = $"{(changementProjets >= 0 ? "+" : "")}{changementProjets}",
                    Type = changementProjets >= 0 ? "hausse" : "baisse"
                },
                ObjectifAnnuel = new StatKPI
                {
                    Valeur = $"{kpis.objectifFinancier:N0}F",
                    Changement = "devis en attente",
                    Type = "neutre"
                },
                SoldeComptes = new StatKPI
                {
                    Valeur = $"{kpis.TresorerieTotal:N0}F",
                    Changement = $"{(changementSolde >= 0 ? "+" : "")}{changementSolde:F1}%",
                    Type = changementSolde >= 0 ? "hausse" : "baisse"
                }
            };
        }

        /// <summary>
        /// Récupère les statistiques pour un chef de projet
        /// </summary>
        public async Task<DashboardStatsChefProjet> GetStatistiquesChefProjetAsync(int chefProjetId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                DECLARE @DebutMois DATETIME = DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()), 0)

                -- Mes projets
                DECLARE @MesProjets INT = (
                    SELECT COUNT(*) 
                    FROM Projets 
                    WHERE ChefProjetId = @ChefProjetId 
                    AND Statut != 'Termine'
                    AND Actif = 1
                )

                -- Projets en retard
                DECLARE @ProjetsEnRetard INT = (
                    SELECT COUNT(*) 
                    FROM Projets 
                    WHERE ChefProjetId = @ChefProjetId 
                    AND DateFinPrevue < GETDATE() 
                    AND Statut = 'EnCours'
                    AND Actif = 1
                )

                -- Tâches terminées (étapes à 100%)
                DECLARE @TachesTerminees INT = (
                    SELECT COUNT(*) 
                    FROM EtapeProjets ep
                    INNER JOIN Projets p ON ep.ProjetId = p.Id
                    WHERE p.ChefProjetId = @ChefProjetId 
                    AND ep.AvancementReel >= 100
                    AND p.Actif = 1
                )

                -- Tâches terminées mois précédent
                DECLARE @TachesTermineesMoisPrecedent INT = (
                    SELECT COUNT(*) 
                    FROM EtapeProjets ep
                    INNER JOIN Projets p ON ep.ProjetId = p.Id
                    WHERE p.ChefProjetId = @ChefProjetId 
                    AND ep.AvancementReel >= 100
                    AND ep.DateDebut < @DebutMois
                    AND p.Actif = 1
                )

                -- Équipe active (nombre de collaborateurs sur projets actifs)
                DECLARE @EquipeActive INT = (
                    SELECT COUNT(DISTINCT ChefProjetId) 
                    FROM Projets 
                    WHERE ChefProjetId = @ChefProjetId 
                    AND Statut = 'EnCours'
                    AND Actif = 1
                )

                SELECT 
                    @MesProjets AS MesProjets,
                    @ProjetsEnRetard AS ProjetsEnRetard,
                    @TachesTerminees AS TachesTerminees,
                    @TachesTermineesMoisPrecedent AS TachesTermineesMoisPrecedent,
                    @EquipeActive AS EquipeActive", conn);

            cmd.Parameters.AddWithValue("@ChefProjetId", chefProjetId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var mesProjets = reader.GetInt32(0);
                var projetsEnRetard = reader.GetInt32(1);
                var tachesTerminees = reader.GetInt32(2);
                var tachesTermineesMoisPrecedent = reader.GetInt32(3);
                var equipeActive = reader.GetInt32(4);

                var changementTaches = tachesTerminees - tachesTermineesMoisPrecedent;

                return new DashboardStatsChefProjet
                {
                    MesProjets = new StatKPI
                    {
                        Valeur = mesProjets.ToString(),
                        Changement = $"+{mesProjets}",
                        Type = "neutre"
                    },
                    ProjetsEnRetard = new StatKPI
                    {
                        Valeur = projetsEnRetard.ToString(),
                        Changement = $"-{projetsEnRetard}",
                        Type = projetsEnRetard > 0 ? "baisse" : "neutre"
                    },
                    TachesTerminees = new StatKPI
                    {
                        Valeur = tachesTerminees.ToString(),
                        Changement = $"+{changementTaches}",
                        Type = "hausse"
                    },
                    EquipeActive = new StatKPI
                    {
                        Valeur = equipeActive.ToString(),
                        Changement = "0",
                        Type = "neutre"
                    }
                };
            }

            return new DashboardStatsChefProjet();
        }

        /// <summary>
        /// Récupère les statistiques pour un commercial
        /// </summary>
        public async Task<DashboardStatsCommercial> GetStatistiquesCommercialAsync(int commercialId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                DECLARE @DebutMois DATETIME = DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()), 0)
                DECLARE @DebutMoisPrecedent DATETIME = DATEADD(MONTH, -1, @DebutMois)

                -- Devis envoyés ce mois
                DECLARE @DevisEnvoyes INT = (
                    SELECT COUNT(*) 
                    FROM Devis 
                    WHERE CommercialId = @CommercialId 
                    AND DateCreation >= @DebutMois
                )

                -- Devis envoyés mois précédent
                DECLARE @DevisEnvoyesMoisPrecedent INT = (
                    SELECT COUNT(*) 
                    FROM Devis 
                    WHERE CommercialId = @CommercialId 
                    AND DateCreation >= @DebutMoisPrecedent 
                    AND DateCreation < @DebutMois
                )

                -- Taux de conversion
                DECLARE @DevisTotal INT = (
                    SELECT COUNT(*) 
                    FROM Devis 
                    WHERE CommercialId = @CommercialId 
                    AND DateCreation >= @DebutMois
                )

                DECLARE @DevisAcceptes INT = (
                    SELECT COUNT(*) 
                    FROM Devis 
                    WHERE CommercialId = @CommercialId 
                    AND DateCreation >= @DebutMois 
                    AND Statut = 'Accepte'
                )

                DECLARE @TauxConversion DECIMAL(5,2) = 
                    CASE WHEN @DevisTotal > 0 
                    THEN (@DevisAcceptes * 100.0 / @DevisTotal)
                    ELSE 0 END

                -- Devis en attente
                DECLARE @DevisEnAttente INT = (
                    SELECT COUNT(*) 
                    FROM Devis 
                    WHERE CommercialId = @CommercialId 
                    AND (Statut = 'EnAttente' OR Statut = 'Envoye')
                )

                -- Clients prospects
                DECLARE @ClientsProspects INT = (
                    SELECT COUNT(*) 
                    FROM Clients 
                    WHERE CommercialId = @CommercialId 
                    AND DateCreation >= @DebutMois
                )

                SELECT 
                    @DevisEnvoyes AS DevisEnvoyes,
                    @DevisEnvoyesMoisPrecedent AS DevisEnvoyesMoisPrecedent,
                    @TauxConversion AS TauxConversion,
                    @DevisEnAttente AS DevisEnAttente,
                    @ClientsProspects AS ClientsProspects", conn);

            cmd.Parameters.AddWithValue("@CommercialId", commercialId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var devisEnvoyes = reader.GetInt32(0);
                var devisEnvoyesMoisPrecedent = reader.GetInt32(1);
                var tauxConversion = reader.GetDecimal(2);
                var devisEnAttente = reader.GetInt32(3);
                var clientsProspects = reader.GetInt32(4);

                var changementDevis = devisEnvoyes - devisEnvoyesMoisPrecedent;

                return new DashboardStatsCommercial
                {
                    DevisEnvoyes = new StatKPI
                    {
                        Valeur = devisEnvoyes.ToString(),
                        Changement = $"+{changementDevis}",
                        Type = changementDevis >= 0 ? "hausse" : "baisse"
                    },
                    TauxConversion = new StatKPI
                    {
                        Valeur = $"{tauxConversion:F0}%",
                        Changement = $"+{tauxConversion:F1}%",
                        Type = "hausse"
                    },
                    DevisEnAttente = new StatKPI
                    {
                        Valeur = devisEnAttente.ToString(),
                        Changement = $"-{devisEnAttente}",
                        Type = "neutre"
                    },
                    ClientsProspects = new StatKPI
                    {
                        Valeur = clientsProspects.ToString(),
                        Changement = $"+{clientsProspects}",
                        Type = "hausse"
                    }
                };
            }

            return new DashboardStatsCommercial();
        }

        /// <summary>
        /// Récupère les statistiques pour un comptable
        /// Utilise les données de la vue v_KPIDashboard
        /// </summary>
        public async Task<DashboardStatsComptable> GetStatistiquesComptableAsync()
        {
            var kpis = await GetKPIsAsync();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                DECLARE @DebutMois DATETIME = DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()), 0)

                -- Factures impayées montant total
                DECLARE @FacturesImpayees DECIMAL(18,2) = (
                    SELECT ISNULL(SUM(MontantTTC - MontantPaye), 0)
                    FROM Factures
                    WHERE Statut IN ('Impayee', 'PartellementPayee')
                )

                -- Factures du mois
                DECLARE @FacturesMois INT = (
                    SELECT COUNT(*) 
                    FROM Factures 
                    WHERE DateCreation >= @DebutMois
                )

                -- Retards de paiement
                DECLARE @RetardsPaiement INT = (
                    SELECT COUNT(*) 
                    FROM Factures 
                    WHERE DateEcheance < GETDATE() 
                    AND Statut IN ('Impayee', 'PartellementPayee')
                )

                SELECT 
                    @FacturesImpayees AS FacturesImpayees,
                    @FacturesMois AS FacturesMois,
                    @RetardsPaiement AS RetardsPaiement", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var facturesImpayees = reader.GetDecimal(0);
                var facturesMois = reader.GetInt32(1);
                var retardsPaiement = reader.GetInt32(2);

                return new DashboardStatsComptable
                {
                    FacturesImpayees = new StatKPI
                    {
                        Valeur = $"{facturesImpayees:N0}F",
                        Changement = $"-{facturesImpayees:N0}F",
                        Type = "baisse"
                    },
                    Tresorerie = new StatKPI
                    {
                        Valeur = $"{kpis.TresorerieTotal:N0}F",
                        Changement = $"+{kpis.TresorerieTotal:N0}F",
                        Type = "hausse"
                    },
                    FacturesMois = new StatKPI
                    {
                        Valeur = facturesMois.ToString(),
                        Changement = $"+{facturesMois}",
                        Type = "hausse"
                    },
                    RetardsPaiement = new StatKPI
                    {
                        Valeur = retardsPaiement.ToString(),
                        Changement = $"-{retardsPaiement}",
                        Type = retardsPaiement > 0 ? "baisse" : "neutre"
                    }
                };
            }

            return new DashboardStatsComptable();
        }

        /// <summary>
        /// Récupère les projets nécessitant une attention
        /// </summary>
        public async Task<List<AlerteProjet>> GetProjetsAlerteAsync(int? chefProjetId = null)
        {
            var alertes = new List<AlerteProjet>();


            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string filter = chefProjetId.HasValue ? "ChefProjetId = @ChefProjetId AND" : "";

            var query = @"

SELECT TOP 10
Id,
Nom,
Type,
Message,
Niveau
FROM
(
-- 📌 Projets en retard
SELECT
p.Id,
p.Nom,
'retard' AS Type,
'En retard de ' + CAST(DATEDIFF(DAY, p.DateFinPrevue, GETDATE()) AS VARCHAR(10)) + ' jours' AS Message,
'urgent' AS Niveau,
p.DateFinPrevue AS SortValue
FROM Projets p
WHERE " + filter + @"
p.Statut = 'EnCours'
AND p.DateFinPrevue < GETDATE()
AND p.Actif = 1


UNION ALL

-- 📌 Projets ayant dépassé le budget
SELECT
    p.Id,
    p.Nom,
    'budget' AS Type,
    'Budget dépassé de ' + 
    CAST(((p.CoutReel - p.BudgetRevise) / p.BudgetRevise * 100) AS VARCHAR(10)) + '%' AS Message,
    'attention' AS Niveau,
    p.DateFinPrevue AS SortValue
FROM Projets p
WHERE " + filter + @"
      p.Statut = 'EnCours'
  AND p.CoutReel > p.BudgetRevise * 1.1
  AND p.Actif = 1


) AS alertes
ORDER BY SortValue DESC";


            using var cmd = new SqlCommand(query, conn);

            if (chefProjetId.HasValue)
                cmd.Parameters.AddWithValue("@ChefProjetId", chefProjetId.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                alertes.Add(new AlerteProjet
                {
                    ProjetId = reader.GetInt32(0),
                    NomProjet = reader.GetString(1),
                    Type = reader.GetString(2),
                    Message = reader.GetString(3),
                    Niveau = reader.GetString(4)
                });
            }

            return alertes;

        }

        /// <summary>
        /// Récupère les activités récentes
        /// </summary>
        public async Task<List<ActiviteRecente>> GetActivitesRecentesAsync(int limite = 10)
        {
            var activites = new List<ActiviteRecente>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // 1. Récupérer les devis récents
            using (var cmd = new SqlCommand(@"
                SELECT TOP 5
                    Numero,
                    Statut,
                    DateCreation
                FROM Devis
                ORDER BY DateCreation DESC", conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var numero = reader.GetString(0);
                    var statut = reader.GetString(1);
                    var date = reader.GetDateTime(2);

                    var message = statut == "Accepte"
                        ? $"Devis #{numero} validé"
                        : $"Devis #{numero} créé";

                    var couleur = statut == "Accepte" ? "green" : "blue";

                    activites.Add(new ActiviteRecente
                    {
                        Type = "devis",
                        Message = message,
                        DateActivite = date,
                        Couleur = couleur
                    });
                }
            }

            // 2. Récupérer les projets récents
            using (var cmd = new SqlCommand(@"
                SELECT TOP 5
                    Nom,
                    DateCreation
                FROM Projets
                WHERE Actif = 1
                ORDER BY DateCreation DESC", conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var nom = reader.GetString(0);
                    var date = reader.GetDateTime(1);

                    activites.Add(new ActiviteRecente
                    {
                        Type = "projet",
                        Message = $"Nouveau projet {nom} créé",
                        DateActivite = date,
                        Couleur = "blue"
                    });
                }
            }

            // 3. Récupérer les factures en retard
            using (var cmd = new SqlCommand(@"
                SELECT TOP 5
                    Numero,
                    DateEcheance
                FROM Factures
                WHERE DateEcheance < GETDATE() 
                AND Statut IN ('Impayee', 'PartellementPayee')
                ORDER BY DateEcheance DESC", conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var numero = reader.GetString(0);
                    var date = reader.GetDateTime(1);

                    activites.Add(new ActiviteRecente
                    {
                        Type = "facture",
                        Message = $"Facture #{numero} en retard de paiement",
                        DateActivite = date,
                        Couleur = "orange"
                    });
                }
            }

            // Trier par date décroissante et limiter
            return activites
                .OrderByDescending(a => a.DateActivite)
                .Take(limite)
                .ToList();
        }
        /// <summary>
        /// Récupère la répartition des projets par statut
        /// </summary>
        public async Task<List<ChartData>> GetRepartitionProjetsParStatutAsync()
        {
            var data = new List<ChartData>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    CASE Statut
                        WHEN 'EnCours' THEN 'En cours'
                        WHEN 'Termine' THEN 'Terminés'
                        WHEN 'Planifie' THEN 'Planifiés'
                        ELSE Statut
                    END AS StatutLabel,
                    COUNT(*) AS NbProjets,
                    CASE Statut
                        WHEN 'EnCours' THEN '#3b82f6'
                        WHEN 'Termine' THEN '#10b981'
                        WHEN 'Planifie' THEN '#f59e0b'
                        ELSE '#6b7280'
                    END AS Couleur
                FROM Projets
                WHERE Actif = 1
                GROUP BY Statut
                
                UNION ALL
                
                SELECT 
                    'En retard' AS StatutLabel,
                    COUNT(*) AS NbProjets,
                    '#ef4444' AS Couleur
                FROM Projets
                WHERE Statut = 'EnCours' 
                AND DateFinPrevue < GETDATE()
                AND Actif = 1
                
                ORDER BY NbProjets DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                data.Add(new ChartData
                {
                    Label = reader.GetString(0),
                    Value = reader.GetInt32(1),
                    Color = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }

            return data;
        }
    }

    // =============================================
    // DTOs EXISTANTS (CONSERVÉS)
    // =============================================

    public class DashboardKPIs
    {
        public int ProjetsActifs { get; set; }
        public int DevisEnAttente { get; set; }
        public int FacturesImpayes { get; set; }
        public decimal RevenusMois { get; set; }
        public decimal TresorerieTotal { get; set; }
        public decimal objectifFinancier { get; set; }
    }

    public class DashboardKPIsCAFacture
    {
       
        public int NombreFactureEmise { get; set; }
        public decimal ChiffreAffireHT { get; set; }
        public decimal ChiffreAffireTTC{ get; set; }
        public decimal objectifFinancier { get; set; }
    }

    public class ProjetActif
    {
        public int Id { get; set; }
        public string Numero { get; set; }
        public string Nom { get; set; }
        public string Statut { get; set; }
        public int PourcentageAvancement { get; set; }
        public string NomClient { get; set; }
        public string TypeProjet { get; set; }
        public decimal BudgetRevise { get; set; }
        public decimal CoutReel { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public string? ChefProjet { get; set; }
    }

    public class ChartData
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
        public string? Color { get; set; }
    }

    // =============================================
    // NOUVEAUX DTOs
    // =============================================

    public class StatKPI
    {
        public string Valeur { get; set; }
        public string Changement { get; set; }
        public string Type { get; set; } // hausse, baisse, neutre
    }

    public class DashboardStatsGlobal
    {
        public StatKPI ChiffreAffaires { get; set; }
        public StatKPI ProjetsActifs { get; set; }
        public StatKPI ObjectifAnnuel { get; set; }
        public StatKPI SoldeComptes { get; set; }
    }

    public class DashboardStatsChefProjet
    {
        public StatKPI MesProjets { get; set; }
        public StatKPI ProjetsEnRetard { get; set; }
        public StatKPI TachesTerminees { get; set; }
        public StatKPI EquipeActive { get; set; }
    }

    public class DashboardStatsCommercial
    {
        public StatKPI DevisEnvoyes { get; set; }
        public StatKPI TauxConversion { get; set; }
        public StatKPI DevisEnAttente { get; set; }
        public StatKPI ClientsProspects { get; set; }
    }

    public class DashboardStatsComptable
    {
        public StatKPI FacturesImpayees { get; set; }
        public StatKPI Tresorerie { get; set; }
        public StatKPI FacturesMois { get; set; }
        public StatKPI RetardsPaiement { get; set; }
    }

    public class AlerteProjet
    {
        public int ProjetId { get; set; }
        public string NomProjet { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public string Niveau { get; set; }
    }

    public class ActiviteRecente
    {
        public string Type { get; set; }
        public string Message { get; set; }
        public DateTime DateActivite { get; set; }
        public string Couleur { get; set; }
    }
}