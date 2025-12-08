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

    public class UpdateUtilisateurRequest
    {
        [Required]
        public int Id { get; set; }

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

        [Required]
        public int RoleId { get; set; }

        public string? Photo { get; set; }
        public bool Actif { get; set; }
    }
    public class ChangePasswordRequest
    {
        [Required]
        public int UtilisateurId { get; set; }

        [Required(ErrorMessage = "L'ancien mot de passe est requis")]
        public string AncienMotDePasse { get; set; }

        [Required(ErrorMessage = "Le nouveau mot de passe est requis")]
        [MinLength(6, ErrorMessage = "Le mot de passe doit contenir au moins 6 caractères")]
        public string NouveauMotDePasse { get; set; }

        [Required(ErrorMessage = "La confirmation est requise")]
        [Compare("NouveauMotDePasse", ErrorMessage = "Les mots de passe ne correspondent pas")]
        public string ConfirmationMotDePasse { get; set; }
    }

    public class CreateUtilisateurRequest
    {
        [Required(ErrorMessage = "L'email est requis")]
        [EmailAddress(ErrorMessage = "Format d'email invalide")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Le nom d'utilisateur est requis")]
        [MinLength(3, ErrorMessage = "Le nom d'utilisateur doit contenir au moins 3 caractères")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Le mot de passe est requis")]
        [MinLength(6, ErrorMessage = "Le mot de passe doit contenir au moins 6 caractères")]
        public string MotDePasse { get; set; }

        [Required(ErrorMessage = "Le prénom est requis")]
        public string Prenom { get; set; }

        [Required(ErrorMessage = "Le nom est requis")]
        public string Nom { get; set; }

        public string? Telephone { get; set; }

        [Required(ErrorMessage = "Le rôle est requis")]
        public int RoleId { get; set; }

        public string? Photo { get; set; }
        public bool Actif { get; set; } = true;
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

    public class UtilisateurResponse
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string Prenom { get; set; }
        public string Nom { get; set; }
        public string NomComplet { get; set; }
        public string? Telephone { get; set; }
        public int RoleId { get; set; }
        public string? RoleName { get; set; }
        public string? RoleDescription { get; set; }
        public string? Photo { get; set; }
        public DateTime? DerniereConnexion { get; set; }
        public DateTime? DateCreation { get; set; }
        public bool Actif { get; set; }
    }

    // =============================================
    // DTOs: RÔLE
    // =============================================
    public class CreateRoleRequest
    {
        [Required(ErrorMessage = "Le nom du rôle est requis")]
        [MaxLength(50)]
        public string Nom { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        public List<string>? Permissions { get; set; }
        public bool Actif { get; set; } = true;
    }

    public class UpdateRoleRequest
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Nom { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        public List<string>? Permissions { get; set; }
        public bool Actif { get; set; }
    }

    public class RoleResponse
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public List<string>? Permissions { get; set; }
        public DateTime? DateCreation { get; set; }
        public bool Actif { get; set; }
        public int? NombreUtilisateurs { get; set; }
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

    public class SearchUtilisateursResponse
    {
        public List<UtilisateurResponse> Utilisateurs { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }

    public class StatistiquesUtilisateursResponse
    {
        public int TotalUtilisateurs { get; set; }
        public int UtilisateursActifs { get; set; }
        public int UtilisateursInactifs { get; set; }
        public int ConnexionsRecentes { get; set; } // Dernières 30 jours
        public List<StatistiqueParRole> ParRole { get; set; }
    }

    public class StatistiqueParRole
    {
        public string RoleName { get; set; }
        public string? RoleDescription { get; set; }
        public int NombreUtilisateurs { get; set; }
        public int UtilisateursActifs { get; set; }
        public int ConnexionsRecentes { get; set; }
    }

    // =============================================
    // ÉNUMÉRATION: PERMISSIONS
    // =============================================
    public static class PermissionsList
    {
        // Permissions globales
        public const string ALL = "all";

        // Utilisateurs
        public const string USERS_READ = "users.read";
        public const string USERS_CREATE = "users.create";
        public const string USERS_UPDATE = "users.update";
        public const string USERS_DELETE = "users.delete";
        public const string USERS_ALL = "users.all";

        // Projets
        public const string PROJECTS_READ = "projects.read";
        public const string PROJECTS_CREATE = "projects.create";
        public const string PROJECTS_UPDATE = "projects.update";
        public const string PROJECTS_DELETE = "projects.delete";
        public const string PROJECTS_ALL = "projects.all";
        public const string PROJECTS_ASSIGNED = "projects.assigned";
        public const string PROJECTS_TASKS = "projects.tasks";

        // Clients
        public const string CLIENTS_READ = "clients.read";
        public const string CLIENTS_CREATE = "clients.create";
        public const string CLIENTS_UPDATE = "clients.update";
        public const string CLIENTS_DELETE = "clients.delete";
        public const string CLIENTS_ALL = "clients.all";

        // Factures
        public const string INVOICES_READ = "invoices.read";
        public const string INVOICES_CREATE = "invoices.create";
        public const string INVOICES_UPDATE = "invoices.update";
        public const string INVOICES_DELETE = "invoices.delete";
        public const string INVOICES_ALL = "invoices.all";

        // Finance
        public const string FINANCE_READ = "finance.read";
        public const string FINANCE_CREATE = "finance.create";
        public const string FINANCE_UPDATE = "finance.update";
        public const string FINANCE_DELETE = "finance.delete";
        public const string FINANCE_ALL = "finance.all";

        // DQE
        public const string DQE_READ = "dqe.read";
        public const string DQE_CREATE = "dqe.create";
        public const string DQE_UPDATE = "dqe.update";
        public const string DQE_DELETE = "dqe.delete";
        public const string DQE_ALL = "dqe.all";

        // Stock
        public const string STOCK_READ = "stock.read";
        public const string STOCK_CREATE = "stock.create";
        public const string STOCK_UPDATE = "stock.update";
        public const string STOCK_DELETE = "stock.delete";
        public const string STOCK_ALL = "stock.all";

        // Rapports
        public const string REPORTS_FINANCE = "reports.finance";
        public const string REPORTS_PROJECTS = "reports.projects";
        public const string REPORTS_ALL = "reports.all";

        // Documents
        public const string DOCUMENTS_READ = "documents.read";
        public const string DOCUMENTS_UPLOAD = "documents.upload";
        public const string DOCUMENTS_DELETE = "documents.delete";

        // Paramètres
        public const string SETTINGS_READ = "settings.read";
        public const string SETTINGS_UPDATE = "settings.update";

        // Tâches
        public const string TASKS_UPDATE = "tasks.update";
    }

    /// <summary>
    /// Request pour inviter un nouvel utilisateur
    /// </summary>
    public class InviterUtilisateurRequest
    {
        public string Email { get; set; }
        public string Prenom { get; set; }
        public string Nom { get; set; }
        public string Telephone { get; set; }
        public int RoleId { get; set; }
    }

    /// <summary>
    /// Response après invitation
    /// </summary>
    public class UtilisateurInvitationResponse
    {
        public int UtilisateurId { get; set; }
        public string Email { get; set; }
        public string NomComplet { get; set; }
        public bool EmailEnvoye { get; set; }
        public DateTime TokenExpiration { get; set; }
    }

    /// <summary>
    /// Request pour compléter l'inscription
    /// </summary>
    public class CompleterInscriptionRequest
    {
        public string Token { get; set; }
        public string Username { get; set; }
        public string MotDePasse { get; set; }
        public string ConfirmationMotDePasse { get; set; }
    }

    /// <summary>
    /// Request pour valider le token
    /// </summary>
    public class ValiderTokenRequest
    {
        public string Token { get; set; }
    }

    /// <summary>
    /// Response avec info utilisateur depuis token
    /// </summary>
    public class TokenInfoResponse
    {
        public string Email { get; set; }
        public string Prenom { get; set; }
        public string Nom { get; set; }
        public DateTime TokenExpiration { get; set; }
        public bool TokenValide { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "L'email est requis")]
        [EmailAddress(ErrorMessage = "Format d'email invalide")]
        public string Email { get; set; }
    }

    /// <summary>
    /// Request pour réinitialiser le mot de passe avec le token
    /// </summary>
    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "Le token est requis")]
        public string Token { get; set; }

        [Required(ErrorMessage = "Le nouveau mot de passe est requis")]
        [MinLength(8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères")]
        public string NouveauMotDePasse { get; set; }

        [Required(ErrorMessage = "La confirmation est requise")]
        [Compare("NouveauMotDePasse", ErrorMessage = "Les mots de passe ne correspondent pas")]
        public string ConfirmationMotDePasse { get; set; }
    }

    /// <summary>
    /// Request pour valider un token de réinitialisation
    /// </summary>
    public class ValidateResetTokenRequest
    {
        [Required(ErrorMessage = "Le token est requis")]
        public string Token { get; set; }
    }

    /// <summary>
    /// Response après demande de réinitialisation
    /// </summary>
    public class ForgotPasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Email { get; set; }
        public DateTime TokenExpiration { get; set; }
    }

    /// <summary>
    /// Response validation token
    /// </summary>
    public class ValidateResetTokenResponse
    {
        public bool TokenValide { get; set; }
        public string Email { get; set; }
        public string NomComplet { get; set; }
        public DateTime? TokenExpiration { get; set; }
        public string Message { get; set; }
    }
}

