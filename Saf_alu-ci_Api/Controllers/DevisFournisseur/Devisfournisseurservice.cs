using Microsoft.Data.SqlClient;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.DevisFournisseur
{
    public class DevisFournisseurService
    {
        private readonly string _connectionString;

        public DevisFournisseurService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // =============================================
        // FOURNISSEURS — CRUD
        // =============================================

        public async Task<List<Fournisseur>> GetFournisseursAsync(string? search = null)
        {
            var list = new List<Fournisseur>();
            using var conn = new SqlConnection(_connectionString);
            var sql = @"SELECT * FROM Fournisseurs
                        WHERE Actif = 1
                          AND (@Search IS NULL
                               OR Nom        LIKE '%' + @Search + '%'
                               OR Telephone  LIKE '%' + @Search + '%'
                               OR NomContact LIKE '%' + @Search + '%')
                        ORDER BY Nom";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Search", search ?? (object)DBNull.Value);
            await conn.OpenAsync();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(MapToFournisseur(r));
            return list;
        }

        public async Task<Fournisseur?> GetFournisseurByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM Fournisseurs WHERE Id = @Id AND Actif = 1", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();
            using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapToFournisseur(r) : null;
        }

        public async Task<int> CreateFournisseurAsync(Fournisseur f)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO Fournisseurs
                    (Nom,RaisonSociale,Email,Telephone,Adresse,Ville,
                     NomContact,TelephoneContact,EmailContact,Ncc,
                     Actif,DateCreation,DateModification,UtilisateurCreation)
                VALUES
                    (@Nom,@RaisonSociale,@Email,@Telephone,@Adresse,@Ville,
                     @NomContact,@TelephoneContact,@EmailContact,@Ncc,
                     1,@Now,@Now,@UtilisateurCreation);
                SELECT CAST(SCOPE_IDENTITY() AS INT)", conn);
            AddFournisseurParams(cmd, f);
            await conn.OpenAsync();
            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task<bool> UpdateFournisseurAsync(int id, UpdateFournisseurRequest req, int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Fournisseurs SET
                    Nom=@Nom, RaisonSociale=@RaisonSociale, Email=@Email,
                    Telephone=@Telephone, Adresse=@Adresse, Ville=@Ville,
                    NomContact=@NomContact, TelephoneContact=@TelephoneContact,
                    EmailContact=@EmailContact, Ncc=@Ncc,
                    DateModification=@Now
                WHERE Id=@Id AND Actif=1", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Nom", req.Nom);
            cmd.Parameters.AddWithValue("@RaisonSociale", req.RaisonSociale ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", req.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Telephone", req.Telephone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Adresse", req.Adresse ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ville", req.Ville ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NomContact", req.NomContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TelephoneContact", req.TelephoneContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EmailContact", req.EmailContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ncc", req.Ncc ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteFournisseurAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "UPDATE Fournisseurs SET Actif=0, DateModification=@Now WHERE Id=@Id AND Actif=1", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // =============================================
        // DEVIS — LISTE ET DÉTAIL
        // =============================================

        public async Task<List<DevisFournisseurHeader>> GetDevisListAsync(
            string? statut = null, string? typeDevis = null)
        {
            var list = new List<DevisFournisseurHeader>();
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT df.*,
                       COUNT(DISTINCT dd.Id) AS NbDemandes,
                       COUNT(DISTINCT CASE WHEN dd.Statut = 'Repondu' THEN dd.Id END) AS NbReponses
                FROM DevisFournisseur df
                LEFT JOIN DevisFournisseurDemandes dd ON dd.DevisId = df.Id
                WHERE (@Statut   IS NULL OR df.Statut   = @Statut)
                  AND (@TypeDevis IS NULL OR df.TypeDevis = @TypeDevis)
                GROUP BY df.Id,df.Reference,df.TypeDevis,df.Titre,df.Description,
                         df.DateLimiteReponse,df.RemiseGlobalePct,df.RemiseGlobaleValeur,
                         df.Statut,df.DateCreation,df.DateModification,
                         df.UtilisateurCreation,df.UtilisateurModification
                ORDER BY df.DateCreation DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Statut", statut ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TypeDevis", typeDevis ?? (object)DBNull.Value);
            await conn.OpenAsync();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(MapToDevisHeader(r));
            return list;
        }

        public async Task<DevisFournisseurHeader?> GetDevisDetailAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // En-tête
            DevisFournisseurHeader? devis = null;
            using (var cmd = new SqlCommand("SELECT * FROM DevisFournisseur WHERE Id=@Id", conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) devis = MapToDevisHeader(r);
            }
            if (devis == null) return null;

            // Sections
            using (var cmd = new SqlCommand(
                "SELECT * FROM DevisFournisseurSections WHERE DevisId=@Id ORDER BY Ordre", conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) devis.Sections.Add(MapToSection(r));
            }

            // Lignes
            using (var cmd = new SqlCommand(
                "SELECT * FROM DevisFournisseurLignes WHERE DevisId=@Id ORDER BY SectionId, Ordre", conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var ligne = MapToLigne(r);
                    devis.Lignes.Add(ligne);
                    // Rattacher à la section si technique
                    if (ligne.SectionId.HasValue)
                    {
                        var section = devis.Sections.FirstOrDefault(s => s.Id == ligne.SectionId);
                        section?.Lignes.Add(ligne);
                    }
                }
            }

            // Demandes (sans OTP pour la sécurité)
            using (var cmd = new SqlCommand(@"
                SELECT dd.*, f.Nom AS FournisseurNom, f.Telephone AS FournisseurTelephone
                FROM DevisFournisseurDemandes dd
                INNER JOIN Fournisseurs f ON f.Id = dd.FournisseurId
                WHERE dd.DevisId=@Id ORDER BY dd.DateCreation", conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) devis.Demandes.Add(MapToDemande(r, masquerOtp: true));
            }

            return devis;
        }

        // =============================================
        // DEVIS — CRÉATION / MODIFICATION
        // =============================================

        public async Task<int> CreateDevisAsync(CreateDevisFournisseurRequest req, int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                // Générer la référence : DF-{YYYY}-{NNNN}
                var reference = await GenererReferenceAsync(conn, transaction);

                // Insérer l'en-tête
                int devisId;
                using (var cmd = new SqlCommand(@"
                    INSERT INTO DevisFournisseur
                        (Reference,TypeDevis,Titre,Description,DateLimiteReponse,
                         RemiseGlobalePct,RemiseGlobaleValeur,Statut,
                         DateCreation,DateModification,UtilisateurCreation,UtilisateurModification)
                    VALUES
                        (@Reference,@TypeDevis,@Titre,@Description,@DateLimiteReponse,
                         @RemiseGlobalePct,@RemiseGlobaleValeur,'Brouillon',
                         @Now,@Now,@UserId,@UserId);
                    SELECT CAST(SCOPE_IDENTITY() AS INT)", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Reference", reference);
                    cmd.Parameters.AddWithValue("@TypeDevis", req.TypeDevis);
                    cmd.Parameters.AddWithValue("@Titre", req.Titre);
                    cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateLimiteReponse", req.DateLimiteReponse);
                    cmd.Parameters.AddWithValue("@RemiseGlobalePct", req.RemiseGlobalePct);
                    cmd.Parameters.AddWithValue("@RemiseGlobaleValeur", req.RemiseGlobaleValeur);
                    cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    devisId = (int)await cmd.ExecuteScalarAsync();
                }

                // Sections (Technique uniquement)
                var sectionIdMap = new Dictionary<int, int>(); // index → Id BDD
                if (req.TypeDevis == "Technique")
                {
                    for (int i = 0; i < req.Sections.Count; i++)
                    {
                        var s = req.Sections[i];
                        int sectionId = await InsertSectionAsync(conn, transaction, devisId, s);
                        sectionIdMap[i] = sectionId;
                    }
                }

                // Lignes
                foreach (var ligne in req.Lignes)
                    await InsertLigneAsync(conn, transaction, devisId, ligne);

                transaction.Commit();
                return devisId;
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task<bool> UpdateDevisAsync(int id, UpdateDevisFournisseurRequest req, int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE DevisFournisseur SET
                    Titre=@Titre, Description=@Description,
                    DateLimiteReponse=@DateLimiteReponse,
                    RemiseGlobalePct=@RemiseGlobalePct,
                    RemiseGlobaleValeur=@RemiseGlobaleValeur,
                    DateModification=@Now,
                    UtilisateurModification=@UserId
                WHERE Id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Titre", req.Titre);
            cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateLimiteReponse", req.DateLimiteReponse);
            cmd.Parameters.AddWithValue("@RemiseGlobalePct", req.RemiseGlobalePct);
            cmd.Parameters.AddWithValue("@RemiseGlobaleValeur", req.RemiseGlobaleValeur);
            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> CloturerDevisAsync(int id, int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE DevisFournisseur SET
                    Statut='Cloture', DateModification=@Now, UtilisateurModification=@UserId
                WHERE Id=@Id AND Statut IN ('Brouillon','EnCours')", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // =============================================
        // SECTIONS (Technique)
        // =============================================

        public async Task<int> CreateSectionAsync(int devisId, CreateSectionRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            var id = await InsertSectionAsync(conn, tx, devisId, req);
            tx.Commit();
            return id;
        }

        public async Task<bool> UpdateSectionAsync(int sectionId, UpdateSectionRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE DevisFournisseurSections SET
                    Titre=@Titre, Description=@Description, Ordre=@Ordre,
                    RemiseSectionPct=@RemiseSectionPct, RemiseSectionValeur=@RemiseSectionValeur
                WHERE Id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", sectionId);
            cmd.Parameters.AddWithValue("@Titre", req.Titre);
            cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ordre", req.Ordre);
            cmd.Parameters.AddWithValue("@RemiseSectionPct", req.RemiseSectionPct);
            cmd.Parameters.AddWithValue("@RemiseSectionValeur", req.RemiseSectionValeur);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteSectionAsync(int sectionId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "DELETE FROM DevisFournisseurSections WHERE Id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", sectionId);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // =============================================
        // LIGNES
        // =============================================

        public async Task<int> CreateLigneAsync(int devisId, CreateLigneRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            var id = await InsertLigneAsync(conn, tx, devisId, req);
            tx.Commit();
            return id;
        }

        public async Task<bool> UpdateLigneAsync(int ligneId, UpdateLigneRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE DevisFournisseurLignes SET
                    SectionId=@SectionId, Ordre=@Ordre,
                    Designation=@Designation, Description=@Description,
                    Unite=@Unite, Quantite=@Quantite,
                    TypeElement=@TypeElement, DimensionL=@DimensionL, DimensionH=@DimensionH,
                    RemiseLignePct=@RemiseLignePct, RemiseLigneValeur=@RemiseLigneValeur
                WHERE Id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", ligneId);
            AddLigneParams(cmd, req);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteLigneAsync(int ligneId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "DELETE FROM DevisFournisseurLignes WHERE Id=@Id", conn);
            cmd.Parameters.AddWithValue("@Id", ligneId);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // =============================================
        // DEMANDES — ENVOI AUX FOURNISSEURS
        // =============================================

        public async Task<List<DevisFournisseurDemande>> EnvoyerDemandesAsync(
            int devisId, EnvoyerDemandesRequest req, int userId,
            string baseUrl, string messageTemplate)
        {
            var devis = await GetDevisDetailAsync(devisId)
                ?? throw new KeyNotFoundException("Devis introuvable");

            if (devis.Statut == "Cloture" || devis.Statut == "Selectionne")
                throw new InvalidOperationException("Ce devis est clôturé et n'accepte plus de demandes.");

            var demandes = new List<DevisFournisseurDemande>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                foreach (var fournisseurId in req.FournisseurIds.Distinct())
                {
                    // Vérifier si une demande existe déjà
                    int existingCount;
                    using (var chk = new SqlCommand(
                        "SELECT COUNT(*) FROM DevisFournisseurDemandes WHERE DevisId=@D AND FournisseurId=@F",
                        conn, tx))
                    {
                        chk.Parameters.AddWithValue("@D", devisId);
                        chk.Parameters.AddWithValue("@F", fournisseurId);
                        existingCount = (int)await chk.ExecuteScalarAsync();
                    }
                    if (existingCount > 0) continue;

                    // Récupérer le fournisseur
                    Fournisseur? four = null;
                    using (var fc = new SqlCommand(
                        "SELECT * FROM Fournisseurs WHERE Id=@Id AND Actif=1", conn, tx))
                    {
                        fc.Parameters.AddWithValue("@Id", fournisseurId);
                        using var fr = await fc.ExecuteReaderAsync();
                        if (await fr.ReadAsync()) four = MapToFournisseur(fr);
                    }
                    if (four == null) continue;

                    var token = Guid.NewGuid();
                    var otp = GenererOtp();
                    var expiration = DateTime.UtcNow.AddHours(req.DureeValiditeHeures);
                    var lienDevis = $"{baseUrl}/devis-fournisseur/public/{token}";

                    // Construire le message WhatsApp
                    var message = messageTemplate
                        .Replace("{NOM_CONTACT}", four.NomContact ?? four.Nom)
                        .Replace("{NOM_ENTREPRISE}", "SAF-ALU")
                        .Replace("{REFERENCE_DEMANDE}", devis.Reference)
                        .Replace("{DATE_DEMANDE}", DateTime.Now.ToString("dd/MM/yyyy"))
                        .Replace("{NOM_DEMANDEUR}", "SAF-ALU")
                        .Replace("{LIEN_DEVIS}", lienDevis)
                        .Replace("{DESCRIPTION_DEMANDE}", devis.Description ?? devis.Titre)
                        .Replace("{DATE_LIMITE}", devis.DateLimiteReponse.ToString("dd/MM/yyyy"))
                        .Replace("{TELEPHONE_ENTREPRISE}", "")
                        + $"\n\n🔑 *Votre code OTP : {otp}*";

                    // Insérer la demande
                    int demandeId;
                    using (var ins = new SqlCommand(@"
                        INSERT INTO DevisFournisseurDemandes
                            (DevisId,FournisseurId,Token,Otp,DateExpiration,
                             NbTentativesOtp,Statut,MessageWhatsApp,DateEnvoi,DateCreation)
                        VALUES
                            (@DevisId,@FournisseurId,@Token,@Otp,@DateExpiration,
                             0,'EnAttente',@Message,@Now,@Now);
                        SELECT CAST(SCOPE_IDENTITY() AS INT)", conn, tx))
                    {
                        ins.Parameters.AddWithValue("@DevisId", devisId);
                        ins.Parameters.AddWithValue("@FournisseurId", fournisseurId);
                        ins.Parameters.AddWithValue("@Token", token);
                        ins.Parameters.AddWithValue("@Otp", otp);
                        ins.Parameters.AddWithValue("@DateExpiration", expiration);
                        ins.Parameters.AddWithValue("@Message", message);
                        ins.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                        demandeId = (int)await ins.ExecuteScalarAsync();
                    }

                    demandes.Add(new DevisFournisseurDemande
                    {
                        Id = demandeId,
                        DevisId = devisId,
                        FournisseurId = fournisseurId,
                        FournisseurNom = four.Nom,
                        FournisseurTelephone = four.Telephone ?? four.TelephoneContact,
                        Token = token,
                        Otp = otp,      // Retourné UNE SEULE FOIS pour l'envoi WhatsApp
                        DateExpiration = expiration,
                        Statut = "EnAttente",
                        MessageWhatsApp = message,
                        DateCreation = DateTime.UtcNow
                    });
                }

                // Passer le devis en EnCours
                using (var upd = new SqlCommand(@"
                    UPDATE DevisFournisseur SET Statut='EnCours', DateModification=@Now
                    WHERE Id=@Id AND Statut='Brouillon'", conn, tx))
                {
                    upd.Parameters.AddWithValue("@Id", devisId);
                    upd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                    await upd.ExecuteNonQueryAsync();
                }

                tx.Commit();
                return demandes;
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task<bool> AnnulerDemandeAsync(int demandeId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                DELETE FROM DevisFournisseurDemandes
                WHERE Id=@Id AND Statut='EnAttente'", conn);
            cmd.Parameters.AddWithValue("@Id", demandeId);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // =============================================
        // ACCÈS PUBLIC — FOURNISSEUR
        // =============================================

        public async Task<(DevisPublicDTO? Devis, string? Erreur)> GetDevisPublicAsync(Guid token)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Récupérer la demande par token
            DevisFournisseurDemande? demande = null;
            using (var cmd = new SqlCommand(@"
                SELECT dd.*, f.Nom AS FournisseurNom, f.Telephone AS FournisseurTelephone
                FROM DevisFournisseurDemandes dd
                INNER JOIN Fournisseurs f ON f.Id = dd.FournisseurId
                WHERE dd.Token = @Token", conn))
            {
                cmd.Parameters.AddWithValue("@Token", token);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) demande = MapToDemande(r, masquerOtp: false);
            }

            if (demande == null) return (null, "Lien invalide ou introuvable.");
            if (demande.DateExpiration < DateTime.UtcNow)
            {
                await SetDemandeStatutAsync(conn, null, demande.Id, "Expire");
                return (null, "Ce lien a expiré. Contactez votre interlocuteur SAF-ALU.");
            }
            if (demande.Statut == "Rejete")
                return (null, "L'accès à ce formulaire a été bloqué suite à trop de tentatives erronées.");

            // Marquer comme ouvert si premier accès
            if (demande.Statut == "EnAttente")
            {
                await SetDemandeStatutAsync(conn, null, demande.Id, "LienOuvert",
                    extra: "DateOuvertureLien=@Now");
            }

            // Charger le devis public
            var dto = await BuildDevisPublicDTOAsync(conn, demande);
            return (dto, null);
        }

        public async Task<(bool Ok, string Message)> ValiderOtpAsync(Guid token, string otp)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            DevisFournisseurDemande? demande = null;
            using (var cmd = new SqlCommand(
                "SELECT * FROM DevisFournisseurDemandes WHERE Token=@Token", conn))
            {
                cmd.Parameters.AddWithValue("@Token", token);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) demande = MapToDemandePubic(r, masquerOtp: false);
            }

            if (demande == null) return (false, "Lien invalide.");
            if (demande.DateExpiration < DateTime.UtcNow) return (false, "Lien expiré.");
            if (demande.Statut == "Rejete") return (false, "Accès bloqué.");
            if (demande.Statut is "OtpValide" or "Repondu") return (true, "OTP déjà validé.");

            if (demande.Otp != otp.Trim())
            {
                var tentatives = demande.NbTentativesOtp + 1;
                var bloque = tentatives >= 3;
                using var upd = new SqlCommand(@"
                    UPDATE DevisFournisseurDemandes SET
                        NbTentativesOtp=@T,
                        Statut=@Statut
                    WHERE Id=@Id", conn);
                upd.Parameters.AddWithValue("@T", tentatives);
                upd.Parameters.AddWithValue("@Statut", bloque ? "Rejete" : demande.Statut);
                upd.Parameters.AddWithValue("@Id", demande.Id);
                await upd.ExecuteNonQueryAsync();

                return bloque
                    ? (false, "Accès bloqué après 3 tentatives erronées.")
                    : (false, $"Code OTP incorrect. {3 - tentatives} tentative(s) restante(s).");
            }

            // OTP correct
            using (var upd = new SqlCommand(@"
                UPDATE DevisFournisseurDemandes SET
                    Statut='OtpValide', OtpValideA=@Now
                WHERE Id=@Id", conn))
            {
                upd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                upd.Parameters.AddWithValue("@Id", demande.Id);
                await upd.ExecuteNonQueryAsync();
            }

            return (true, "OTP validé avec succès.");
        }

        public async Task<(bool Ok, string Message)> SoumettreReponsesAsync(
            Guid token, SoumettreReponsesRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier la demande
            DevisFournisseurDemande? demande = null;
            using (var cmd = new SqlCommand(
                "SELECT * FROM DevisFournisseurDemandes WHERE Token=@Token", conn))
            {
                cmd.Parameters.AddWithValue("@Token", token);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) demande = MapToDemandePubic(r, masquerOtp: false);
            }

            if (demande == null) return (false, "Lien invalide.");
            if (demande.DateExpiration < DateTime.UtcNow) return (false, "Lien expiré.");
            if (demande.Statut is not ("OtpValide" or "Repondu"))
                return (false, "Veuillez valider votre OTP avant de soumettre vos prix.");

            using var tx = conn.BeginTransaction();
            try
            {
                foreach (var rep in req.Reponses)
                {
                    // UPSERT : insert si absent, update si déjà saisi
                    using var upsert = new SqlCommand(@"
                        MERGE DevisFournisseurLignesReponses AS t
                        USING (SELECT @DemandeId AS DemandeId, @LigneId AS LigneId) AS s
                            ON t.DemandeId = s.DemandeId AND t.LigneId = s.LigneId
                        WHEN MATCHED THEN
                            UPDATE SET PrixUnitaire=@Prix, Commentaire=@Commentaire, DateSaisie=@Now
                        WHEN NOT MATCHED THEN
                            INSERT (DemandeId,LigneId,PrixUnitaire,Commentaire,DateSaisie)
                            VALUES (@DemandeId,@LigneId,@Prix,@Commentaire,@Now);", conn, tx);

                    upsert.Parameters.AddWithValue("@DemandeId", demande.Id);
                    upsert.Parameters.AddWithValue("@LigneId", rep.LigneId);
                    upsert.Parameters.AddWithValue("@Prix", rep.PrixUnitaire);
                    upsert.Parameters.AddWithValue("@Commentaire", rep.Commentaire ?? (object)DBNull.Value);
                    upsert.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                    await upsert.ExecuteNonQueryAsync();
                }

                // Marquer comme Repondu
                using var upd = new SqlCommand(@"
                    UPDATE DevisFournisseurDemandes SET
                        Statut='Repondu', DateReponse=@Now
                    WHERE Id=@Id", conn, tx);
                upd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                upd.Parameters.AddWithValue("@Id", demande.Id);
                await upd.ExecuteNonQueryAsync();

                tx.Commit();
                return (true, "Vos prix ont été soumis avec succès. Merci !");
            }
            catch { tx.Rollback(); throw; }
        }

        // =============================================
        // COMPARAISON ET SÉLECTION
        // =============================================

        public async Task<ComparaisonDevisDTO?> GetComparaisonAsync(int devisId)
        {
            var devis = await GetDevisDetailAsync(devisId);
            if (devis == null) return null;

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var dto = new ComparaisonDevisDTO
            {
                DevisId = devis.Id,
                Reference = devis.Reference,
                Titre = devis.Titre,
                TypeDevis = devis.TypeDevis,
            };

            // Charger la vue de comparaison
            var rows = new List<(
                int LigneId, int Ordre, string Designation, string? Unite,
                decimal Quantite, string? TypeElement, decimal? DimL, decimal? DimH,
                int? SectionId, string? SectionTitre,
                int DemandeId, int FournisseurId, string FournisseurNom,
                decimal PrixUnitaire, decimal MontantBrut, decimal MontantNet,
                string? Commentaire, bool LigneSelectionnee, int Rang,
                bool FournisseurSelectionne
            )>();

            using var cmd = new SqlCommand(
                "SELECT * FROM vw_ComparaisonDevisFournisseur WHERE DevisId=@DevisId", conn);
            cmd.Parameters.AddWithValue("@DevisId", devisId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                rows.Add((
                    r.GetInt32("LigneId"), r.GetInt32("Ordre"),
                    r.GetString("Designation"),
                    r.IsDBNull("Unite") ? null : r.GetString("Unite"),
                    r.GetDecimal("Quantite"),
                    r.IsDBNull("TypeElement") ? null : r.GetString("TypeElement"),
                    r.IsDBNull("DimensionL") ? null : r.GetDecimal("DimensionL"),
                    r.IsDBNull("DimensionH") ? null : r.GetDecimal("DimensionH"),
                    r.IsDBNull("SectionId") ? null : r.GetInt32("SectionId"),
                    r.IsDBNull("SectionTitre") ? null : r.GetString("SectionTitre"),
                    r.GetInt32("DemandeId"), r.GetInt32("FournisseurId"),
                    r.GetString("FournisseurNom"),
                    r.IsDBNull("PrixUnitaire") ? 0 : r.GetDecimal("PrixUnitaire"),
                    r.IsDBNull("MontantBrut") ? 0 : r.GetDecimal("MontantBrut"),
                    r.IsDBNull("MontantNetLigne") ? 0 : r.GetDecimal("MontantNetLigne"),
                    r.IsDBNull("CommentaireLigne") ? null : r.GetString("CommentaireLigne"),
                    !r.IsDBNull("LigneSelectionnee") && r.GetBoolean("LigneSelectionnee"),
                    r.IsDBNull("RangPrixLigne") ? 0 : (int)r.GetInt64("RangPrixLigne"),
                    !r.IsDBNull("FournisseurSelectionne") && r.GetBoolean("FournisseurSelectionne")

                ));
            }

            // Fournisseurs distincts ayant répondu
            var fournisseurIds = rows.Select(x => (x.DemandeId, x.FournisseurId, x.FournisseurNom,
                                                   x.FournisseurSelectionne))
                                     .Distinct().ToList();

            dto.NombreFournisseursAyantRepondu = fournisseurIds.Select(x => x.FournisseurId).Distinct().Count();

            // Totaux par fournisseur
            foreach (var (demandeId, fourId, fourNom, selectionne)
                     in fournisseurIds.DistinctBy(x => x.DemandeId))
            {
                dto.TotauxParFournisseur.Add(new FournisseurTotalDTO
                {
                    DemandeId = demandeId,
                    FournisseurId = fourId,
                    FournisseurNom = fourNom,
                    TotalBrut = rows.Where(x => x.DemandeId == demandeId).Sum(x => x.MontantBrut),
                    TotalNet = rows.Where(x => x.DemandeId == demandeId).Sum(x => x.MontantNet),
                    Selectionne = selectionne,
                });
            }

            // Lignes groupées
            var lignesGrouped = rows.GroupBy(x => x.LigneId);
            foreach (var ligneGroup in lignesGrouped.OrderBy(g => g.First().Ordre))
            {
                var first = ligneGroup.First();
                var ligneDto = new ComparaisonLigneDTO
                {
                    LigneId = first.LigneId,
                    Ordre = first.Ordre,
                    Designation = first.Designation,
                    Unite = first.Unite,
                    Quantite = first.Quantite,
                    TypeElement = first.TypeElement,
                    DimensionL = first.DimL,
                    DimensionH = first.DimH,
                    Offres = ligneGroup.Select(x => new OffreLigneDTO
                    {
                        DemandeId = x.DemandeId,
                        FournisseurId = x.FournisseurId,
                        FournisseurNom = x.FournisseurNom,
                        PrixUnitaire = x.PrixUnitaire,
                        MontantBrut = x.MontantBrut,
                        MontantNet = x.MontantNet,
                        Commentaire = x.Commentaire,
                        RangPrix = x.Rang,
                        LigneSelectionnee = x.LigneSelectionnee
                    }).OrderBy(o => o.RangPrix).ToList()
                };

                dto.Lignes.Add(ligneDto);

                // Regrouper par section si Technique
                if (first.SectionId.HasValue)
                {
                    var section = dto.Sections.FirstOrDefault(s => s.SectionId == first.SectionId);
                    if (section == null)
                    {
                        section = new SectionComparaisonDTO
                        {
                            SectionId = first.SectionId.Value,
                            SectionTitre = first.SectionTitre ?? "",
                        };
                        dto.Sections.Add(section);
                    }
                    section.Lignes.Add(ligneDto);
                }
            }

            return dto;
        }

        public async Task SelectionnerFournisseurAsync(int devisId, SelectionnerFournisseurRequest req, int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                // Désélectionner tous les fournisseurs de ce devis
                using (var clr = new SqlCommand(
                    "UPDATE DevisFournisseurDemandes SET Selectionne=0 WHERE DevisId=@DevisId",
                    conn, tx))
                {
                    clr.Parameters.AddWithValue("@DevisId", devisId);
                    await clr.ExecuteNonQueryAsync();
                }

                // Sélectionner le fournisseur choisi
                using (var sel = new SqlCommand(@"
                    UPDATE DevisFournisseurDemandes SET
                        Selectionne=1, DateSelection=@Now, CommentaireSelection=@Comment
                    WHERE Id=@DemandeId AND DevisId=@DevisId", conn, tx))
                {
                    sel.Parameters.AddWithValue("@DevisId", devisId);
                    sel.Parameters.AddWithValue("@DemandeId", req.DemandeId);
                    sel.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                    sel.Parameters.AddWithValue("@Comment", req.Commentaire ?? (object)DBNull.Value);
                    await sel.ExecuteNonQueryAsync();
                }

                // Passer le devis en Selectionne
                using (var upd = new SqlCommand(@"
                    UPDATE DevisFournisseur SET
                        Statut='Selectionne', DateModification=@Now, UtilisateurModification=@UserId
                    WHERE Id=@DevisId", conn, tx))
                {
                    upd.Parameters.AddWithValue("@DevisId", devisId);
                    upd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                    upd.Parameters.AddWithValue("@UserId", userId);
                    await upd.ExecuteNonQueryAsync();
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task SelectionnerLignesAsync(int devisId, SelectionnerLignesRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                foreach (var (ligneId, demandeId) in req.SelectionParLigne)
                {
                    // Désélectionner toutes les réponses de cette ligne
                    using (var clr = new SqlCommand(@"
                        UPDATE DevisFournisseurLignesReponses
                        SET LigneSelectionnee=0
                        WHERE LigneId=@LigneId
                          AND DemandeId IN (
                              SELECT Id FROM DevisFournisseurDemandes WHERE DevisId=@DevisId)", conn, tx))
                    {
                        clr.Parameters.AddWithValue("@LigneId", ligneId);
                        clr.Parameters.AddWithValue("@DevisId", devisId);
                        await clr.ExecuteNonQueryAsync();
                    }

                    // Sélectionner la réponse du fournisseur choisi
                    using (var sel = new SqlCommand(@"
                        UPDATE DevisFournisseurLignesReponses
                        SET LigneSelectionnee=1
                        WHERE LigneId=@LigneId AND DemandeId=@DemandeId", conn, tx))
                    {
                        sel.Parameters.AddWithValue("@LigneId", ligneId);
                        sel.Parameters.AddWithValue("@DemandeId", demandeId);
                        await sel.ExecuteNonQueryAsync();
                    }
                }
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        // =============================================
        // MÉTHODES PRIVÉES HELPERS
        // =============================================

        private static string GenererOtp()
            => Random.Shared.Next(100000, 999999).ToString();

        private static async Task<string> GenererReferenceAsync(
            SqlConnection conn, SqlTransaction tx)
        {
            var annee = DateTime.Now.Year;
            using var cmd = new SqlCommand(@"
                SELECT ISNULL(MAX(CAST(RIGHT(Reference,4) AS INT)),0) + 1
                FROM DevisFournisseur
                WHERE Reference LIKE 'DF-' + CAST(@Annee AS NVARCHAR) + '-%'",
                conn, tx);
            cmd.Parameters.AddWithValue("@Annee", annee);
            var seq = (int)await cmd.ExecuteScalarAsync();
            return $"DF-{annee}-{seq:D4}";
        }

        private static async Task<int> InsertSectionAsync(
            SqlConnection conn, SqlTransaction tx, int devisId, CreateSectionRequest req)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO DevisFournisseurSections
                    (DevisId,Ordre,Titre,Description,RemiseSectionPct,RemiseSectionValeur)
                VALUES
                    (@DevisId,@Ordre,@Titre,@Description,@RemiseSectionPct,@RemiseSectionValeur);
                SELECT CAST(SCOPE_IDENTITY() AS INT)", conn, tx);
            cmd.Parameters.AddWithValue("@DevisId", devisId);
            cmd.Parameters.AddWithValue("@Ordre", req.Ordre);
            cmd.Parameters.AddWithValue("@Titre", req.Titre);
            cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RemiseSectionPct", req.RemiseSectionPct);
            cmd.Parameters.AddWithValue("@RemiseSectionValeur", req.RemiseSectionValeur);
            return (int)await cmd.ExecuteScalarAsync();
        }

        private static async Task<int> InsertLigneAsync(
            SqlConnection conn, SqlTransaction tx, int devisId, CreateLigneRequest req)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO DevisFournisseurLignes
                    (DevisId,SectionId,Ordre,Designation,Description,Unite,Quantite,
                     TypeElement,DimensionL,DimensionH,RemiseLignePct,RemiseLigneValeur)
                VALUES
                    (@DevisId,@SectionId,@Ordre,@Designation,@Description,@Unite,@Quantite,
                     @TypeElement,@DimensionL,@DimensionH,@RemiseLignePct,@RemiseLigneValeur);
                SELECT CAST(SCOPE_IDENTITY() AS INT)", conn, tx);
            cmd.Parameters.AddWithValue("@DevisId", devisId);
            AddLigneParams(cmd, req);
            return (int)await cmd.ExecuteScalarAsync();
        }

        private static void AddLigneParams(SqlCommand cmd, CreateLigneRequest req)
        {
            cmd.Parameters.AddWithValue("@SectionId", req.SectionId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ordre", req.Ordre);
            cmd.Parameters.AddWithValue("@Designation", req.Designation);
            cmd.Parameters.AddWithValue("@Description", req.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Unite", req.Unite ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Quantite", req.Quantite);
            cmd.Parameters.AddWithValue("@TypeElement", req.TypeElement ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DimensionL", req.DimensionL ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DimensionH", req.DimensionH ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RemiseLignePct", req.RemiseLignePct);
            cmd.Parameters.AddWithValue("@RemiseLigneValeur", req.RemiseLigneValeur);
        }

        private static void AddFournisseurParams(SqlCommand cmd, Fournisseur f)
        {
            cmd.Parameters.AddWithValue("@Nom", f.Nom);
            cmd.Parameters.AddWithValue("@RaisonSociale", f.RaisonSociale ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", f.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Telephone", f.Telephone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Adresse", f.Adresse ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ville", f.Ville ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NomContact", f.NomContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TelephoneContact", f.TelephoneContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EmailContact", f.EmailContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ncc", f.Ncc ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", f.UtilisateurCreation);
        }

        private static async Task SetDemandeStatutAsync(
            SqlConnection conn, SqlTransaction? tx, int demandeId, string statut,
            string extra = "")
        {
            var extraSql = string.IsNullOrEmpty(extra) ? "" : $", {extra}";
            var sql = $"UPDATE DevisFournisseurDemandes SET Statut=@Statut{extraSql} WHERE Id=@Id";
            using var cmd = tx != null
                ? new SqlCommand(sql, conn, tx)
                : new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Statut", statut);
            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@Id", demandeId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<DevisPublicDTO> BuildDevisPublicDTOAsync(
            SqlConnection conn, DevisFournisseurDemande demande)
        {
            var dto = new DevisPublicDTO
            {
                FournisseurNom = demande.FournisseurNom ?? "",
                DejaRepondu = demande.Statut == "Repondu",
            };

            using (var cmd = new SqlCommand(
                "SELECT * FROM DevisFournisseur WHERE Id=@Id", conn))
            {
                cmd.Parameters.AddWithValue("@Id", demande.DevisId);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    dto.Id = r.GetInt32("Id");
                    dto.Reference = r.GetString("Reference");
                    dto.TypeDevis = r.GetString("TypeDevis");
                    dto.Titre = r.GetString("Titre");
                    dto.Description = r.IsDBNull("Description") ? null : r.GetString("Description");
                    dto.DateLimiteReponse = r.GetDateTime("DateLimiteReponse");
                }
            }

            // Sections si Technique
            if (dto.TypeDevis == "Technique")
            {
                using var sc = new SqlCommand(
                    "SELECT * FROM DevisFournisseurSections WHERE DevisId=@Id ORDER BY Ordre", conn);
                sc.Parameters.AddWithValue("@Id", dto.Id);
                using var sr = await sc.ExecuteReaderAsync();
                while (await sr.ReadAsync())
                {
                    dto.Sections.Add(new SectionPublicDTO
                    {
                        Id = sr.GetInt32("Id"),
                        Ordre = sr.GetInt32("Ordre"),
                        Titre = sr.GetString("Titre"),
                        Description = sr.IsDBNull("Description") ? null : sr.GetString("Description"),
                    });
                }
            }

            // Lignes + réponses déjà saisies
            var reponsesExistantes = new Dictionary<int, (decimal Prix, string? Commentaire)>();
            using (var rc = new SqlCommand(
                "SELECT * FROM DevisFournisseurLignesReponses WHERE DemandeId=@DemandeId", conn))
            {
                rc.Parameters.AddWithValue("@DemandeId", demande.Id);
                using var rr = await rc.ExecuteReaderAsync();
                while (await rr.ReadAsync())
                    reponsesExistantes[rr.GetInt32("LigneId")] = (
                        rr.GetDecimal("PrixUnitaire"),
                        rr.IsDBNull("Commentaire") ? null : rr.GetString("Commentaire"));
            }

            using (var lc = new SqlCommand(
                "SELECT * FROM DevisFournisseurLignes WHERE DevisId=@Id ORDER BY SectionId,Ordre", conn))
            {
                lc.Parameters.AddWithValue("@Id", dto.Id);
                using var lr = await lc.ExecuteReaderAsync();
                while (await lr.ReadAsync())
                {
                    int ligneId = lr.GetInt32("Id");
                    int? sectionId = lr.IsDBNull("SectionId") ? null : lr.GetInt32("SectionId");

                    reponsesExistantes.TryGetValue(ligneId, out var rep);
                    var ligneDto = new LignePublicDTO
                    {
                        Id = ligneId,
                        SectionId = sectionId,
                        Ordre = lr.GetInt32("Ordre"),
                        Designation = lr.GetString("Designation"),
                        Description = lr.IsDBNull("Description") ? null : lr.GetString("Description"),
                        Unite = lr.IsDBNull("Unite") ? null : lr.GetString("Unite"),
                        Quantite = lr.GetDecimal("Quantite"),
                        TypeElement = lr.IsDBNull("TypeElement") ? null : lr.GetString("TypeElement"),
                        DimensionL = lr.IsDBNull("DimensionL") ? null : lr.GetDecimal("DimensionL"),
                        DimensionH = lr.IsDBNull("DimensionH") ? null : lr.GetDecimal("DimensionH"),
                        PrixUnitaireSaisi = rep.Prix > 0 ? rep.Prix : null,
                        CommentaireSaisi = rep.Commentaire,
                    };
                    dto.Lignes.Add(ligneDto);

                    if (sectionId.HasValue)
                    {
                        var sec = dto.Sections.FirstOrDefault(s => s.Id == sectionId);
                        sec?.Lignes.Add(ligneDto);
                    }
                }
            }

            return dto;
        }

        // ── MapTo* ──────────────────────────────────────────────────────

        private static Fournisseur MapToFournisseur(SqlDataReader r) => new()
        {
            Id = r.GetInt32("Id"),
            Nom = r.GetString("Nom"),
            RaisonSociale = r.IsDBNull("RaisonSociale") ? null : r.GetString("RaisonSociale"),
            Email = r.IsDBNull("Email") ? null : r.GetString("Email"),
            Telephone = r.IsDBNull("Telephone") ? null : r.GetString("Telephone"),
            Adresse = r.IsDBNull("Adresse") ? null : r.GetString("Adresse"),
            Ville = r.IsDBNull("Ville") ? null : r.GetString("Ville"),
            NomContact = r.IsDBNull("NomContact") ? null : r.GetString("NomContact"),
            TelephoneContact = r.IsDBNull("TelephoneContact") ? null : r.GetString("TelephoneContact"),
            EmailContact = r.IsDBNull("EmailContact") ? null : r.GetString("EmailContact"),
            Ncc = r.IsDBNull("Ncc") ? null : r.GetString("Ncc"),
            Actif = r.GetBoolean("Actif"),
            DateCreation = r.GetDateTime("DateCreation"),
            DateModification = r.GetDateTime("DateModification"),
            UtilisateurCreation = r.GetInt32("UtilisateurCreation"),
        };

        private static DevisFournisseurHeader MapToDevisHeader(SqlDataReader r) 
        {
            var header = new DevisFournisseurHeader
            {
                Id = r.GetInt32("Id"),
            Reference = r.GetString("Reference"),
            TypeDevis = r.GetString("TypeDevis"),
            Titre = r.GetString("Titre"),
            Description = r.IsDBNull("Description") ? null : r.GetString("Description"),
            DateLimiteReponse = r.GetDateTime("DateLimiteReponse"),
            RemiseGlobalePct = r.GetDecimal("RemiseGlobalePct"),
            RemiseGlobaleValeur = r.GetDecimal("RemiseGlobaleValeur"),
            Statut = r.GetString("Statut"),
            DateCreation = r.GetDateTime("DateCreation"),
            DateModification = r.GetDateTime("DateModification"),
            UtilisateurCreation = r.GetInt32("UtilisateurCreation"),
            UtilisateurModification = r.GetInt32("UtilisateurModification"),

            };

            // 🆕 Lire les agrégats — présents uniquement dans GetDevisListAsync
            try { header.NbDemandes = r.GetInt32("NbDemandes"); } catch { }
            try { header.NbReponses = r.GetInt32("NbReponses"); } catch { }

            return header;
        }

        private static DevisFournisseurSection MapToSection(SqlDataReader r) => new()
        {
            Id = r.GetInt32("Id"),
            DevisId = r.GetInt32("DevisId"),
            Ordre = r.GetInt32("Ordre"),
            Titre = r.GetString("Titre"),
            Description = r.IsDBNull("Description") ? null : r.GetString("Description"),
            RemiseSectionPct = r.GetDecimal("RemiseSectionPct"),
            RemiseSectionValeur = r.GetDecimal("RemiseSectionValeur"),
        };

        private static DevisFournisseurLigne MapToLigne(SqlDataReader r) => new()
        {
            Id = r.GetInt32("Id"),
            DevisId = r.GetInt32("DevisId"),
            SectionId = r.IsDBNull("SectionId") ? null : r.GetInt32("SectionId"),
            Ordre = r.GetInt32("Ordre"),
            Designation = r.GetString("Designation"),
            Description = r.IsDBNull("Description") ? null : r.GetString("Description"),
            Unite = r.IsDBNull("Unite") ? null : r.GetString("Unite"),
            Quantite = r.GetDecimal("Quantite"),
            TypeElement = r.IsDBNull("TypeElement") ? null : r.GetString("TypeElement"),
            DimensionL = r.IsDBNull("DimensionL") ? null : r.GetDecimal("DimensionL"),
            DimensionH = r.IsDBNull("DimensionH") ? null : r.GetDecimal("DimensionH"),
            RemiseLignePct = r.GetDecimal("RemiseLignePct"),
            RemiseLigneValeur = r.GetDecimal("RemiseLigneValeur"),
        };

        private static DevisFournisseurDemande MapToDemande(SqlDataReader r, bool masquerOtp) => new()
        {
            Id = r.GetInt32("Id"),
            DevisId = r.GetInt32("DevisId"),
            FournisseurId = r.GetInt32("FournisseurId"),
            FournisseurNom = r.IsDBNull("FournisseurNom") ? null : r.GetString("FournisseurNom"),
            FournisseurTelephone = r.IsDBNull("FournisseurTelephone") ? null : r.GetString("FournisseurTelephone"),
            Token = r.GetGuid("Token"),
            Otp = masquerOtp ? "******" : r.GetString("Otp"),
            DateExpiration = r.GetDateTime("DateExpiration"),
            NbTentativesOtp = r.GetInt32("NbTentativesOtp"),
            OtpValideA = r.IsDBNull("OtpValideA") ? null : r.GetDateTime("OtpValideA"),
            Statut = r.GetString("Statut"),
            MessageWhatsApp = r.IsDBNull("MessageWhatsApp") ? null : r.GetString("MessageWhatsApp"),
            DateEnvoi = r.IsDBNull("DateEnvoi") ? null : r.GetDateTime("DateEnvoi"),
            DateOuvertureLien = r.IsDBNull("DateOuvertureLien") ? null : r.GetDateTime("DateOuvertureLien"),
            DateReponse = r.IsDBNull("DateReponse") ? null : r.GetDateTime("DateReponse"),
            Selectionne = r.GetBoolean("Selectionne"),
            DateSelection = r.IsDBNull("DateSelection") ? null : r.GetDateTime("DateSelection"),
            CommentaireSelection = r.IsDBNull("CommentaireSelection") ? null : r.GetString("CommentaireSelection"),
            DateCreation = r.GetDateTime("DateCreation"),
        };
        private static DevisFournisseurDemande MapToDemandePubic(SqlDataReader r, bool masquerOtp) => new()
        {
            Id = r.GetInt32("Id"),
            DevisId = r.GetInt32("DevisId"),
            FournisseurId = r.GetInt32("FournisseurId"),
            //FournisseurNom = r.IsDBNull("FournisseurNom") ? null : r.GetString("FournisseurNom"),
            //FournisseurTelephone = r.IsDBNull("FournisseurTelephone") ? null : r.GetString("FournisseurTelephone"),
            Token = r.GetGuid("Token"),
            Otp = masquerOtp ? "******" : r.GetString("Otp"),
            DateExpiration = r.GetDateTime("DateExpiration"),
            NbTentativesOtp = r.GetInt32("NbTentativesOtp"),
            OtpValideA = r.IsDBNull("OtpValideA") ? null : r.GetDateTime("OtpValideA"),
            Statut = r.GetString("Statut"),
            MessageWhatsApp = r.IsDBNull("MessageWhatsApp") ? null : r.GetString("MessageWhatsApp"),
            DateEnvoi = r.IsDBNull("DateEnvoi") ? null : r.GetDateTime("DateEnvoi"),
            DateOuvertureLien = r.IsDBNull("DateOuvertureLien") ? null : r.GetDateTime("DateOuvertureLien"),
            DateReponse = r.IsDBNull("DateReponse") ? null : r.GetDateTime("DateReponse"),
            Selectionne = r.GetBoolean("Selectionne"),
            DateSelection = r.IsDBNull("DateSelection") ? null : r.GetDateTime("DateSelection"),
            CommentaireSelection = r.IsDBNull("CommentaireSelection") ? null : r.GetString("CommentaireSelection"),
            DateCreation = r.GetDateTime("DateCreation"),
        };

    }
}