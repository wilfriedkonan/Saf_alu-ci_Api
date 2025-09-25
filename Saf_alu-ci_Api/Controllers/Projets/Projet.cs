using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.Utilisateurs;

namespace Saf_alu_ci_Api.Controllers.Projets
{
    public class Projet
    {
        public int Id { get; set; }
        public string Numero { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int ClientId { get; set; }
        public int TypeProjetId { get; set; }
        public int? DevisId { get; set; }
        public string Statut { get; set; } = "Planification"; // Planification, EnCours, Suspendu, Termine, Annule
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinRelle { get; set; }
        public decimal BudgetInitial { get; set; }
        public decimal BudgetRevise { get; set; }
        public decimal CoutReel { get; set; }
        public string? AdresseChantier { get; set; }
        public string? CodePostalChantier { get; set; }
        public string? VilleChantier { get; set; }
        public int PourcentageAvancement { get; set; } = 0;
        public int? ChefProjetId { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public int UtilisateurCreation { get; set; }
        public bool Actif { get; set; } = true;

        // Navigation properties
        public virtual Client? Client { get; set; }
        public virtual TypeProjet? TypeProjet { get; set; }
        public virtual Utilisateur? ChefProjet { get; set; }
        public virtual List<EtapeProjet>? Etapes { get; set; }
    }

    public class TypeProjet
    {
        public int Id { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public string Couleur { get; set; } = "#2563eb";
        public bool Actif { get; set; } = true;
    }

    public class EtapeProjet
    {
        public int Id { get; set; }
        public int ProjetId { get; set; }
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int Ordre { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }
        public string Statut { get; set; } = "NonCommence"; // NonCommence, EnCours, Termine, Suspendu
        public int PourcentageAvancement { get; set; } = 0;
        public decimal BudgetPrevu { get; set; }
        public decimal CoutReel { get; set; }
        public int? ResponsableId { get; set; }
        public string TypeResponsable { get; set; } = "Interne"; // Interne, SousTraitant
    }

    public class CreateProjetRequest
    {
        public string Nom { get; set; }
        public string? Description { get; set; }
        public int ClientId { get; set; }
        public int TypeProjetId { get; set; }
        public int? DevisId { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public decimal BudgetInitial { get; set; }
        public string? AdresseChantier { get; set; }
        public string? CodePostalChantier { get; set; }
        public string? VilleChantier { get; set; }
        public int? ChefProjetId { get; set; }
        public List<CreateEtapeProjetRequest>? Etapes { get; set; }
    }

    public class CreateEtapeProjetRequest
    {
        public string Nom { get; set; }
        public string? Description { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public decimal BudgetPrevu { get; set; }
    }

    public class UpdateAvancementRequest
    {
        public int PourcentageAvancement { get; set; }
        public int? Note { get; set; } // Note sur 5 pour évaluation
        public string? Commentaire { get; set; }
    }
}
