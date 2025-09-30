namespace Saf_alu_ci_Api.Controllers.Clients
{
    public class Client
    {
        public int Id { get; set; }
        public string TypeClient { get; set; } // Particulier, Entreprise, Collectivite
        public string Nom { get; set; }
        public string? Prenom { get; set; }
        public string? RaisonSociale { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? TelephoneMobile { get; set; }
        public string? Adresse { get; set; }
        public string? CodePostal { get; set; }
        public string? Ville { get; set; }
        public string Pays { get; set; }
        public string? Siret { get; set; }
        public string? NumeroTVA { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public bool Actif { get; set; } = true;
        public int? UtilisateurCreation { get; set; }
    }
    public class CreateClientRequest
    {
        public string TypeClient { get; set; }
        public string Nom { get; set; }
        public string? Prenom { get; set; }
        public string? RaisonSociale { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? TelephoneMobile { get; set; }
        public string? Adresse { get; set; }
        public string? CodePostal { get; set; }
        public string? Ville { get; set; }
        public string? Siret { get; set; }
        public string? NumeroTVA { get; set; }
    }
}
