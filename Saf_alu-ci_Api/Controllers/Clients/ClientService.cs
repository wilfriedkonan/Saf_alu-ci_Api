using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Devis;
using System.Data;
using System.Text;

namespace Saf_alu_ci_Api.Controllers.Clients
{
    public class ClientService
    {
        private readonly string _connectionString;

        public ClientService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Récupère tous les clients actifs avec leurs statistiques
        /// </summary>
        public async Task<List<ClientWithStatistique>> GetAllAsync()
        {
            var clients = new List<ClientWithStatistique>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    c.*,
                    COUNT(DISTINCT d.Id) as TotalDevis,
                    COUNT(DISTINCT f.Id) as TotalFactures,
                    COUNT(DISTINCT p.Id) as TotalProjets,
                    ISNULL(SUM(CASE WHEN f.Statut IN ('Payee', 'Envoyee', 'PartPayee') THEN f.MontantTTC ELSE 0 END), 0) as TotalRevenue
                FROM Clients c
                LEFT JOIN Devis d ON c.Id = d.ClientId
                LEFT JOIN Factures f ON c.Id = f.ClientId
                LEFT JOIN Projets p ON c.ID = p.ClientId
                WHERE c.Actif = 1
                GROUP BY c.Id, c.TypeClient, c.Nom, c.RaisonSociale, c.Email, 
                         c.Telephone, c.Adresse, c.Ville, c.Ncc, c.Status, 
                         c.DateCreation, c.DateModification, c.Actif, c.UtilisateurCreation
                ORDER BY c.DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                clients.Add(MapToClientWithStatistique(reader));
            }

            return clients;
        }

