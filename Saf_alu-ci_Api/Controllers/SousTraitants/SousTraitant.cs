namespace Saf_alu_ci_Api.Controllers.SousTraitants
{
    public class SousTraitant
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public string? RaisonSociale { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? TelephoneMobile { get; set; }
        public string? Adresse { get; set; }
        public string? CodePostal { get; set; }
        public string? Ville { get; set; }
        public string? Siret { get; set; }
        public string? NumeroTVA { get; set; }
        public string? NomContact { get; set; }
        public string? PrenomContact { get; set; }
        public string? EmailContact { get; set; }
        public string? TelephoneContact { get; set; }
        public decimal NoteMoyenne { get; set; } = 0;
        public int NombreEvaluations { get; set; } = 0;
        public bool AssuranceValide { get; set; } = false;
        public DateTime? DateExpirationAssurance { get; set; }
        public string? NumeroAssurance { get; set; }
        public string? Certifications { get; set; } // JSON
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public bool Actif { get; set; } = true;
        public int? UtilisateurCreation { get; set; }

        // Navigation properties
        public virtual List<SousTraitantSpecialite>? Specialites { get; set; }
        public virtual List<EvaluationSousTraitant>? Evaluations { get; set; }
    }

    public class Specialite
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public string Couleur { get; set; } = "#059669";
        public bool Actif { get; set; } = true;
    }

    public class SousTraitantSpecialite
    {
        public int SousTraitantId { get; set; }
        public int SpecialiteId { get; set; }
        public int NiveauExpertise { get; set; } = 3; // 1-5
        public virtual Specialite? Specialite { get; set; }
    }

    public class EvaluationSousTraitant
    {
        public int Id { get; set; }
        public int SousTraitantId { get; set; }
        public int ProjetId { get; set; }
        public int? EtapeProjetId { get; set; }
        public int Note { get; set; } // 1-5
        public string? Commentaire { get; set; }
        public string? Criteres { get; set; } // JSON
        public DateTime DateEvaluation { get; set; } = DateTime.UtcNow;
        public int EvaluateurId { get; set; }
    }

    public class CreateSousTraitantRequest
    {
        public string Nom { get; set; }
        public string? RaisonSociale { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? TelephoneMobile { get; set; }
        public string? Adresse { get; set; }
        public string? CodePostal { get; set; }
        public string? Ville { get; set; }
        public string? Siret { get; set; }
        public string? NumeroTVA { get; set; }
        public string? NomContact { get; set; }
        public string? PrenomContact { get; set; }
        public string? EmailContact { get; set; }
        public string? TelephoneContact { get; set; }
        public List<int>? SpecialiteIds { get; set; }
        public bool AssuranceValide { get; set; }
        public DateTime? DateExpirationAssurance { get; set; }
        public string? NumeroAssurance { get; set; }
    }

    public class CreateEvaluationRequest
    {
        public int SousTraitantId { get; set; }
        public int ProjetId { get; set; }
        public int? EtapeProjetId { get; set; }
        public int Note { get; set; }
        public string? Commentaire { get; set; }
        public Dictionary<string, int>? Criteres { get; set; } // Qualité, Délais, Communication, etc.
    }

    // DTOs supplémentaires pour Sous-traitants
    public class UpdateSousTraitantRequest : CreateSousTraitantRequest
    {
        public bool? AssuranceValide { get; set; }
        public DateTime? DateExpirationAssurance { get; set; }
        public string? NumeroAssurance { get; set; }
        public string? Certifications { get; set; }
        public List<SpecialiteAvecNiveau>? Specialites { get; set; }
    }

    public class SpecialiteAvecNiveau
    {
        public int SpecialiteId { get; set; }
        public int NiveauExpertise { get; set; } = 3;
    }

    public class UpdateAssuranceRequest
    {
        public bool AssuranceValide { get; set; }
        public DateTime? DateExpirationAssurance { get; set; }
        public string? NumeroAssurance { get; set; }
    }
}
