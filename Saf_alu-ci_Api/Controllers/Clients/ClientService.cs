using Microsoft.Data.SqlClient;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.Clients
{
    public class ClientService
    {
        private readonly string _connectionString;

        public ClientService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<Client>> GetAllAsync()
        {
            var clients = new List<Client>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM Clients WHERE Actif = 1 ORDER BY DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                clients.Add(MapToClient(reader));
            }

            return clients;
        }

        public async Task<Client?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM Clients WHERE Id = @Id AND Actif = 1", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return MapToClient(reader);
            }

            return null;
        }

        public async Task<int> CreateAsync(Client client)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO Clients (TypeClient, Nom, Prenom, RaisonSociale, Email, Telephone, TelephoneMobile, 
                                   Adresse, CodePostal, Ville, Pays, Siret, NumeroTVA, DateCreation, DateModification, 
                                   Actif, UtilisateurCreation)
                VALUES (@TypeClient, @Nom, @Prenom, @RaisonSociale, @Email, @Telephone, @TelephoneMobile, 
                       @Adresse, @CodePostal, @Ville, @Pays, @Siret, @NumeroTVA, @DateCreation, @DateModification, 
                       @Actif, @UtilisateurCreation);
                SELECT CAST(SCOPE_IDENTITY() as int)", conn);

            AddClientParameters(cmd, client);
            await conn.OpenAsync();
            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task UpdateAsync(Client client)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Clients SET 
                    TypeClient = @TypeClient, Nom = @Nom, Prenom = @Prenom, RaisonSociale = @RaisonSociale,
                    Email = @Email, Telephone = @Telephone, TelephoneMobile = @TelephoneMobile,
                    Adresse = @Adresse, CodePostal = @CodePostal, Ville = @Ville, Pays = @Pays,
                    Siret = @Siret, NumeroTVA = @NumeroTVA, DateModification = @DateModification
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", client.Id);
            AddClientParameters(cmd, client);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("UPDATE Clients SET Actif = 0, DateModification = @DateModification WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        private void AddClientParameters(SqlCommand cmd, Client client)
        {
            cmd.Parameters.AddWithValue("@TypeClient", client.TypeClient);
            cmd.Parameters.AddWithValue("@Nom", client.Nom);
            cmd.Parameters.AddWithValue("@Prenom", client.Prenom ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RaisonSociale", client.RaisonSociale ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", client.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Telephone", client.Telephone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TelephoneMobile", client.TelephoneMobile ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Adresse", client.Adresse ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CodePostal", client.CodePostal ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ville", client.Ville ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Pays", client.Pays);
            cmd.Parameters.AddWithValue("@Siret", client.Siret ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NumeroTVA", client.NumeroTVA ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateCreation", client.DateCreation);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@Actif", client.Actif);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", client.UtilisateurCreation ?? (object)DBNull.Value);
        }

        private Client MapToClient(SqlDataReader reader)
        {
            return new Client
            {
                Id = reader.GetInt32("Id"),
                TypeClient = reader.GetString("TypeClient"),
                Nom = reader.GetString("Nom"),
                Prenom = reader.IsDBNull("Prenom") ? null : reader.GetString("Prenom"),
                RaisonSociale = reader.IsDBNull("RaisonSociale") ? null : reader.GetString("RaisonSociale"),
                Email = reader.IsDBNull("Email") ? null : reader.GetString("Email"),
                Telephone = reader.IsDBNull("Telephone") ? null : reader.GetString("Telephone"),
                TelephoneMobile = reader.IsDBNull("TelephoneMobile") ? null : reader.GetString("TelephoneMobile"),
                Adresse = reader.IsDBNull("Adresse") ? null : reader.GetString("Adresse"),
                CodePostal = reader.IsDBNull("CodePostal") ? null : reader.GetString("CodePostal"),
                Ville = reader.IsDBNull("Ville") ? null : reader.GetString("Ville"),
                Pays = reader.GetString("Pays"),
                Siret = reader.IsDBNull("Siret") ? null : reader.GetString("Siret"),
                NumeroTVA = reader.IsDBNull("NumeroTVA") ? null : reader.GetString("NumeroTVA"),
                DateCreation = reader.GetDateTime("DateCreation"),
                DateModification = reader.GetDateTime("DateModification"),
                Actif = reader.GetBoolean("Actif"),
                UtilisateurCreation = reader.IsDBNull("UtilisateurCreation") ? null : reader.GetInt32("UtilisateurCreation")
            };
        }
    }
}