using Microsoft.Data.SqlClient;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.Dashboard
{
    public class DashboardService
    {
        private readonly string _connectionString;

        public DashboardService(string connectionString)
        {
            _connectionString = connectionString;
        }

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
                    TresorerieTotal = reader.GetDecimal("TresorerieTotal")
                };
            }

            return new DashboardKPIs();
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

        public async Task<List<ChartData>> GetRevenusParMoisAsync(int nbMois = 12)
        {
            var data = new List<ChartData>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    FORMAT(DatePaiement, 'yyyy-MM') as Mois,
                    SUM(MontantTTC) as Total
                FROM Factures 
                WHERE Statut = 'Payee' 
                    AND DatePaiement >= DATEADD(MONTH, -@NbMois, GETDATE())
                GROUP BY FORMAT(DatePaiement, 'yyyy-MM')
                ORDER BY Mois", conn);

            cmd.Parameters.AddWithValue("@NbMois", nbMois);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                data.Add(new ChartData
                {
                    Label = reader.GetString("Mois"),
                    Value = reader.GetDecimal("Total")
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
    }

    public class DashboardKPIs
    {
        public int ProjetsActifs { get; set; }
        public int DevisEnAttente { get; set; }
        public int FacturesImpayes { get; set; }
        public decimal RevenusMois { get; set; }
        public decimal TresorerieTotal { get; set; }
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
    }
}
