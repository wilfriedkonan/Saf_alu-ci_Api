using System.ComponentModel.DataAnnotations;

namespace Saf_alu_ci_Api.Controllers.Parametres
{
    public class Parametre
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Cle { get; set; }

        public string? Valeur { get; set; }

        [MaxLength(20)]
        public string? TypeValeur { get; set; } // string, integer, decimal, boolean, json

        [MaxLength(300)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Categorie { get; set; }

        public DateTime? DateModification { get; set; } = DateTime.UtcNow;
        public int? UtilisateurModification { get; set; }
    }

    // =============================================
    // DTOs POUR LES PARAMÈTRES SYSTÈME
    // =============================================

    public class ParametreResponse
    {
        public int Id { get; set; }
        public string Cle { get; set; }
        public string? Valeur { get; set; }
        public string? TypeValeur { get; set; }
        public string? Description { get; set; }
        public string? Categorie { get; set; }
        public DateTime? DateModification { get; set; }
        public int? UtilisateurModification { get; set; }
    }

    public class ParametresByCategorieResponse
    {
        public string Categorie { get; set; }
        public List<ParametreResponse> Parametres { get; set; }
    }

    public class UpdateParametreRequest
    {
        [Required]
        public string Cle { get; set; }

        [Required]
        public string Valeur { get; set; }
    }

    public class SearchUtilisateursRequest
    {
        public string? SearchTerm { get; set; }
        public string? RoleFilter { get; set; }
        public bool? StatusFilter { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

}
