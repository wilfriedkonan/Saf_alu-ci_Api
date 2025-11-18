using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.Utilisateurs;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.ObjectifFinancier
{
    public class ObjectifFinacierService
    {
        private readonly string _connectionString;

        public ObjectifFinacierService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<ObjectifFinancierModel>> GetAllAsync()
        {
            var list = new List<ObjectifFinancierModel>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
            SELECT o.*, u.Nom, u.Prenom
            FROM ObjectifFinancier o
            LEFT JOIN Utilisateurs u ON u.Id = o.UtilisateurCreation
            WHERE o.Actif = 1", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                list.Add(Map(reader));

            return list;
        }

        public async Task<ObjectifFinancierModel?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
            SELECT o.*, u.Nom, u.Prenom
            FROM ObjectifFinancier o
            LEFT JOIN Utilisateurs u ON u.Id = o.UtilisateurCreation
            WHERE o.Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            return await reader.ReadAsync() ? Map(reader) : null;
        }

        public async Task<int> CreateAsync(ObjectifFinancierModel o)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
            INSERT INTO ObjectifFinancier
                (Montant, Actif, Statut, DateFinPrevue, DateCreation, UtilisateurCreation)
            VALUES
                (@Montant, @Actif, @Statut, @DateFinPrevue, @DateCreation, @UtilisateurCreation);
            SELECT SCOPE_IDENTITY();", conn);

            cmd.Parameters.AddWithValue("@Montant", o.Montant);
            cmd.Parameters.AddWithValue("@Actif", o.Actif);
            cmd.Parameters.AddWithValue("@Statut", o.Statut);
            cmd.Parameters.AddWithValue("@DateFinPrevue", o.DateFinPrevue);
            cmd.Parameters.AddWithValue("@DateCreation", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", o.UtilisateurCreation);

            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateAsync(int id, UpdateObjectifRequest o)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
            UPDATE ObjectifFinancier SET
                Montant = @Montant,
                Actif = @Actif,
                Statut = @Statut,
                DateFinPrevue = @DateFinPrevue,
                DateModification = GETDATE(),
                UtilisateurModification = @UtilisateurModification
            WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Montant", o.Montant);
            cmd.Parameters.AddWithValue("@Actif", o.Actif);
            cmd.Parameters.AddWithValue("@Statut", o.Statut);
            cmd.Parameters.AddWithValue("@DateFinPrevue", o.DateFinPrevue);
            cmd.Parameters.AddWithValue("@UtilisateurModification", o.UtilisateurModification);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        private ObjectifFinancierModel Map(SqlDataReader reader)
        {
            return new ObjectifFinancierModel
            {
                Id = reader.GetInt32("Id"),
                Montant = reader.GetDecimal("Montant"),
                Actif = reader.GetBoolean("Actif"),
                Statut = reader.GetString("Statut"),
                DateFinPrevue = reader.IsDBNull("DateFinPrevue") ? null : reader.GetDateTime("DateFinPrevue"),
                DateCreation = reader.IsDBNull("DateCreation") ? null : reader.GetDateTime("DateCreation"),
                DateModification = reader.IsDBNull("DateModification") ? null : reader.GetDateTime("DateModification"),
                UtilisateurCreation = reader.GetInt32("UtilisateurCreation"),
                CreatedBy = new Utilisateur
                {
                    Nom = reader.IsDBNull("Nom") ? "" : reader.GetString("Nom"),
                    Prenom = reader.IsDBNull("Prenom") ? "" : reader.GetString("Prenom")
                }
            };
        }
    }
}
