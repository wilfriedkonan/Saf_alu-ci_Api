using System.ComponentModel.DataAnnotations;

namespace Saf_alu_ci_Api.Controllers.WhatsApp
{
    // =============================================
    // MODÈLES
    // =============================================

    public class WhatsAppCompte
    {
        public int Id { get; set; }
        public string NomInstance { get; set; } = string.Empty;
        public string NomAffichage { get; set; } = string.Empty;
        public string NumeroTelephone { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Service { get; set; }
        public bool Actif { get; set; } = true;
        public bool Connecte { get; set; } = false;
        public DateTime? DateConnexion { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public int UtilisateurCreation { get; set; }
        public int UtilisateurModification { get; set; }
    }

    public class WhatsAppMessageType
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Actif { get; set; } = true;
    }

    public class WhatsAppMessagePredefini
    {
        public int Id { get; set; }
        public int IdType { get; set; }
        public string Titre { get; set; } = string.Empty;
        public string Contenu { get; set; } = string.Empty;
        public string? Variables { get; set; }
        public bool Actif { get; set; } = true;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public int UtilisateurCreation { get; set; }
        public int UtilisateurModification { get; set; }

        // Navigation
        public WhatsAppMessageType? Type { get; set; }
    }

    // =============================================
    // DTOs — COMPTES
    // =============================================

    public class CreateWhatsAppCompteRequest
    {
        [Required(ErrorMessage = "Le nom d'instance est obligatoire")]
        [StringLength(100)]
        public string NomInstance { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le nom d'affichage est obligatoire")]
        [StringLength(150)]
        public string NomAffichage { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le numéro de téléphone est obligatoire")]
        [StringLength(20)]
        public string NumeroTelephone { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? Service { get; set; }
    }

    public class UpdateWhatsAppCompteRequest
    {
        [Required(ErrorMessage = "Le nom d'affichage est obligatoire")]
        [StringLength(150)]
        public string NomAffichage { get; set; } = string.Empty;

        [StringLength(20)]
        public string? NumeroTelephone { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? Service { get; set; }
    }

    public class ConnexionWhatsAppRequest
    {
        [Required]
        public bool Connecte { get; set; }
    }

    // =============================================
    // DTOs — MESSAGES PRÉDÉFINIS
    // =============================================

    public class CreateWhatsAppMessagePredefiniRequest
    {
        [Required(ErrorMessage = "Le type de message est obligatoire")]
        public int IdType { get; set; }

        [Required(ErrorMessage = "Le titre est obligatoire")]
        [StringLength(150)]
        public string Titre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le contenu est obligatoire")]
        public string Contenu { get; set; } = string.Empty;

        /// <summary>
        /// Liste des variables présentes dans le contenu, séparées par des virgules.
        /// Ex : {PRENOM},{NOM},{EMAIL}
        /// </summary>
        [StringLength(500)]
        public string? Variables { get; set; }
    }

    public class UpdateWhatsAppMessagePredefiniRequest
    {
        [Required(ErrorMessage = "Le titre est obligatoire")]
        [StringLength(150)]
        public string Titre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le contenu est obligatoire")]
        public string Contenu { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Variables { get; set; }

        public bool Actif { get; set; } = true;
    }

    /// <summary>
    /// Request pour prévisualiser un message en substituant ses variables.
    /// </summary>
    public class PrevisualiserMessageRequest
    {
        /// <summary>
        /// Dictionnaire clé/valeur : {"PRENOM": "Koffi", "NOM": "Atta", ...}
        /// Les clés doivent correspondre aux noms de variables SANS les accolades.
        /// </summary>
        [Required(ErrorMessage = "Le dictionnaire de variables est obligatoire")]
        public Dictionary<string, string> Variables { get; set; } = new();
    }
}