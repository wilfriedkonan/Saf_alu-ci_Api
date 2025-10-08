namespace Saf_alu_ci_Api.Controllers.Clients
{
    public class Client
    {
        public int Id { get; set; }
        public string TypeClient { get; set; } // Particulier, Entreprise, Collectivite
        public string Nom { get; set; }
        public string? RaisonSociale { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
        public string? Ncc { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime DateModification { get; set; } = DateTime.UtcNow;
        public bool Actif { get; set; } = true;
        public string? Status { get; set; }// "actif" | "inactif" | "prospect"//
        public int? UtilisateurCreation { get; set; }
    }
    public class CreateClientRequest
    {
        public string TypeClient { get; set; }
        public string Nom { get; set; }
        public string? RaisonSociale { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
        public string? Ncc { get; set; }
    }

    public class Statistique
    {
        public int? TotalDevis { get; set; }
        public int? TotalFactures { get; set; }
        public int? TotalProjets { get; set; }
        public decimal? TotalRevenue { get; set; }
    }

    public class StatistiqueGolbal
    {
        public int? TotalClients { get; set; }
        public int? TotalActifs { get; set; }
        public int? TotalProspects { get; set; }
        public decimal? TotalEntreprises { get; set; }
    }

    /// <summary>
    /// Classe pour retourner un client avec ses statistiques
    /// </summary>
    public class ClientWithStatistique
    {
        public int Id { get; set; }
        public string TypeClient { get; set; }
        public string Nom { get; set; }
        public string? RaisonSociale { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Adresse { get; set; }
        public string? Ville { get; set; }
        public string? Ncc { get; set; }
        public DateTime DateCreation { get; set; }
        public DateTime DateModification { get; set; }
        public bool Actif { get; set; }
        public string? Status { get; set; }
        public int? UtilisateurCreation { get; set; }
        public Statistique Statistique { get; set; }
    }
}
