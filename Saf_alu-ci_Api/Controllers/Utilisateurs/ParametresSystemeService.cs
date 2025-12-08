using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Parametres;
using System.Data;
using System.Text.Json;

namespace Saf_alu_ci_Api.Controllers.Utilisateurs
{
    /// <summary>
    /// Service complémentaire pour la gestion des paramètres système et fonctionnalités avancées
    /// S'intègre avec UtilisateurService existant
    /// </summary>
    public class ParametresSystemeService
    {
        private readonly string _connectionString;
        private readonly UtilisateurService _utilisateurService;

        public ParametresSystemeService(string connectionString, UtilisateurService utilisateurService)
        {
            _connectionString = connectionString;
            _utilisateurService = utilisateurService;
        }

        // =============================================
        // SECTION: GESTION DES RÔLES (CRUD COMPLET)
        // =============================================

        public async Task<List<RoleResponse>> GetAllRolesWithStatsAsync()
        {
            var roles = new List<RoleResponse>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT 
                        r.Id, r.Nom, r.Description, r.Permissions, r.DateCreation, r.Actif,
                        COUNT(u.Id) as NombreUtilisateurs
                    FROM Roles r
                    LEFT JOIN Utilisateurs u ON r.Id = u.RoleId
                    GROUP BY r.Id, r.Nom, r.Description, r.Permissions, r.DateCreation, r.Actif
                    ORDER BY r.Nom";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var permissionsJson = reader.IsDBNull(reader.GetOrdinal("Permissions"))
                            ? null : reader.GetString(reader.GetOrdinal("Permissions"));

                        List<string> permissions = null;
                        if (!string.IsNullOrEmpty(permissionsJson))
                        {
                            try
                            {
                                permissions = JsonSerializer.Deserialize<List<string>>(permissionsJson);
                            }
                            catch
                            {
                                // Fallback vers les permissions par défaut
                                permissions = RolePermissions.GetPermissions(reader.GetString(reader.GetOrdinal("Nom")));
                            }
                        }
                        else
                        {
                            permissions = RolePermissions.GetPermissions(reader.GetString(reader.GetOrdinal("Nom")));
                        }

                        roles.Add(new RoleResponse
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Nom = reader.GetString(reader.GetOrdinal("Nom")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                                ? null : reader.GetString(reader.GetOrdinal("Description")),
                            Permissions = permissions,
                            DateCreation = reader.IsDBNull(reader.GetOrdinal("DateCreation"))
                                ? null : reader.GetDateTime(reader.GetOrdinal("DateCreation")),
                            Actif = reader.GetBoolean(reader.GetOrdinal("Actif")),
                            NombreUtilisateurs = reader.GetInt32(reader.GetOrdinal("NombreUtilisateurs"))
                        });
                    }
                }
            }

            return roles;
        }

        public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request)
        {
            // Vérifier si le nom existe déjà
            var existingRole = await _utilisateurService.GetRoleByNameAsync(request.Nom);
            if (existingRole != null)
            {
                throw new Exception($"Un rôle avec le nom '{request.Nom}' existe déjà");
            }

            string permissionsJson = request.Permissions != null
                ? JsonSerializer.Serialize(request.Permissions)
                : null;

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
                    var role = await _utilisateurService.GetRoleByIdAsync(newId);

                    return new RoleResponse
                    {
                        Id = role.Id,
                        Nom = role.Nom,
                        Description = role.Description,
                        Permissions = role.GetPermissionsList(),
                        DateCreation = role.DateCreation,
                        Actif = role.Actif,
                        NombreUtilisateurs = 0
                    };
                }
            }
        }

        public async Task<RoleResponse> UpdateRoleAsync(UpdateRoleRequest request)
        {
            // Vérifier que le rôle existe
            var existingRole = await _utilisateurService.GetRoleByIdAsync(request.Id);
            if (existingRole == null)
            {
                throw new Exception("Rôle non trouvé");
            }

            // Vérifier si le nom existe déjà pour un autre rôle
            var roleWithSameName = await _utilisateurService.GetRoleByNameAsync(request.Nom);
            if (roleWithSameName != null && roleWithSameName.Id != request.Id)
            {
                throw new Exception($"Un autre rôle avec le nom '{request.Nom}' existe déjà");
            }

            string permissionsJson = request.Permissions != null
                ? JsonSerializer.Serialize(request.Permissions)
                : null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string updateQuery = @"
                    UPDATE Roles
                    SET Nom = @Nom, Description = @Description, Permissions = @Permissions, Actif = @Actif
                    WHERE Id = @Id";

                using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", request.Id);
                    cmd.Parameters.AddWithValue("@Nom", request.Nom);
                    cmd.Parameters.AddWithValue("@Description", (object)request.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Permissions", (object)permissionsJson ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Actif", request.Actif);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Retourner le rôle mis à jour avec stats
            var roles = await GetAllRolesWithStatsAsync();
            return roles.FirstOrDefault(r => r.Id == request.Id);
        }

        public async Task<bool> DeleteRoleAsync(int id)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Vérifier si des utilisateurs ont ce rôle
                string checkQuery = "SELECT COUNT(*) FROM Utilisateurs WHERE RoleId = @Id";
                using (SqlCommand cmd = new SqlCommand(checkQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    int count = (int)await cmd.ExecuteScalarAsync();

                    if (count > 0)
                    {
                        throw new Exception($"Ce rôle ne peut pas être supprimé car {count} utilisateur(s) l'utilisent");
                    }
                }

                // Supprimer le rôle
                string deleteQuery = "DELETE FROM Roles WHERE Id = @Id";
                using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        // =============================================
        // SECTION: RECHERCHE AVANCÉE AVEC PAGINATION
        // =============================================

        public async Task<SearchUtilisateursResponse> SearchUtilisateursAvanceeAsync(SearchUtilisateursRequest request)
        {
            var response = new SearchUtilisateursResponse
            {
                Utilisateurs = new List<UtilisateurResponse>(),
                CurrentPage = request.PageNumber
            };

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Utiliser la procédure stockée existante si disponible, sinon requête manuelle
                string query = @"
                    WITH FilteredUsers AS (
                        SELECT 
                            u.Id, u.Email, u.Username, u.Prenom, u.Nom, u.Telephone,
                            u.RoleId, u.Photo, u.DerniereConnexion, u.DateCreation, u.Actif,
                            r.Nom as RoleName, r.Description as RoleDescription,
                            ROW_NUMBER() OVER (ORDER BY u.DateCreation DESC) as RowNum
                        FROM Utilisateurs u
                        LEFT JOIN Roles r ON u.RoleId = r.Id
                        WHERE 
                            (@SearchTerm = '' OR 
                             u.Email LIKE '%' + @SearchTerm + '%' OR
                             u.Username LIKE '%' + @SearchTerm + '%' OR
                             u.Prenom LIKE '%' + @SearchTerm + '%' OR
                             u.Nom LIKE '%' + @SearchTerm + '%') AND
                            (@RoleFilter = '' OR r.Nom = @RoleFilter) AND
                            (@StatusFilter IS NULL OR u.Actif = @StatusFilter)
                    )
                    SELECT * FROM FilteredUsers
                    WHERE RowNum BETWEEN @StartRow AND @EndRow;

                    -- Total count
                    SELECT COUNT(*) FROM Utilisateurs u
                    LEFT JOIN Roles r ON u.RoleId = r.Id
                    WHERE 
                        (@SearchTerm = '' OR 
                         u.Email LIKE '%' + @SearchTerm + '%' OR
                         u.Username LIKE '%' + @SearchTerm + '%' OR
                         u.Prenom LIKE '%' + @SearchTerm + '%' OR
                         u.Nom LIKE '%' + @SearchTerm + '%') AND
                        (@RoleFilter = '' OR r.Nom = @RoleFilter) AND
                        (@StatusFilter IS NULL OR u.Actif = @StatusFilter);";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SearchTerm", request.SearchTerm ?? "");
                    cmd.Parameters.AddWithValue("@RoleFilter", request.RoleFilter ?? "");
                    cmd.Parameters.AddWithValue("@StatusFilter", (object)request.StatusFilter ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@StartRow", (request.PageNumber - 1) * request.PageSize + 1);
                    cmd.Parameters.AddWithValue("@EndRow", request.PageNumber * request.PageSize);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        // Lire les utilisateurs
                        while (await reader.ReadAsync())
                        {
                            response.Utilisateurs.Add(new UtilisateurResponse
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                Email = reader.GetString(reader.GetOrdinal("Email")),
                                Username = reader.GetString(reader.GetOrdinal("Username")),
                                Prenom = reader.GetString(reader.GetOrdinal("Prenom")),
                                Nom = reader.GetString(reader.GetOrdinal("Nom")),
                                NomComplet = $"{reader.GetString(reader.GetOrdinal("Prenom"))} {reader.GetString(reader.GetOrdinal("Nom"))}",
                                Telephone = reader.IsDBNull(reader.GetOrdinal("Telephone"))
                                    ? null : reader.GetString(reader.GetOrdinal("Telephone")),
                                RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                                RoleName = reader.IsDBNull(reader.GetOrdinal("RoleName"))
                                    ? null : reader.GetString(reader.GetOrdinal("RoleName")),
                                RoleDescription = reader.IsDBNull(reader.GetOrdinal("RoleDescription"))
                                    ? null : reader.GetString(reader.GetOrdinal("RoleDescription")),
                                Photo = reader.IsDBNull(reader.GetOrdinal("Photo"))
                                    ? null : reader.GetString(reader.GetOrdinal("Photo")),
                                DerniereConnexion = reader.IsDBNull(reader.GetOrdinal("DerniereConnexion"))
                                    ? null : reader.GetDateTime(reader.GetOrdinal("DerniereConnexion")),
                                DateCreation = reader.IsDBNull(reader.GetOrdinal("DateCreation"))
                                    ? null : reader.GetDateTime(reader.GetOrdinal("DateCreation")),
                                Actif = reader.GetBoolean(reader.GetOrdinal("Actif"))
                            });
                        }

                        // Lire le total
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

        // =============================================
        // SECTION: STATISTIQUES AVANCÉES
        // =============================================

        public async Task<StatistiquesUtilisateursResponse> GetStatistiquesDetailleesAsync()
        {
            var stats = new StatistiquesUtilisateursResponse
            {
                ParRole = new List<StatistiqueParRole>()
            };

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Statistiques globales
                string globalQuery = @"
                    SELECT 
                        COUNT(*) as Total,
                        SUM(CASE WHEN Actif = 1 THEN 1 ELSE 0 END) as Actifs,
                        SUM(CASE WHEN Actif = 0 THEN 1 ELSE 0 END) as Inactifs,
                        SUM(CASE WHEN DerniereConnexion > DATEADD(day, -30, GETUTCDATE()) THEN 1 ELSE 0 END) as Recents
                    FROM Utilisateurs";

                using (SqlCommand cmd = new SqlCommand(globalQuery, conn))
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

                // Statistiques par rôle
                string roleQuery = @"
                    SELECT 
                        r.Nom, r.Description,
                        COUNT(u.Id) as Total,
                        SUM(CASE WHEN u.Actif = 1 THEN 1 ELSE 0 END) as Actifs,
                        SUM(CASE WHEN u.DerniereConnexion > DATEADD(day, -30, GETUTCDATE()) THEN 1 ELSE 0 END) as Recents
                    FROM Roles r
                    LEFT JOIN Utilisateurs u ON r.Id = u.RoleId
                    WHERE r.Actif = 1
                    GROUP BY r.Id, r.Nom, r.Description
                    ORDER BY r.Nom";

                using (SqlCommand cmd = new SqlCommand(roleQuery, conn))
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
        // SECTION: GESTION DES PARAMÈTRES SYSTÈME
        // =============================================

        public async Task<List<ParametreResponse>> GetAllParametresAsync()
        {
            var parametres = new List<ParametreResponse>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT Id, Cle, Valeur, TypeValeur, Description, Categorie, 
                           DateModification, UtilisateurModification 
                    FROM Parametres 
                    ORDER BY Categorie, Cle";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        parametres.Add(new ParametreResponse
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Cle = reader.GetString(reader.GetOrdinal("Cle")),
                            Valeur = reader.IsDBNull(reader.GetOrdinal("Valeur"))
                                ? null : reader.GetString(reader.GetOrdinal("Valeur")),
                            TypeValeur = reader.IsDBNull(reader.GetOrdinal("TypeValeur"))
                                ? null : reader.GetString(reader.GetOrdinal("TypeValeur")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                                ? null : reader.GetString(reader.GetOrdinal("Description")),
                            Categorie = reader.IsDBNull(reader.GetOrdinal("Categorie"))
                                ? null : reader.GetString(reader.GetOrdinal("Categorie")),
                            DateModification = reader.IsDBNull(reader.GetOrdinal("DateModification"))
                                ? null : reader.GetDateTime(reader.GetOrdinal("DateModification")),
                            UtilisateurModification = reader.IsDBNull(reader.GetOrdinal("UtilisateurModification"))
                                ? null : reader.GetInt32(reader.GetOrdinal("UtilisateurModification"))
                        });
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
                string query = @"
                    SELECT Id, Cle, Valeur, TypeValeur, Description, Categorie, 
                           DateModification, UtilisateurModification 
                    FROM Parametres 
                    WHERE Cle = @Cle";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Cle", cle);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new ParametreResponse
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                Cle = reader.GetString(reader.GetOrdinal("Cle")),
                                Valeur = reader.IsDBNull(reader.GetOrdinal("Valeur"))
                                    ? null : reader.GetString(reader.GetOrdinal("Valeur")),
                                TypeValeur = reader.IsDBNull(reader.GetOrdinal("TypeValeur"))
                                    ? null : reader.GetString(reader.GetOrdinal("TypeValeur")),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                                    ? null : reader.GetString(reader.GetOrdinal("Description")),
                                Categorie = reader.IsDBNull(reader.GetOrdinal("Categorie"))
                                    ? null : reader.GetString(reader.GetOrdinal("Categorie")),
                                DateModification = reader.IsDBNull(reader.GetOrdinal("DateModification"))
                                    ? null : reader.GetDateTime(reader.GetOrdinal("DateModification")),
                                UtilisateurModification = reader.IsDBNull(reader.GetOrdinal("UtilisateurModification"))
                                    ? null : reader.GetInt32(reader.GetOrdinal("UtilisateurModification"))
                            };
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

                // Utiliser la procédure stockée si disponible
                try
                {
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
                                return new ParametreResponse
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    Cle = reader.GetString(reader.GetOrdinal("Cle")),
                                    Valeur = reader.IsDBNull(reader.GetOrdinal("Valeur"))
                                        ? null : reader.GetString(reader.GetOrdinal("Valeur")),
                                    TypeValeur = reader.IsDBNull(reader.GetOrdinal("TypeValeur"))
                                        ? null : reader.GetString(reader.GetOrdinal("TypeValeur")),
                                    Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                                        ? null : reader.GetString(reader.GetOrdinal("Description")),
                                    Categorie = reader.IsDBNull(reader.GetOrdinal("Categorie"))
                                        ? null : reader.GetString(reader.GetOrdinal("Categorie")),
                                    DateModification = reader.IsDBNull(reader.GetOrdinal("DateModification"))
                                        ? null : reader.GetDateTime(reader.GetOrdinal("DateModification")),
                                    UtilisateurModification = reader.IsDBNull(reader.GetOrdinal("UtilisateurModification"))
                                        ? null : reader.GetInt32(reader.GetOrdinal("UtilisateurModification"))
                                };
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback: mise à jour manuelle si la procédure n'existe pas
                    string updateQuery = @"
                        UPDATE Parametres
                        SET Valeur = @Valeur,
                            DateModification = GETUTCDATE(),
                            UtilisateurModification = @UtilisateurId
                        WHERE Cle = @Cle";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Cle", request.Cle);
                        cmd.Parameters.AddWithValue("@Valeur", request.Valeur);
                        cmd.Parameters.AddWithValue("@UtilisateurId", utilisateurId);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        if (rowsAffected > 0)
                        {
                            return await GetParametreByKeyAsync(request.Cle);
                        }
                    }
                }
            }

            return null;
        }
    }
}
