using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Utilisateurs;
using System.Data;
using System.Text.Json;

namespace Saf_alu_ci_Api.Controllers.Parametres
{
    public class ParametresService
    {
        private readonly string _connectionString;

        public ParametresService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // =============================================
        // SECTION: GESTION DES UTILISATEURS
        // =============================================

        public async Task<List<UtilisateurResponse>> GetAllUtilisateursAsync()
        {
            var utilisateurs = new List<UtilisateurResponse>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT 
                        u.Id, u.Email, u.Username, u.Prenom, u.Nom, u.Telephone,
                        u.RoleId, u.Photo, u.DerniereConnexion, u.DateCreation, u.Actif,
                        r.Nom as RoleName, r.Description as RoleDescription
                    FROM Utilisateurs u
                    LEFT JOIN Roles r ON u.RoleId = r.Id
                    ORDER BY u.DateCreation DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        utilisateurs.Add(MapUtilisateurFromReader(reader));
                    }
                }
            }

            return utilisateurs;
        }

        public async Task<UtilisateurResponse> GetUtilisateurByIdAsync(int id)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT 
                        u.Id, u.Email, u.Username, u.Prenom, u.Nom, u.Telephone,
                        u.RoleId, u.Photo, u.DerniereConnexion, u.DateCreation, u.Actif,
                        r.Nom as RoleName, r.Description as RoleDescription
                    FROM Utilisateurs u
                    LEFT JOIN Roles r ON u.RoleId = r.Id
                    WHERE u.Id = @Id";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapUtilisateurFromReader(reader);
                        }
                    }
                }
            }
            return null;
        }

        public async Task<SearchUtilisateursResponse> SearchUtilisateursAsync(SearchUtilisateursRequest request)
        {
            var response = new SearchUtilisateursResponse
            {
                Utilisateurs = new List<UtilisateurResponse>(),
                CurrentPage = request.PageNumber
            };

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("sp_RechercherUtilisateurs", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SearchTerm", request.SearchTerm ?? "");
                    cmd.Parameters.AddWithValue("@RoleFilter", request.RoleFilter ?? "");
                    cmd.Parameters.AddWithValue("@StatusFilter", (object)request.StatusFilter ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PageNumber", request.PageNumber);
                    cmd.Parameters.AddWithValue("@PageSize", request.PageSize);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            response.Utilisateurs.Add(MapUtilisateurFromReader(reader));
                        }

                        if (await reader.NextResultAsync() && await reader.ReadAsync())
                        {
                            response.TotalRecords = reader.GetInt32(0);
                            response.TotalPages = (int)Math.Ceiling((double)response.TotalRecords / request.PageSize);
                        }
                    }
                }
            }
            return response;
        }

        public async Task<UtilisateurResponse> CreateUtilisateurAsync(CreateUtilisateurRequest request)
        {
            if (await CheckEmailExistsAsync(request.Email))
                throw new Exception("Un utilisateur avec cet email existe déjà");

            if (await CheckUsernameExistsAsync(request.Username))
                throw new Exception("Un utilisateur avec ce nom d'utilisateur existe déjà");

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.MotDePasse);

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        int newId;
                        string getMaxIdQuery = "SELECT ISNULL(MAX(Id), 0) + 1 FROM Utilisateurs";
                        using (SqlCommand cmd = new SqlCommand(getMaxIdQuery, conn, transaction))
                        {
                            newId = (int)await cmd.ExecuteScalarAsync();
                        }

                        string insertQuery = @"
                            INSERT INTO Utilisateurs (Id, Email, Username, MotDePasseHash, Prenom, Nom, Telephone, RoleId, Photo, DateCreation, DateModification, Actif)
                            VALUES (@Id, @Email, @Username, @MotDePasseHash, @Prenom, @Nom, @Telephone, @RoleId, @Photo, GETUTCDATE(), GETUTCDATE(), @Actif)";

                        using (SqlCommand cmd = new SqlCommand(insertQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", newId);
                            cmd.Parameters.AddWithValue("@Email", request.Email);
                            cmd.Parameters.AddWithValue("@Username", request.Username);
                            cmd.Parameters.AddWithValue("@MotDePasseHash", passwordHash);
                            cmd.Parameters.AddWithValue("@Prenom", request.Prenom);
                            cmd.Parameters.AddWithValue("@Nom", request.Nom);
                            cmd.Parameters.AddWithValue("@Telephone", (object)request.Telephone ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@RoleId", request.RoleId);
                            cmd.Parameters.AddWithValue("@Photo", (object)request.Photo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Actif", request.Actif);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return await GetUtilisateurByIdAsync(newId);
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<UtilisateurResponse> UpdateUtilisateurAsync(UpdateUtilisateurRequest request)
        {
            if (await CheckEmailExistsForOtherUserAsync(request.Email, request.Id))
                throw new Exception("Un autre utilisateur avec cet email existe déjà");

            if (await CheckUsernameExistsForOtherUserAsync(request.Username, request.Id))
                throw new Exception("Un autre utilisateur avec ce nom d'utilisateur existe déjà");

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string updateQuery = @"
                    UPDATE Utilisateurs
                    SET Email = @Email, Username = @Username, Prenom = @Prenom, Nom = @Nom,
                        Telephone = @Telephone, RoleId = @RoleId, Photo = @Photo, Actif = @Actif,
                        DateModification = GETUTCDATE()
                    WHERE Id = @Id";

                using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", request.Id);
                    cmd.Parameters.AddWithValue("@Email", request.Email);
                    cmd.Parameters.AddWithValue("@Username", request.Username);
                    cmd.Parameters.AddWithValue("@Prenom", request.Prenom);
                    cmd.Parameters.AddWithValue("@Nom", request.Nom);
                    cmd.Parameters.AddWithValue("@Telephone", (object)request.Telephone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RoleId", request.RoleId);
                    cmd.Parameters.AddWithValue("@Photo", (object)request.Photo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Actif", request.Actif);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                        throw new Exception("Utilisateur non trouvé");
                }
            }
            return await GetUtilisateurByIdAsync(request.Id);
        }

        public async Task<bool> DeleteUtilisateurAsync(int id)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string deleteQuery = "DELETE FROM Utilisateurs WHERE Id = @Id";
                using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> ChangePasswordAsync(ChangePasswordRequest request)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string getPasswordQuery = "SELECT MotDePasseHash FROM Utilisateurs WHERE Id = @Id";
                string currentHash;

                using (SqlCommand cmd = new SqlCommand(getPasswordQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", request.UtilisateurId);
                    currentHash = (string)await cmd.ExecuteScalarAsync();
                }

                if (currentHash == null)
                    throw new Exception("Utilisateur non trouvé");

                if (!BCrypt.Net.BCrypt.Verify(request.AncienMotDePasse, currentHash))
                    throw new Exception("L'ancien mot de passe est incorrect");

                string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NouveauMotDePasse);
                string updateQuery = "UPDATE Utilisateurs SET MotDePasseHash = @NewHash, DateModification = GETUTCDATE() WHERE Id = @Id";

                using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", request.UtilisateurId);
                    cmd.Parameters.AddWithValue("@NewHash", newPasswordHash);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<StatistiquesUtilisateursResponse> GetStatistiquesUtilisateursAsync()
        {
            var stats = new StatistiquesUtilisateursResponse { ParRole = new List<StatistiqueParRole>() };

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string globalStatsQuery = @"
                    SELECT 
                        COUNT(*) as TotalUtilisateurs,
                        SUM(CASE WHEN Actif = 1 THEN 1 ELSE 0 END) as UtilisateursActifs,
                        SUM(CASE WHEN Actif = 0 THEN 1 ELSE 0 END) as UtilisateursInactifs,
                        SUM(CASE WHEN DerniereConnexion > DATEADD(day, -30, GETUTCDATE()) THEN 1 ELSE 0 END) as ConnexionsRecentes
                    FROM Utilisateurs";

                using (SqlCommand cmd = new SqlCommand(globalStatsQuery, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        stats.TotalUtilisateurs = reader.GetInt32(0);
                        stats.UtilisateursActifs = reader.GetInt32(1);
                        stats.UtilisateursInactifs = reader.GetInt32(2);
                        stats.ConnexionsRecentes = reader.GetInt32(3);
                    }
                }

                string roleStatsQuery = @"
                    SELECT r.Nom, r.Description, COUNT(u.Id) as Nombre,
                           SUM(CASE WHEN u.Actif = 1 THEN 1 ELSE 0 END) as Actifs,
                           SUM(CASE WHEN u.DerniereConnexion > DATEADD(day, -30, GETUTCDATE()) THEN 1 ELSE 0 END) as Recents
                    FROM Roles r
                    LEFT JOIN Utilisateurs u ON r.Id = u.RoleId
                    WHERE r.Actif = 1
                    GROUP BY r.Id, r.Nom, r.Description
                    ORDER BY r.Nom";

                using (SqlCommand cmd = new SqlCommand(roleStatsQuery, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        stats.ParRole.Add(new StatistiqueParRole
                        {
                            RoleName = reader.GetString(0),
                            RoleDescription = reader.IsDBNull(1) ? null : reader.GetString(1),
                            NombreUtilisateurs = reader.GetInt32(2),
                            UtilisateursActifs = reader.GetInt32(3),
                            ConnexionsRecentes = reader.GetInt32(4)
                        });
                    }
                }
            }
            return stats;
        }

        // =============================================
        // SECTION: GESTION DES RÔLES
        // =============================================

        public async Task<List<RoleResponse>> GetAllRolesAsync()
        {
            var roles = new List<RoleResponse>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT r.Id, r.Nom, r.Description, r.Permissions, r.DateCreation, r.Actif, COUNT(u.Id) as NombreUtilisateurs
                    FROM Roles r
                    LEFT JOIN Utilisateurs u ON r.Id = u.RoleId
                    GROUP BY r.Id, r.Nom, r.Description, r.Permissions, r.DateCreation, r.Actif
                    ORDER BY r.Nom";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        roles.Add(MapRoleFromReader(reader));
                    }
                }
            }
            return roles;
        }

        public async Task<RoleResponse> GetRoleByIdAsync(int id)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT r.Id, r.Nom, r.Description, r.Permissions, r.DateCreation, r.Actif, COUNT(u.Id) as NombreUtilisateurs
                    FROM Roles r
                    LEFT JOIN Utilisateurs u ON r.Id = u.RoleId
                    WHERE r.Id = @Id
                    GROUP BY r.Id, r.Nom, r.Description, r.Permissions, r.DateCreation, r.Actif";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapRoleFromReader(reader);
                        }
                    }
                }
            }
            return null;
        }

        public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request)
        {
            if (await CheckRoleNameExistsAsync(request.Nom))
                throw new Exception("Un rôle avec ce nom existe déjà");

            string permissionsJson = request.Permissions != null ? JsonSerializer.Serialize(request.Permissions) : null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string insertQuery = @"
                    INSERT INTO Roles (Nom, Description, Permissions, DateCreation, Actif)
                    OUTPUT INSERTED.Id
                    VALUES (@Nom, @Description, @Permissions, GETUTCDATE(), @Actif)";

                using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Nom", request.Nom);
                    cmd.Parameters.AddWithValue("@Description", (object)request.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Permissions", (object)permissionsJson ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Actif", request.Actif);
                    int newId = (int)await cmd.ExecuteScalarAsync();
                    return await GetRoleByIdAsync(newId);
                }
            }
        }

        public async Task<RoleResponse> UpdateRoleAsync(UpdateRoleRequest request)
        {
            if (await CheckRoleNameExistsForOtherAsync(request.Nom, request.Id))
                throw new Exception("Un autre rôle avec ce nom existe déjà");

            string permissionsJson = request.Permissions != null ? JsonSerializer.Serialize(request.Permissions) : null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string updateQuery = "UPDATE Roles SET Nom = @Nom, Description = @Description, Permissions = @Permissions, Actif = @Actif WHERE Id = @Id";

                using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", request.Id);
                    cmd.Parameters.AddWithValue("@Nom", request.Nom);
                    cmd.Parameters.AddWithValue("@Description", (object)request.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Permissions", (object)permissionsJson ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Actif", request.Actif);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                        throw new Exception("Rôle non trouvé");
                }
            }
            return await GetRoleByIdAsync(request.Id);
        }

        public async Task<bool> DeleteRoleAsync(int id)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string checkQuery = "SELECT COUNT(*) FROM Utilisateurs WHERE RoleId = @Id";
                using (SqlCommand cmd = new SqlCommand(checkQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    int count = (int)await cmd.ExecuteScalarAsync();
                    if (count > 0)
                        throw new Exception("Ce rôle ne peut pas être supprimé car il est utilisé par des utilisateurs");
                }

                string deleteQuery = "DELETE FROM Roles WHERE Id = @Id";
                using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
        }

        // =============================================
        // SECTION: GESTION DES PARAMÈTRES
        // =============================================

        public async Task<List<ParametreResponse>> GetAllParametresAsync()
        {
            var parametres = new List<ParametreResponse>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT Id, Cle, Valeur, TypeValeur, Description, Categorie, DateModification, UtilisateurModification FROM Parametres ORDER BY Categorie, Cle";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        parametres.Add(MapParametreFromReader(reader));
                    }
                }
            }
            return parametres;
        }

        public async Task<List<ParametresByCategorieResponse>> GetParametresByCategorieAsync()
        {
            var parametres = await GetAllParametresAsync();
            return parametres
                .GroupBy(p => p.Categorie ?? "Général")
                .Select(g => new ParametresByCategorieResponse
                {
                    Categorie = g.Key,
                    Parametres = g.ToList()
                })
                .OrderBy(x => x.Categorie)
                .ToList();
        }

        public async Task<ParametreResponse> GetParametreByKeyAsync(string cle)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT Id, Cle, Valeur, TypeValeur, Description, Categorie, DateModification, UtilisateurModification FROM Parametres WHERE Cle = @Cle";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Cle", cle);
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapParametreFromReader(reader);
                        }
                    }
                }
            }
            return null;
        }

        public async Task<ParametreResponse> UpdateParametreAsync(UpdateParametreRequest request, int utilisateurId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("sp_UpdateParametre", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Cle", request.Cle);
                    cmd.Parameters.AddWithValue("@Valeur", request.Valeur);
                    cmd.Parameters.AddWithValue("@UtilisateurId", utilisateurId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapParametreFromReader(reader);
                        }
                    }
                }
            }
            return null;
        }

        // =============================================
        // MÉTHODES PRIVÉES - HELPERS
        // =============================================

        private UtilisateurResponse MapUtilisateurFromReader(SqlDataReader reader)
        {
            return new UtilisateurResponse
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                Username = reader.GetString(reader.GetOrdinal("Username")),
                Prenom = reader.GetString(reader.GetOrdinal("Prenom")),
                Nom = reader.GetString(reader.GetOrdinal("Nom")),
                NomComplet = $"{reader.GetString(reader.GetOrdinal("Prenom"))} {reader.GetString(reader.GetOrdinal("Nom"))}",
                Telephone = reader.IsDBNull(reader.GetOrdinal("Telephone")) ? null : reader.GetString(reader.GetOrdinal("Telephone")),
                RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                RoleName = reader.IsDBNull(reader.GetOrdinal("RoleName")) ? null : reader.GetString(reader.GetOrdinal("RoleName")),
                RoleDescription = reader.IsDBNull(reader.GetOrdinal("RoleDescription")) ? null : reader.GetString(reader.GetOrdinal("RoleDescription")),
                Photo = reader.IsDBNull(reader.GetOrdinal("Photo")) ? null : reader.GetString(reader.GetOrdinal("Photo")),
                DerniereConnexion = reader.IsDBNull(reader.GetOrdinal("DerniereConnexion")) ? null : reader.GetDateTime(reader.GetOrdinal("DerniereConnexion")),
                DateCreation = reader.IsDBNull(reader.GetOrdinal("DateCreation")) ? null : reader.GetDateTime(reader.GetOrdinal("DateCreation")),
                Actif = reader.GetBoolean(reader.GetOrdinal("Actif"))
            };
        }

        private RoleResponse MapRoleFromReader(SqlDataReader reader)
        {
            string permissionsJson = reader.IsDBNull(reader.GetOrdinal("Permissions")) ? null : reader.GetString(reader.GetOrdinal("Permissions"));
            List<string> permissions = null;

            if (!string.IsNullOrEmpty(permissionsJson))
            {
                try
                {
                    permissions = JsonSerializer.Deserialize<List<string>>(permissionsJson);
                }
                catch
                {
                    permissions = new List<string>();
                }
            }

            return new RoleResponse
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Nom = reader.GetString(reader.GetOrdinal("Nom")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                Permissions = permissions,
                DateCreation = reader.IsDBNull(reader.GetOrdinal("DateCreation")) ? null : reader.GetDateTime(reader.GetOrdinal("DateCreation")),
                Actif = reader.GetBoolean(reader.GetOrdinal("Actif")),
                NombreUtilisateurs = reader.GetInt32(reader.GetOrdinal("NombreUtilisateurs"))
            };
        }

        private ParametreResponse MapParametreFromReader(SqlDataReader reader)
        {
            return new ParametreResponse
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Cle = reader.GetString(reader.GetOrdinal("Cle")),
                Valeur = reader.IsDBNull(reader.GetOrdinal("Valeur")) ? null : reader.GetString(reader.GetOrdinal("Valeur")),
                TypeValeur = reader.IsDBNull(reader.GetOrdinal("TypeValeur")) ? null : reader.GetString(reader.GetOrdinal("TypeValeur")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                Categorie = reader.IsDBNull(reader.GetOrdinal("Categorie")) ? null : reader.GetString(reader.GetOrdinal("Categorie")),
                DateModification = reader.IsDBNull(reader.GetOrdinal("DateModification")) ? null : reader.GetDateTime(reader.GetOrdinal("DateModification")),
                UtilisateurModification = reader.IsDBNull(reader.GetOrdinal("UtilisateurModification")) ? null : reader.GetInt32(reader.GetOrdinal("UtilisateurModification"))
            };
        }

        private async Task<bool> CheckEmailExistsAsync(string email)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Utilisateurs WHERE Email = @Email", conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }
        }

        private async Task<bool> CheckUsernameExistsAsync(string username)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Utilisateurs WHERE Username = @Username", conn))
                {
                    cmd.Parameters.AddWithValue("@Username", username);
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }
        }

        private async Task<bool> CheckEmailExistsForOtherUserAsync(string email, int userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Utilisateurs WHERE Email = @Email AND Id != @UserId", conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }
        }

        private async Task<bool> CheckUsernameExistsForOtherUserAsync(string username, int userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Utilisateurs WHERE Username = @Username AND Id != @UserId", conn))
                {
                    cmd.Parameters.AddWithValue("@Username", username);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }
        }

        private async Task<bool> CheckRoleNameExistsAsync(string nom)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Roles WHERE Nom = @Nom", conn))
                {
                    cmd.Parameters.AddWithValue("@Nom", nom);
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }
        }

        private async Task<bool> CheckRoleNameExistsForOtherAsync(string nom, int roleId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Roles WHERE Nom = @Nom AND Id != @RoleId", conn))
                {
                    cmd.Parameters.AddWithValue("@Nom", nom);
                    cmd.Parameters.AddWithValue("@RoleId", roleId);
                    return (int)await cmd.ExecuteScalarAsync() > 0;
                }
            }
        }
    }
}

