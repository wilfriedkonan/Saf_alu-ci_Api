using Microsoft.Data.SqlClient;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.WhatsApp
{
    public class WhatsAppService
    {
        private readonly string _connectionString;

        public WhatsAppService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // =============================================
        // COMPTES
        // =============================================

        public async Task<List<WhatsAppCompte>> GetAllComptesAsync(string? service = null)
        {
            var comptes = new List<WhatsAppCompte>();

            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT * FROM WhatsAppComptes
                WHERE Actif = 1
                  AND (@Service IS NULL OR Service = @Service)
                ORDER BY NomAffichage";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Service", service ?? (object)DBNull.Value);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                comptes.Add(MapToCompte(reader));

            return comptes;
        }

        public async Task<WhatsAppCompte?> GetCompteByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT * FROM WhatsAppComptes WHERE Id = @Id AND Actif = 1", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            return await reader.ReadAsync() ? MapToCompte(reader) : null;
        }

        public async Task<int> CreateCompteAsync(WhatsAppCompte compte)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO WhatsAppComptes
                    (NomInstance, NomAffichage, NumeroTelephone, Description, Service,
                     Actif, Connecte, DateCreation, DateModification,
                     UtilisateurCreation, UtilisateurModification)
                VALUES
                    (@NomInstance, @NomAffichage, @NumeroTelephone, @Description, @Service,
                     1, 0, @DateCreation, @DateModification,
                     @UtilisateurCreation, @UtilisateurModification);
                SELECT CAST(SCOPE_IDENTITY() AS INT)", conn);

            AddCompteParameters(cmd, compte);

            await conn.OpenAsync();
            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task<bool> UpdateCompteAsync(int id, UpdateWhatsAppCompteRequest request, int utilisateurId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE WhatsAppComptes SET
                    NomAffichage          = @NomAffichage,
                    NumeroTelephone       = ISNULL(@NumeroTelephone, NumeroTelephone),
                    Description           = @Description,
                    Service               = @Service,
                    DateModification      = @DateModification,
                    UtilisateurModification = @UtilisateurModification
                WHERE Id = @Id AND Actif = 1", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@NomAffichage", request.NomAffichage);
            cmd.Parameters.AddWithValue("@NumeroTelephone", request.NumeroTelephone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Service", request.Service ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UtilisateurModification", utilisateurId);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> SetConnexionAsync(int id, bool connecte, int utilisateurId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE WhatsAppComptes SET
                    Connecte              = @Connecte,
                    DateConnexion         = @DateConnexion,
                    DateModification      = @DateModification,
                    UtilisateurModification = @UtilisateurModification
                WHERE Id = @Id AND Actif = 1", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Connecte", connecte);
            cmd.Parameters.AddWithValue("@DateConnexion", connecte ? DateTime.UtcNow : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UtilisateurModification", utilisateurId);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteCompteAsync(int id, int utilisateurId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE WhatsAppComptes SET
                    Actif                 = 0,
                    DateModification      = @DateModification,
                    UtilisateurModification = @UtilisateurModification
                WHERE Id = @Id AND Actif = 1", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UtilisateurModification", utilisateurId);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // =============================================
        // TYPES DE MESSAGES (lecture seule)
        // =============================================

        public async Task<List<WhatsAppMessageType>> GetAllTypesAsync()
        {
            var types = new List<WhatsAppMessageType>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT * FROM WhatsAppMessagesTypes WHERE Actif = 1 ORDER BY Libelle", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                types.Add(MapToType(reader));

            return types;
        }

        public async Task<WhatsAppMessageType?> GetTypeByCodeAsync(string code)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT * FROM WhatsAppMessagesTypes WHERE Code = @Code AND Actif = 1", conn);
            cmd.Parameters.AddWithValue("@Code", code);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            return await reader.ReadAsync() ? MapToType(reader) : null;
        }

        // =============================================
        // MESSAGES PRÉDÉFINIS
        // =============================================

        public async Task<List<WhatsAppMessagePredefini>> GetAllMessagesAsync(string? typeCode = null)
        {
            var messages = new List<WhatsAppMessagePredefini>();

            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT mp.*, mt.Code AS TypeCode, mt.Libelle AS TypeLibelle, mt.Description AS TypeDescription
                FROM WhatsAppMessagesPredéfinis mp
                INNER JOIN WhatsAppMessagesTypes mt ON mp.IdType = mt.Id
                WHERE mp.Actif = 1
                  AND (@TypeCode IS NULL OR mt.Code = @TypeCode)
                ORDER BY mt.Libelle, mp.Titre";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@TypeCode", typeCode ?? (object)DBNull.Value);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                messages.Add(MapToMessage(reader));

            return messages;
        }

        public async Task<WhatsAppMessagePredefini?> GetMessageByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT mp.*, mt.Code AS TypeCode, mt.Libelle AS TypeLibelle, mt.Description AS TypeDescription
                FROM WhatsAppMessagesPredéfinis mp
                INNER JOIN WhatsAppMessagesTypes mt ON mp.IdType = mt.Id
                WHERE mp.Id = @Id AND mp.Actif = 1";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            return await reader.ReadAsync() ? MapToMessage(reader) : null;
        }

        public async Task<List<WhatsAppMessagePredefini>> GetMessagesByTypeCodeAsync(string code)
        {
            var messages = new List<WhatsAppMessagePredefini>();

            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT mp.*, mt.Code AS TypeCode, mt.Libelle AS TypeLibelle, mt.Description AS TypeDescription
                FROM WhatsAppMessagesPredéfinis mp
                INNER JOIN WhatsAppMessagesTypes mt ON mp.IdType = mt.Id
                WHERE mt.Code = @Code AND mp.Actif = 1
                ORDER BY mp.Titre";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Code", code);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                messages.Add(MapToMessage(reader));

            return messages;
        }

        public async Task<int> CreateMessageAsync(WhatsAppMessagePredefini message)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO WhatsAppMessagesPredéfinis
                    (IdType, Titre, Contenu, Variables, Actif,
                     DateCreation, DateModification,
                     UtilisateurCreation, UtilisateurModification)
                VALUES
                    (@IdType, @Titre, @Contenu, @Variables, 1,
                     @DateCreation, @DateModification,
                     @UtilisateurCreation, @UtilisateurModification);
                SELECT CAST(SCOPE_IDENTITY() AS INT)", conn);

            AddMessageParameters(cmd, message);

            await conn.OpenAsync();
            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task<bool> UpdateMessageAsync(int id, UpdateWhatsAppMessagePredefiniRequest request, int utilisateurId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE WhatsAppMessagesPredéfinis SET
                    Titre                   = @Titre,
                    Contenu                 = @Contenu,
                    Variables               = @Variables,
                    Actif                   = @Actif,
                    DateModification        = @DateModification,
                    UtilisateurModification = @UtilisateurModification
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Titre", request.Titre);
            cmd.Parameters.AddWithValue("@Contenu", request.Contenu);
            cmd.Parameters.AddWithValue("@Variables", request.Variables ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Actif", request.Actif);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UtilisateurModification", utilisateurId);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteMessageAsync(int id, int utilisateurId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE WhatsAppMessagesPredéfinis SET
                    Actif                   = 0,
                    DateModification        = @DateModification,
                    UtilisateurModification = @UtilisateurModification
                WHERE Id = @Id AND Actif = 1", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UtilisateurModification", utilisateurId);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // =============================================
        // PRÉVISUALISATION
        // =============================================

        /// <summary>
        /// Substitue les {VARIABLES} du contenu d'un message par les valeurs fournies.
        /// Retourne le contenu résolu et la liste des variables non renseignées.
        /// </summary>
        public async Task<(string ContenuResolu, List<string> VariablesManquantes)>
            PrevisualiserMessageAsync(int messageId, Dictionary<string, string> valeurs)
        {
            var message = await GetMessageByIdAsync(messageId);
            if (message == null)
                throw new KeyNotFoundException($"Message prédéfini introuvable (Id={messageId})");

            var contenu = message.Contenu;
            var manquantes = new List<string>();

            // Extraire les variables déclarées sur le message
            var variablesDeclares = (message.Variables ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim().Trim('{', '}'))
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();

            // Substituer les variables fournies
            foreach (var (cle, valeur) in valeurs)
            {
                contenu = contenu.Replace($"{{{cle}}}", valeur, StringComparison.OrdinalIgnoreCase);
            }

            // Détecter les variables non renseignées
            foreach (var variable in variablesDeclares)
            {
                if (!valeurs.ContainsKey(variable) ||
                    string.IsNullOrWhiteSpace(valeurs[variable]))
                {
                    manquantes.Add(variable);
                }
            }

            return (contenu, manquantes);
        }

        // =============================================
        // HELPERS PRIVÉS
        // =============================================

        private void AddCompteParameters(SqlCommand cmd, WhatsAppCompte c)
        {
            cmd.Parameters.AddWithValue("@NomInstance", c.NomInstance);
            cmd.Parameters.AddWithValue("@NomAffichage", c.NomAffichage);
            cmd.Parameters.AddWithValue("@NumeroTelephone", c.NumeroTelephone);
            cmd.Parameters.AddWithValue("@Description", c.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Service", c.Service ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateCreation", c.DateCreation);
            cmd.Parameters.AddWithValue("@DateModification", c.DateModification);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", c.UtilisateurCreation);
            cmd.Parameters.AddWithValue("@UtilisateurModification", c.UtilisateurModification);
        }

        private void AddMessageParameters(SqlCommand cmd, WhatsAppMessagePredefini m)
        {
            cmd.Parameters.AddWithValue("@IdType", m.IdType);
            cmd.Parameters.AddWithValue("@Titre", m.Titre);
            cmd.Parameters.AddWithValue("@Contenu", m.Contenu);
            cmd.Parameters.AddWithValue("@Variables", m.Variables ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateCreation", m.DateCreation);
            cmd.Parameters.AddWithValue("@DateModification", m.DateModification);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", m.UtilisateurCreation);
            cmd.Parameters.AddWithValue("@UtilisateurModification", m.UtilisateurModification);
        }

        private static WhatsAppCompte MapToCompte(SqlDataReader r) => new()
        {
            Id = r.GetInt32("Id"),
            NomInstance = r.GetString("NomInstance"),
            NomAffichage = r.GetString("NomAffichage"),
            NumeroTelephone = r.GetString("NumeroTelephone"),
            Description = r.IsDBNull("Description") ? null : r.GetString("Description"),
            Service = r.IsDBNull("Service") ? null : r.GetString("Service"),
            Actif = r.GetBoolean("Actif"),
            Connecte = r.GetBoolean("Connecte"),
            DateConnexion = r.IsDBNull("DateConnexion") ? null : r.GetDateTime("DateConnexion"),
            DateCreation = r.GetDateTime("DateCreation"),
            DateModification = r.GetDateTime("DateModification"),
            UtilisateurCreation = r.GetInt32("UtilisateurCreation"),
            UtilisateurModification = r.GetInt32("UtilisateurModification"),
        };

        private static WhatsAppMessageType MapToType(SqlDataReader r) => new()
        {
            Id = r.GetInt32("Id"),
            Code = r.GetString("Code"),
            Libelle = r.GetString("Libelle"),
            Description = r.IsDBNull("Description") ? null : r.GetString("Description"),
            Actif = r.GetBoolean("Actif"),
        };

        private static WhatsAppMessagePredefini MapToMessage(SqlDataReader r) => new()
        {
            Id = r.GetInt32("Id"),
            IdType = r.GetInt32("IdType"),
            Titre = r.GetString("Titre"),
            Contenu = r.GetString("Contenu"),
            Variables = r.IsDBNull("Variables") ? null : r.GetString("Variables"),
            Actif = r.GetBoolean("Actif"),
            DateCreation = r.GetDateTime("DateCreation"),
            DateModification = r.GetDateTime("DateModification"),
            UtilisateurCreation = r.GetInt32("UtilisateurCreation"),
            UtilisateurModification = r.GetInt32("UtilisateurModification"),
            Type = new WhatsAppMessageType
            {
                Code = r.GetString("TypeCode"),
                Libelle = r.GetString("TypeLibelle"),
                Description = r.IsDBNull("TypeDescription") ? null : r.GetString("TypeDescription"),
                Actif = true,
            }
        };
    }
}