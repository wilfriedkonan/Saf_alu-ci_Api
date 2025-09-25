using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Saf_alu_ci_Api.Controllers.Utilisateurs
{
    public class Utilisateur
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string MotDePasseHash { get; set; }
        public string Prenom { get; set; }
        public string Nom { get; set; }
        public string? Telephone { get; set; }
        public int RoleId { get; set; }
        public string? Photo { get; set; }
        public DateTime? DerniereConnexion { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public bool Actif { get; set; } = true;

        // Navigation properties
        public virtual Role? Role { get; set; }

        // Helper method to check permissions
        public bool HasPermission(string permission)
        {
            if (Role?.Nom == null) return false;

            var permissions = RolePermissions.GetPermissions(Role.Nom);
            return permissions.Contains(permission);
        }
    }

    public class Role
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public string? Permissions { get; set; } // JSON array de permissions
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public bool Actif { get; set; } = true;

        // Helper method to get permissions as List
        public List<string> GetPermissionsList()
        {
            if (string.IsNullOrEmpty(Permissions))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(Permissions) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        // Helper method to set permissions from List
        public void SetPermissionsList(List<string> permissions)
        {
            Permissions = JsonSerializer.Serialize(permissions);
        }
    }

    // Énumération des rôles
    public enum UserRole
    {
        super_admin,
        admin,
        chef_projet,
        comptable,
        commercial,
        sous_traitant
    }

    // Classe statique pour gérer les permissions par rôle
    public static class RolePermissions
    {
        public static readonly Dictionary<string, List<string>> Permissions = new()
        {
            {
                "super_admin",
                new List<string>
                {
                    "dashboard", "devis", "projets", "factures", "clients",
                    "sous_traitants", "tresorerie", "utilisateurs", "notifications", "parametres"
                }
            },
            {
                "admin",
                new List<string>
                {
                    "dashboard", "devis", "projets", "factures", "clients",
                    "sous_traitants", "tresorerie", "utilisateurs", "notifications"
                }
            },
            {
                "chef_projet",
                new List<string>
                {
                    "dashboard", "projets", "sous_traitants", "clients"
                }
            },
            {
                "comptable",
                new List<string>
                {
                    "dashboard", "factures", "tresorerie", "clients"
                }
            },
            {
                "commercial",
                new List<string>
                {
                    "dashboard", "devis", "clients"
                }
            },
            {
                "sous_traitant",
                new List<string>
                {
                    "dashboard", "projets"
                }
            }
        };

        public static readonly Dictionary<string, string> RoleLabels = new()
        {
            { "super_admin", "Super Admin" },
            { "admin", "Administrateur" },
            { "chef_projet", "Chef de Projet" },
            { "comptable", "Comptable" },
            { "commercial", "Commercial" },
            { "sous_traitant", "Sous-traitant" }
        };

        public static readonly Dictionary<string, int> RoleHierarchy = new()
        {
            { "sous_traitant", 1 },
            { "commercial", 2 },
            { "comptable", 2 },
            { "chef_projet", 3 },
            { "admin", 4 },
            { "super_admin", 5 }
        };

        public static List<string> GetPermissions(string role)
        {
            return Permissions.TryGetValue(role, out var permissions) ? permissions : new List<string>();
        }

        public static string GetRoleLabel(string role)
        {
            return RoleLabels.TryGetValue(role, out var label) ? label : role;
        }

        public static bool IsRoleHigherOrEqual(string userRole, string requiredRole)
        {
            var userLevel = RoleHierarchy.TryGetValue(userRole, out var uLevel) ? uLevel : 0;
            var requiredLevel = RoleHierarchy.TryGetValue(requiredRole, out var rLevel) ? rLevel : 0;

            return userLevel >= requiredLevel;
        }

        public static List<string> GetAllRoles()
        {
            return RoleLabels.Keys.ToList();
        }
    }

    // DTOs mis à jour
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }

    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; }

        [Required]
        public string Prenom { get; set; }

        [Required]
        public string Nom { get; set; }

        public string? Telephone { get; set; }

        [Required]
        public int RoleId { get; set; }
    }

    public class UpdateUserRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string Prenom { get; set; }

        [Required]
        public string Nom { get; set; }

        public string? Telephone { get; set; }
        public int RoleId { get; set; }
        public string? Photo { get; set; }
        public bool? Actif { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; }

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; }
    }

    // Response DTOs
    public class AuthResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public AuthData? Data { get; set; }
        public int Status { get; set; }
    }

    public class AuthData
    {
        public string Token { get; set; }
        public UserProfile User { get; set; }
        public DateTime? Expiration { get; set; }
    }

    public class UserProfile
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string Prenom { get; set; }
        public string Nom { get; set; }
        public string? Telephone { get; set; }
        public string? Photo { get; set; }
        public string? Role { get; set; }
        public List<string> Permissions { get; set; } = new();
        public DateTime? DerniereConnexion { get; set; }
        public DateTime DateCreation { get; set; }
        public bool Actif { get; set; }
    }

    public class RoleInfo
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public string Label { get; set; }
        public List<string> Permissions { get; set; }
    }
}