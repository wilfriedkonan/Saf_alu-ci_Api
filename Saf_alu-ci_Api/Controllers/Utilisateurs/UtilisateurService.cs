using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace Saf_alu_ci_Api.Controllers.Utilisateurs
{
    public class UtilisateurService
    {
        private readonly string _connectionString;

        public UtilisateurService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<Utilisateur>> GetAllAsync()
        {
            var users = new List<Utilisateur>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT u.*, r.Nom as RoleNom, r.Description as RoleDescription, r.Permissions 
                FROM Utilisateurs u 
                LEFT JOIN Roles r ON u.RoleId = r.Id 
                WHERE u.Actif = 1 
                ORDER BY u.DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(MapToUtilisateur(reader));
            }

            return users;
        }

        public async Task<List<Utilisateur>> GetAllIncludingInactiveAsync()
        {
            var users = new List<Utilisateur>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT u.*, r.Nom as RoleNom, r.Description as RoleDescription, r.Permissions 
                FROM Utilisateurs u 
                LEFT JOIN Roles r ON u.RoleId = r.Id 
                ORDER BY u.DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(MapToUtilisateur(reader));
            }

            return users;
        }

        public async Task<Utilisateur?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT u.*, r.Nom as RoleNom, r.Description as RoleDescription, r.Permissions 
                FROM Utilisateurs u 
                LEFT JOIN Roles r ON u.RoleId = r.Id 
                WHERE u.Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapToUtilisateur(reader);
            }

            return null;
        }

        public async Task<Utilisateur?> GetByEmailAsync(string email)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT u.*, r.Nom as RoleNom, r.Description as RoleDescription, r.Permissions 
                FROM Utilisateurs u 
                LEFT JOIN Roles r ON u.RoleId = r.Id 
                WHERE u.Email = @Email AND u.Actif = 1", conn);

            cmd.Parameters.AddWithValue("@Email", email);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapToUtilisateur(reader);
            }

            return null;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM Utilisateurs WHERE Email = @Email", conn);
            cmd.Parameters.AddWithValue("@Email", email);

            await conn.OpenAsync();
            var count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM Utilisateurs WHERE Username = @Username", conn);
            cmd.Parameters.AddWithValue("@Username", username);

            await conn.OpenAsync();
            var count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        public async Task<int> CreateAsync(Utilisateur user)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO Utilisateurs (Email, Username, MotDePasseHash, Prenom, Nom, Telephone, RoleId, Photo, DateCreation, DateModification, Actif)
                VALUES (@Email, @Username, @MotDePasseHash, @Prenom, @Nom, @Telephone, @RoleId, @Photo, @DateCreation, @DateModification, @Actif);
                SELECT CAST(SCOPE_IDENTITY() as int)", conn);

            cmd.Parameters.AddWithValue("@Email", user.Email);
            cmd.Parameters.AddWithValue("@Username", user.Username);
            cmd.Parameters.AddWithValue("@MotDePasseHash", user.MotDePasseHash);
            cmd.Parameters.AddWithValue("@Prenom", user.Prenom);
            cmd.Parameters.AddWithValue("@Nom", user.Nom);
            cmd.Parameters.AddWithValue("@Telephone", user.Telephone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RoleId", user.RoleId);
            cmd.Parameters.AddWithValue("@Photo", user.Photo ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateCreation", user.DateCreation);
            cmd.Parameters.AddWithValue("@DateModification", user.DateModification);
            cmd.Parameters.AddWithValue("@Actif", user.Actif);

            await conn.OpenAsync();
            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task UpdateAsync(Utilisateur user)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Utilisateurs SET 
                    Email = @Email, 
                    Username = @Username, 
                    Prenom = @Prenom, 
                    Nom = @Nom, 
                    Telephone = @Telephone, 
                    RoleId = @RoleId, 
                    Photo = @Photo, 
                    DateModification = @DateModification,
                    Actif = @Actif
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", user.Id);
            cmd.Parameters.AddWithValue("@Email", user.Email);
            cmd.Parameters.AddWithValue("@Username", user.Username);
            cmd.Parameters.AddWithValue("@Prenom", user.Prenom);
            cmd.Parameters.AddWithValue("@Nom", user.Nom);
            cmd.Parameters.AddWithValue("@Telephone", user.Telephone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RoleId", user.RoleId);
            cmd.Parameters.AddWithValue("@Photo", user.Photo ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateModification", user.DateModification);
            cmd.Parameters.AddWithValue("@Actif", user.Actif);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdatePasswordAsync(int userId, string hashedPassword)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Utilisateurs SET 
                    MotDePasseHash = @MotDePasseHash, 
                    DateModification = @DateModification 
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", userId);
            cmd.Parameters.AddWithValue("@MotDePasseHash", hashedPassword);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Utilisateurs SET 
                    Actif = 0, 
                    DateModification = @DateModification 
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ToggleStatusAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Utilisateurs SET 
                    Actif = CASE WHEN Actif = 1 THEN 0 ELSE 1 END, 
                    DateModification = @DateModification 
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateDerniereConnexionAsync(int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Utilisateurs SET 
                    DerniereConnexion = @DerniereConnexion 
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", userId);
            cmd.Parameters.AddWithValue("@DerniereConnexion", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // Méthodes pour la gestion des rôles
        public async Task<List<Role>> GetAllRolesAsync()
        {
            var roles = new List<Role>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT Id, Nom, Description, Permissions, DateCreation, Actif 
                FROM Roles 
                WHERE Actif = 1 
                ORDER BY Id", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var role = new Role
                {
                    Id = reader.GetInt32("Id"),
                    Nom = reader.GetString("Nom"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Permissions = reader.IsDBNull("Permissions") ? null : reader.GetString("Permissions"),
                    DateCreation = reader.GetDateTime("DateCreation"),
                    Actif = reader.GetBoolean("Actif")
                };

                // Si les permissions ne sont pas stockées en base, utiliser les permissions par défaut
                if (string.IsNullOrEmpty(role.Permissions))
                {
                    var defaultPermissions = RolePermissions.GetPermissions(role.Nom);
                    role.SetPermissionsList(defaultPermissions);
                }

                roles.Add(role);
            }

            return roles;
        }

        public async Task<Role?> GetRoleByIdAsync(int roleId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT Id, Nom, Description, Permissions, DateCreation, Actif 
                FROM Roles 
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", roleId);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var role = new Role
                {
                    Id = reader.GetInt32("Id"),
                    Nom = reader.GetString("Nom"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Permissions = reader.IsDBNull("Permissions") ? null : reader.GetString("Permissions"),
                    DateCreation = reader.GetDateTime("DateCreation"),
                    Actif = reader.GetBoolean("Actif")
                };

                // Si les permissions ne sont pas stockées en base, utiliser les permissions par défaut
                if (string.IsNullOrEmpty(role.Permissions))
                {
                    var defaultPermissions = RolePermissions.GetPermissions(role.Nom);
                    role.SetPermissionsList(defaultPermissions);
                }

                return role;
            }

            return null;
        }

        public async Task<Role?> GetRoleByNameAsync(string roleName)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT Id, Nom, Description, Permissions, DateCreation, Actif 
                FROM Roles 
                WHERE Nom = @Nom", conn);

            cmd.Parameters.AddWithValue("@Nom", roleName);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var role = new Role
                {
                    Id = reader.GetInt32("Id"),
                    Nom = reader.GetString("Nom"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Permissions = reader.IsDBNull("Permissions") ? null : reader.GetString("Permissions"),
                    DateCreation = reader.GetDateTime("DateCreation"),
                    Actif = reader.GetBoolean("Actif")
                };

                // Si les permissions ne sont pas stockées en base, utiliser les permissions par défaut
                if (string.IsNullOrEmpty(role.Permissions))
                {
                    var defaultPermissions = RolePermissions.GetPermissions(role.Nom);
                    role.SetPermissionsList(defaultPermissions);
                }

                return role;
            }

            return null;
        }

        // Méthodes de statistiques
        public async Task<Dictionary<string, int>> GetUserStatsByRoleAsync()
        {
            var stats = new Dictionary<string, int>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT r.Nom, COUNT(u.Id) as UserCount
                FROM Roles r
                LEFT JOIN Utilisateurs u ON r.Id = u.RoleId AND u.Actif = 1
                WHERE r.Actif = 1
                GROUP BY r.Id, r.Nom
                ORDER BY r.Id", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                stats[reader.GetString("Nom")] = reader.GetInt32("UserCount");
            }

            return stats;
        }

        public async Task<List<Utilisateur>> GetUsersByRoleAsync(string roleName)
        {
            var users = new List<Utilisateur>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT u.*, r.Nom as RoleNom, r.Description as RoleDescription, r.Permissions 
                FROM Utilisateurs u 
                INNER JOIN Roles r ON u.RoleId = r.Id 
                WHERE r.Nom = @RoleName AND u.Actif = 1
                ORDER BY u.DateCreation DESC", conn);

            cmd.Parameters.AddWithValue("@RoleName", roleName);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(MapToUtilisateur(reader));
            }

            return users;
        }

        // Recherche d'utilisateurs
        public async Task<List<Utilisateur>> SearchUsersAsync(string searchTerm)
        {
            var users = new List<Utilisateur>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT u.*, r.Nom as RoleNom, r.Description as RoleDescription, r.Permissions 
                FROM Utilisateurs u 
                LEFT JOIN Roles r ON u.RoleId = r.Id 
                WHERE u.Actif = 1 
                AND (u.Email LIKE @Search 
                     OR u.Username LIKE @Search 
                     OR u.Prenom LIKE @Search 
                     OR u.Nom LIKE @Search)
                ORDER BY u.DateCreation DESC", conn);

            cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(MapToUtilisateur(reader));
            }

            return users;
        }

        private Utilisateur MapToUtilisateur(SqlDataReader reader)
        {
            return new Utilisateur
            {
                Id = reader.GetInt32("Id"),
                Email = reader.GetString("Email"),
                Username = reader.GetString("Username"),
                MotDePasseHash = reader.GetString("MotDePasseHash"),
                Prenom = reader.GetString("Prenom"),
                Nom = reader.GetString("Nom"),
                Telephone = reader.IsDBNull("Telephone") ? null : reader.GetString("Telephone"),
                RoleId = reader.GetInt32("RoleId"),
                Photo = reader.IsDBNull("Photo") ? null : reader.GetString("Photo"),
                DerniereConnexion = reader.IsDBNull("DerniereConnexion") ? null : reader.GetDateTime("DerniereConnexion"),
                DateCreation = reader.GetDateTime("DateCreation"),
                DateModification = reader.GetDateTime("DateModification"),
                Actif = reader.GetBoolean("Actif"),
                Role = reader.IsDBNull("RoleNom") ? null : new Role
                {
                    Id = reader.GetInt32("RoleId"),
                    Nom = reader.GetString("RoleNom"),
                    Description = reader.IsDBNull("RoleDescription") ? null : reader.GetString("RoleDescription"),
                    Permissions = reader.IsDBNull("Permissions") ?
                        JsonSerializer.Serialize(RolePermissions.GetPermissions(reader.GetString("RoleNom"))) :
                        reader.GetString("Permissions")
                }
            };
        }
    }
}