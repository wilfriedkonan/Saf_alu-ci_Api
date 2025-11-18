using Saf_alu_ci_Api.Controllers.Utilisateurs;

namespace Saf_alu_ci_Api.Controllers.ObjectifFinancier
{
    public class ObjectifFinancierModel
    {
        public int Id { get; set; }
        public decimal Montant { get; set; }
        public bool Actif { get; set; } = true;
        public string Statut { get; set; } = "EnCours";
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateCreation { get; set; }
        public DateTime? DateModification { get; set; }
        public int UtilisateurCreation { get; set; }
        public Utilisateur? CreatedBy { get; set; }
    }

    public class CreateObjectifRequest
    {
        public decimal Montant { get; set; }
        public DateTime DateFinPrevue { get; set; }
        public int UtilisateurCreation { get; set; }
    }

    public class UpdateObjectifRequest
    {
        public decimal Montant { get; set; }
        public bool Actif { get; set; }
        public string Statut { get; set; }
        public DateTime DateFinPrevue { get; set; }
        public int UtilisateurModification { get; set; }
    }

}