        /// <summary>
        /// Récupère un client par son ID avec ses statistiques
        /// </summary>
        public async Task<ClientWithStatistique?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    c.*,
                    COUNT(DISTINCT d.Id) as TotalDevis,
                    COUNT(DISTINCT f.Id) as TotalFactures,
                    COUNT(DISTINCT p.Id) as TotalProjets,
                    ISNULL(SUM(CASE WHEN f.Statut IN ('Payee', 'Envoyee', 'PartPayee') THEN f.MontantTTC ELSE 0 END), 0) as TotalRevenue
                FROM Clients c
                LEFT JOIN Devis d ON c.Id = d.ClientId
                LEFT JOIN Factures f ON c.Id = f.ClientId
                LEFT JOIN Projets p ON c.ID = p.ClientId
                WHERE c.Id = @Id AND c.Actif = 1
                GROUP BY c.Id, c.TypeClient, c.Nom, c.RaisonSociale, c.Email, 
                         c.Telephone, c.Adresse, c.Ville, c.Ncc, c.Status, 
                         c.DateCreation, c.DateModification, c.Actif, c.UtilisateurCreation", conn);

            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return MapToClientWithStatistique(reader);
            }

            return null;
        }

        /// <summary>
        /// Récupère les statistiques d'un client spécifique
        /// </summary>
        public async Task<Statistique> GetClientStatistiquesAsync(int clientId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    COUNT(DISTINCT d.Id) as TotalDevis,
                    COUNT(DISTINCT f.Id) as TotalFactures,
                    COUNT(DISTINCT p.Id) as TotalProjets,
                    ISNULL(SUM(CASE WHEN f.Statut IN ('Payee', 'Envoyee', 'PartPayee') THEN f.MontantTTC ELSE 0 END), 0) as TotalRevenue
                FROM Clients c
                LEFT JOIN Devis d ON c.Id = d.ClientId
                LEFT JOIN Factures f ON c.Id = f.ClientId
                LEFT JOIN Projets p ON c.ID = p.ClientId
                WHERE c.Id = @ClientId
                GROUP BY c.Id", conn);

            cmd.Parameters.AddWithValue("@ClientId", clientId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new Statistique
                {
                    TotalDevis = reader.GetInt32(reader.GetOrdinal("TotalDevis")),
                    TotalFactures = reader.GetInt32(reader.GetOrdinal("TotalFactures")),
                    TotalRevenue = reader.GetDecimal(reader.GetOrdinal("TotalRevenue")),
                    TotalProjets = reader.GetInt32(reader.GetOrdinal("TotalFactures"))
                };
            }

            return new Statistique { TotalDevis = 0, TotalFactures = 0, TotalProjets = 0, TotalRevenue = 0 };
        }
        public async Task<StatistiqueGolbal> GetStatistiquesAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    COUNT(*) as TotalClients,
                    COUNT(CASE WHEN TypeClient = 'Entreprise' THEN 1 END) as TotalEntreprises,
                    COUNT(CASE WHEN Status = 'prospect' THEN 1 END) as TotalProspects,
                    COUNT(CASE WHEN Actif = 'true' THEN 1 END) as TotalActifs
                FROM Clients", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new StatistiqueGolbal
                {
                    TotalClients = reader.GetInt32("TotalClients"),
                    TotalEntreprises = reader.GetInt32("TotalEntreprises"),
                    TotalProspects = reader.GetInt32("TotalProspects"),
                    TotalActifs = reader.GetInt32("TotalActifs"),
                };
            }

            return new StatistiqueGolbal();
        }

        /// <summary>
        /// Crée un nouveau client
        /// </summary>
        public async Task<int> CreateAsync(Client client)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO Clients (
                    TypeClient, Nom, RaisonSociale, Email, Telephone, 
                    Adresse, Ville, Ncc, Status, DateCreation, DateModification, 
                    Actif, UtilisateurCreation
                )
                VALUES (
                    @TypeClient, @Nom, @RaisonSociale, @Email, @Telephone, 
                    @Adresse, @Ville, @Ncc, @Status, @DateCreation, @DateModification, 
                    @Actif, @UtilisateurCreation
                );
                SELECT CAST(SCOPE_IDENTITY() as int)", conn);

            AddClientParameters(cmd, client);
            await conn.OpenAsync();

            var result = await cmd.ExecuteScalarAsync();
            return (int)result;
        }

        /// <summary>
        /// Met à jour un client existant
        /// </summary>
        public async Task UpdateAsync(Client client)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Clients SET 
                    TypeClient = @TypeClient,
                    Nom = @Nom,
                    RaisonSociale = @RaisonSociale,
                    Email = @Email,
                    Telephone = @Telephone,
                    Adresse = @Adresse,
                    Ville = @Ville,
                    Ncc = @Ncc,
                    Status = @Status,
                    DateModification = @DateModification
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", client.Id);
            AddClientParameters(cmd, client);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Supprime un client (soft delete)
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Clients 
                SET Actif = 0, DateModification = @DateModification 
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Recherche des clients par critères avec statistiques
        /// </summary>
        public async Task<List<ClientWithStatistique>> SearchAsync(string? nom, string? typeClient, string? status)
        {
            var clients = new List<ClientWithStatistique>();
            var whereConditions = new List<string> { "c.Actif = 1" };

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand();
            cmd.Connection = conn;

            if (!string.IsNullOrWhiteSpace(nom))
            {
                whereConditions.Add("(c.Nom LIKE @Nom OR c.RaisonSociale LIKE @Nom )");
                cmd.Parameters.AddWithValue("@Nom", $"%{nom}%");
            }

            if (!string.IsNullOrWhiteSpace(typeClient))
            {
                whereConditions.Add("c.TypeClient = @TypeClient");
                cmd.Parameters.AddWithValue("@TypeClient", typeClient);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                whereConditions.Add("c.Status = @Status");
                cmd.Parameters.AddWithValue("@Status", status);
            }

            var whereClause = string.Join(" AND ", whereConditions);

            cmd.CommandText = $@"
                SELECT 
                    c.*,
                    COUNT(DISTINCT d.Id) as TotalDevis,
                    COUNT(DISTINCT f.Id) as TotalFactures,
                    COUNT(DISTINCT p.Id) as TotalProjets,
                    ISNULL(SUM(CASE WHEN f.Statut IN ('Payee', 'Envoyee', 'PartPayee') THEN f.MontantTTC ELSE 0 END), 0) as TotalRevenue
                FROM Clients c
                LEFT JOIN Devis d ON c.Id = d.ClientId
                LEFT JOIN Factures f ON c.Id = f.ClientId
                LEFT JOIN Projets p ON c.Id = p.ClientId
                WHERE {whereClause}
                GROUP BY c.Id, c.TypeClient, c.Nom, c.RaisonSociale, c.Email, 
                         c.Telephone, c.Adresse, c.Ville, c.Ncc, c.Status, 
                         c.DateCreation, c.DateModification, c.Actif, c.UtilisateurCreation
                ORDER BY c.DateCreation DESC";

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                clients.Add(MapToClientWithStatistique(reader));
            }

            return clients;
        }

        /// <summary>
        /// Ajoute les paramètres du client à la commande SQL
        /// </summary>
        private void AddClientParameters(SqlCommand cmd, Client client)
        {
            cmd.Parameters.AddWithValue("@TypeClient", client.TypeClient);
            cmd.Parameters.AddWithValue("@Nom", client.Nom);
            cmd.Parameters.AddWithValue("@RaisonSociale", client.RaisonSociale ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", client.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Telephone", client.Telephone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Adresse", client.Adresse ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ville", client.Ville ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ncc", client.Ncc ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", client.Status ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateCreation", client.DateCreation);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@Actif", client.Actif);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", client.UtilisateurCreation ?? (object)DBNull.Value);
        }

        /// <summary>
        /// Mappe les données vers ClientWithStatistique
        /// </summary>
        private ClientWithStatistique MapToClientWithStatistique(SqlDataReader reader)
        {
            return new ClientWithStatistique
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                TypeClient = reader.GetString(reader.GetOrdinal("TypeClient")),
                Nom = reader.GetString(reader.GetOrdinal("Nom")),
                RaisonSociale = reader.IsDBNull(reader.GetOrdinal("RaisonSociale"))
                    ? null : reader.GetString(reader.GetOrdinal("RaisonSociale")),
                Email = reader.IsDBNull(reader.GetOrdinal("Email"))
                    ? null : reader.GetString(reader.GetOrdinal("Email")),
                Telephone = reader.IsDBNull(reader.GetOrdinal("Telephone"))
                    ? null : reader.GetString(reader.GetOrdinal("Telephone")),
                Adresse = reader.IsDBNull(reader.GetOrdinal("Adresse"))
                    ? null : reader.GetString(reader.GetOrdinal("Adresse")),
                Ville = reader.IsDBNull(reader.GetOrdinal("Ville"))
                    ? null : reader.GetString(reader.GetOrdinal("Ville")),
                Ncc = reader.IsDBNull(reader.GetOrdinal("Ncc"))
                    ? null : reader.GetString(reader.GetOrdinal("Ncc")),
                Status = reader.IsDBNull(reader.GetOrdinal("Status"))
                    ? null : reader.GetString(reader.GetOrdinal("Status")),
                DateCreation = reader.GetDateTime(reader.GetOrdinal("DateCreation")),
                DateModification = reader.GetDateTime(reader.GetOrdinal("DateModification")),
                Actif = reader.GetBoolean(reader.GetOrdinal("Actif")),
                UtilisateurCreation = reader.IsDBNull(reader.GetOrdinal("UtilisateurCreation"))
                    ? null : reader.GetInt32(reader.GetOrdinal("UtilisateurCreation")),
                Statistique = new Statistique
                {
                    TotalDevis = reader.GetInt32(reader.GetOrdinal("TotalDevis")),
                    TotalFactures = reader.GetInt32(reader.GetOrdinal("TotalFactures")),
                    TotalProjets = reader.GetInt32(reader.GetOrdinal("TotalProjets")),
                    TotalRevenue = reader.GetDecimal(reader.GetOrdinal("TotalRevenue"))
                }
            };
        }
    }

    /// <summary>
    /// Classe pour retourner un client avec ses statistiques
    /// </summary>
}