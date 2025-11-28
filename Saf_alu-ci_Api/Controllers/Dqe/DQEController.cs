using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saf_alu_ci_Api.Controllers.Dqe;
using Saf_alu_ci_Api.Controllers.Projets;

namespace Saf_alu_ci_Api.Controllers.Dqe
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DQEController : BaseController
    {
        private readonly DQEService _dqeService;
        private readonly ConversionService _conversionService;
        private readonly ProjetService _projetService;
        private readonly DQEExportService _dqeExportService;


        public DQEController(
            DQEService dqeService,
            ConversionService conversionService, ProjetService projetService, DQEExportService dqeExportService)
        {
            _dqeService = dqeService;
            _conversionService = conversionService;
            _projetService = projetService;
            _dqeExportService = dqeExportService;
            _dqeExportService = dqeExportService;
        }

        // ========================================
        // ENDPOINTS CRUD DE BASE
        // ========================================

        /// <summary>
        /// Récupère tous les DQE avec filtres optionnels
        /// GET /api/dqe
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? statut = null,
            [FromQuery] bool? isConverted = null)
        {
            try
            {
                var dqeList = await _dqeService.GetAllAsync(statut, isConverted);
                return Ok(dqeList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Récupère un DQE par son ID avec structure complète
        /// GET /api/dqe/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var dqe = await _dqeService.GetByIdAsync(id);

                if (dqe == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                return Ok(dqe);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Crée un nouveau DQE
        /// POST /api/dqe
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDQERequest request)
        {
            try
            {
                // TODO: Récupérer l'utilisateur depuis JWT
                var utilisateurId = Convert.ToInt32(GetCurrentUserId());

                var dqeId = await _dqeService.CreateAsync(request, utilisateurId);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = dqeId },
                    new { message = "DQE créé avec succès", id = dqeId }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Met à jour un DQE existant
        /// PUT /api/dqe/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDQERequest request)
        {
            try
            {
                var existing = await _dqeService.GetByIdAsync(id);
                if (existing == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                // Vérifier que le DQE n'est pas converti (lecture seule)
                if (existing.IsConverted)
                {
                    return BadRequest(new { message = "Impossible de modifier un DQE déjà converti en projet" });
                }

                await _dqeService.UpdateAsync(id, request);

                return Ok(new { message = "DQE mis à jour avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Supprime un DQE (soft delete)
        /// DELETE /api/dqe/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existing = await _dqeService.GetByIdAsync(id);
                if (existing == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                // Vérifier que le DQE n'est pas converti
                if (existing.IsConverted)
                {
                    return BadRequest(new { message = "Impossible de supprimer un DQE déjà converti en projet" });
                }

                await _dqeService.DeleteAsync(id);

                return Ok(new { message = "DQE supprimé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // ========================================
        // ENDPOINTS DE VALIDATION
        // ========================================

        /// <summary>
        /// Valide un DQE (changement de statut)
        /// POST /api/dqe/{id}/validate
        /// </summary>
        [HttpPost("{id}/validate")]
        public async Task<IActionResult> Validate(int id, [FromBody] ValidateDQERequest? request = null)
        {
            try
            {
                var existing = await _dqeService.GetByIdAsync(id);
                var dqe = await _dqeService.GetByIdAsync(id);
                bool DebourseSecOk = false;

                foreach (var item_lot in dqe.Lots.ToList())
                {
                    foreach (var item_chap in item_lot.Chapters.ToList())
                    {
                        foreach (var item in item_chap.Items.ToList())
                        {

                            if (item.DeboursseSec <= 0.0m)
                            {
                                DebourseSecOk = true;
                                break;
                            }
                        }
                    }
                }
                if (DebourseSecOk != false)
                {
                    return BadRequest(new { message = "Veillez rensseigner tous les debourssé sec avant de convertir" });
                }
                if (existing == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                if (existing.Statut == "validé")
                {
                    return BadRequest(new { message = "Le DQE est déjà validé" });
                }

                if (existing.IsConverted)
                {
                    return BadRequest(new { message = "Le DQE est déjà converti en projet" });
                }

                // TODO: Récupérer le nom de l'utilisateur depuis JWT
                int validePar = 3; // Placeholder

                var success = await _dqeService.ValidateAsync(id, validePar);

                if (success)
                {
                    return Ok(new { message = "DQE validé avec succès" });
                }
                else
                {
                    return BadRequest(new { message = "Impossible de valider le DQE" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // ========================================
        // ENDPOINTS DE CONVERSION
        // ========================================

        /// <summary>
        /// Vérifie si un DQE peut être converti en projet
        /// GET /api/dqe/{id}/can-convert
        /// </summary>
        [HttpGet("{id}/can-convert")]
        public async Task<IActionResult> CanConvert(int id)
        {
            try
            {
                var (canConvert, reason) = await _dqeService.CanConvertToProjectAsync(id);

                return Ok(new
                {
                    canConvert,
                    reason,
                    message = canConvert ? "Le DQE peut être converti" : reason
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Génère une prévisualisation de la conversion DQE → Projet
        /// POST /api/dqe/{id}/conversion-preview
        /// </summary>
        [HttpPost("{id}/conversion-preview")]
        public async Task<IActionResult> GetConversionPreview(
            int id,
            [FromBody] ConvertDQEToProjectRequest request)
        {
            try
            {
                var preview = await _conversionService.PreviewConversionAsync(id, request);

                return Ok(preview);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Convertit un DQE en projet
        /// POST /api/dqe/{id}/convert-to-project
        /// </summary>
        [HttpPost("{id}/convert-to-project")]
        public async Task<IActionResult> ConvertToProject(
            int id,
            [FromBody] ConvertDQEToProjectRequest request)
        {
            try
            {
                // TODO: Récupérer l'utilisateur depuis JWT
                var utilisateurId = 3;
                request.ChefProjetId = 3;

                var projetId = await _conversionService.ConvertDQEToProjectAsync(id, request, utilisateurId);

                return Ok(new
                {
                    message = "DQE converti en projet avec succès",
                    projetId,
                    redirectUrl = $"/projets/{projetId}"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // ========================================
        // ENDPOINTS SUPPLÉMENTAIRES
        // ========================================

        /// <summary>
        /// Récupère tous les DQE convertis
        /// GET /api/dqe/converted
        /// </summary>
        [HttpGet("converted")]
        public async Task<IActionResult> GetConverted()
        {
            try
            {
                var dqeList = await _dqeService.GetAllAsync(isConverted: true);
                return Ok(dqeList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Récupère tous les DQE convertibles (validés et non convertis)
        /// GET /api/dqe/convertible
        /// </summary>
        [HttpGet("convertible")]
        public async Task<IActionResult> GetConvertible()
        {
            try
            {
                var dqeList = await _dqeService.GetAllAsync(statut: "validé", isConverted: false);
                return Ok(dqeList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Récupère tous les DQE en brouillon
        /// GET /api/dqe/brouillon
        /// </summary>
        [HttpGet("brouillon")]
        public async Task<IActionResult> GetBrouillon()
        {
            try
            {
                var dqeList = await _dqeService.GetAllAsync(statut: "brouillon");
                return Ok(dqeList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Récupère tous les DQE validés
        /// GET /api/dqe/valide
        /// </summary>
        [HttpGet("valide")]
        public async Task<IActionResult> GetValide()
        {
            try
            {
                var dqeList = await _dqeService.GetAllAsync(statut: "validé");
                return Ok(dqeList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // ========================================
        // ENDPOINTS STATISTIQUES
        // ========================================

        /// <summary>
        /// Récupère les statistiques globales des DQE
        /// GET /api/dqe/stats
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var tous = await _dqeService.GetAllAsync();
                var convertis = tous.Where(d => d.IsConverted).ToList();
                var convertibles = tous.Where(d => d.ConversionStatus == "convertible").ToList();
                var brouillons = tous.Where(d => d.Statut == "brouillon").ToList();
                var valides = tous.Where(d => d.Statut == "validé").ToList();

                return Ok(new
                {
                    total = tous.Count,
                    converti = convertis.Count,
                    convertible = convertibles.Count,
                    brouillon = brouillons.Count,
                    valide = valides.Count,
                    totalBudgetHT = tous.Sum(d => d.TotalRevenueHT),
                    budgetConverti = convertis.Sum(d => d.TotalRevenueHT),
                    budgetConvertible = convertibles.Sum(d => d.TotalRevenueHT),
                    tauxConversion = tous.Count > 0 ?
                        Math.Round((decimal)convertis.Count / tous.Count * 100, 2) : 0
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpPost("{dqeId}/link-to-project/{projetId}")]
        public async Task<IActionResult> LinkToExistingProject(int dqeId, int projetId)
        {
            try
            {
                // Vérifier que le DQE existe et peut être converti
                var (canConvert, reason) = await _dqeService.CanConvertToProjectAsync(dqeId);
                if (!canConvert)
                {
                    return BadRequest(new { message = reason });
                }

                // Vérifier que le projet existe et est disponible
                var projet = await _projetService.GetByIdAsync(projetId);
                if (projet == null)
                {
                    return NotFound(new { message = "Projet introuvable" });
                }

                if (projet.Statut == "Terminé" || projet.Statut == "Clôturé")
                {
                    return BadRequest(new { message = "Impossible de lier à un projet terminé" });
                }

                if (projet.LinkedDqeId.HasValue)
                {
                    return BadRequest(new { message = "Ce projet est déjà lié à un autre DQE" });
                }

                // Récupérer l'utilisateur depuis JWT
                var utilisateurId = Convert.ToInt32(GetCurrentUserId());

                // Lier le DQE au projet existant
                var success = await _conversionService.LinkDQEToExistingProjectAsync(
                    dqeId,
                    projetId,
                    utilisateurId
                );

                if (success)
                {
                    return Ok(new
                    {
                        message = "DQE lié au projet avec succès",
                        projetId,
                        redirectUrl = $"/projets/{projetId}"
                    });
                }
                else
                {
                    return BadRequest(new { message = "Erreur lors de la liaison du DQE au projet" });
                }
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // ========================================
        // ENDPOINTS D'EXPORT (EXCEL & PDF)
        // ========================================

        /// <summary>
        /// Exporte un DQE vers Excel
        /// GET /api/dqe/{id}/export/excel
        /// </summary>
        [HttpGet("{id}/export/excel")]
        public async Task<IActionResult> ExportToExcel(int id)
        {
            try
            {
                var dqe = await _dqeService.GetByIdAsync(id);
                if (dqe == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                var excelBytes = await _dqeExportService.ExportToExcelAsync(id);

                var fileName = $"DQE_{dqe.Reference}_{DateTime.Now:yyyyMMdd}.xlsx";

                return File(
                    excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur lors de l'export Excel : {ex.Message}" });
            }
        }

        /// <summary>
        /// Exporte un DQE vers PDF
        /// GET /api/dqe/{id}/export/pdf
        /// </summary>
        [HttpGet("{id}/export/pdf")]
        public async Task<IActionResult> ExportToPdf(int id)
        {
            try
            {
                var dqe = await _dqeService.GetByIdAsync(id);
                if (dqe == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                var pdfBytes = await _dqeExportService.ExportToPdfAsync(id);

                var fileName = $"DQE_{dqe.Reference}_{DateTime.Now:yyyyMMdd}.pdf";

                return File(
                    pdfBytes,
                    "application/pdf",
                    fileName
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur lors de l'export PDF : {ex.Message}" });
            }
        }

        /// <summary>
        /// Prévisualise un DQE en HTML (optionnel)
        /// GET /api/dqe/{id}/preview
        /// </summary>
        [HttpGet("{id}/preview")]
        public async Task<IActionResult> PreviewDqe(int id)
        {
            try
            {
                var dqe = await _dqeService.GetByIdAsync(id);
                if (dqe == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                var html = GenerateDqeHtmlPreview(dqe);
                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur lors de la prévisualisation : {ex.Message}" });
            }
        }

        // ========================================
        // MÉTHODE PRIVÉE POUR PRÉVISUALISATION HTML
        // ========================================

        private string GenerateDqeHtmlPreview(DQEDetailDTO dqe)
        {
            var html = $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>DQE - {dqe.Reference}</title>
    <style>
        body {{
            font-family: 'Calibri', Arial, sans-serif;
            margin: 20px;
            font-size: 11pt;
        }}
        .header {{
            background-color: #0066CC;
            color: white;
            padding: 20px;
            text-align: center;
            margin-bottom: 20px;
        }}
        .header h1 {{
            margin: 0;
            font-size: 24pt;
        }}
        .info-section {{
            background-color: #E7E6E6;
            padding: 15px;
            margin-bottom: 20px;
            border-radius: 5px;
        }}
        .info-row {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 10px;
        }}
        .info-label {{
            font-weight: bold;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 20px;
        }}
        th {{
            background-color: #4472C4;
            color: white;
            padding: 10px;
            text-align: left;
            font-weight: bold;
        }}
        td {{
            padding: 8px;
            border-bottom: 1px solid #ddd;
        }}
        .lot-row {{
            background-color: #D9E1F2;
            font-weight: bold;
            font-size: 12pt;
        }}
        .chapter-row {{
            background-color: #F2F2F2;
            font-style: italic;
            padding-left: 20px;
        }}
        .item-row {{
            padding-left: 40px;
        }}
        .total-section {{
            margin-top: 30px;
            text-align: right;
        }}
        .total-row {{
            display: flex;
            justify-content: flex-end;
            margin-bottom: 10px;
            font-size: 12pt;
        }}
        .total-label {{
            font-weight: bold;
            margin-right: 20px;
            min-width: 150px;
        }}
        .total-value {{
            min-width: 150px;
            text-align: right;
        }}
        .total-ht {{
            background-color: #FFF2CC;
            padding: 10px;
            font-weight: bold;
        }}
        .total-ttc {{
            background-color: #C6E0B4;
            padding: 10px;
            font-weight: bold;
            font-size: 14pt;
            border: 2px solid #0066CC;
        }}
        .footer {{
            margin-top: 40px;
            padding-top: 20px;
            border-top: 2px solid #0066CC;
            font-size: 9pt;
            color: #666;
            text-align: center;
        }}
        .text-right {{
            text-align: right;
        }}
        .text-center {{
            text-align: center;
        }}
        @media print {{
            body {{
                margin: 0;
            }}
            .header {{
                page-break-after: avoid;
            }}
            table {{
                page-break-inside: auto;
            }}
            tr {{
                page-break-inside: avoid;
                page-break-after: auto;
            }}
        }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>SAF ALU CI</h1>
        <h2>DÉCOMPOSITION QUANTITATIVE ESTIMATIVE (DQE)</h2>
    </div>

    <div class='info-section'>
        <div class='info-row'>
            <div>
                <span class='info-label'>Référence :</span> {dqe.Reference}
            </div>
            <div>
                <span class='info-label'>Date :</span> {dqe.DateCreation:dd/MM/yyyy}
            </div>
        </div>
        <div class='info-row'>
            <div>
                <span class='info-label'>Nom du projet :</span> {dqe.Nom}
            </div>
            <div>
                <span class='info-label'>Statut :</span> <strong>{dqe.Statut.ToUpper()}</strong>
            </div>
        </div>
        <div class='info-row'>
            <div>
                <span class='info-label'>Client :</span> {dqe.Client?.Nom ?? "N/A"}
            </div>
        </div>
        {(!string.IsNullOrEmpty(dqe.Description) ? $@"
        <div class='info-row'>
            <div>
                <span class='info-label'>Description :</span> {dqe.Description}
            </div>
        </div>" : "")}
    </div>

    <table>
        <thead>
            <tr>
                <th>CODE</th>
                <th>DÉSIGNATION</th>
                <th class='text-center'>UNITÉ</th>
                <th class='text-right'>QUANTITÉ</th>
                <th class='text-right'>PRIX UNITAIRE HT</th>
                <th class='text-right'>MONTANT HT</th>
                <th class='text-center'>% TOTAL</th>
            </tr>
        </thead>
        <tbody>
";

            // PARCOURIR LES LOTS
            if (dqe.Lots != null && dqe.Lots.Any())
            {
                foreach (var lot in dqe.Lots.OrderBy(l => l.Ordre))
                {
                    html += $@"
            <tr class='lot-row'>
                <td>{lot.Code}</td>
                <td>{lot.Nom.ToUpper()}</td>
                <td></td>
                <td></td>
                <td></td>
                <td class='text-right'>{lot.TotalRevenueHT:N0} F</td>
                <td class='text-center'>{lot.PourcentageTotal:F2}%</td>
            </tr>";

                    // PARCOURIR LES CHAPITRES
                    if (lot.Chapters != null && lot.Chapters.Any())
                    {
                        foreach (var chapter in lot.Chapters.OrderBy(c => c.Ordre))
                        {
                            html += $@"
            <tr class='chapter-row'>
                <td>  {chapter.Code}</td>
                <td>  {chapter.Nom}</td>
                <td></td>
                <td></td>
                <td></td>
                <td class='text-right'>{chapter.TotalRevenueHT:N0} F</td>
                <td></td>
            </tr>";

                            // PARCOURIR LES ITEMS
                            if (chapter.Items != null && chapter.Items.Any())
                            {
                                foreach (var item in chapter.Items.OrderBy(i => i.Ordre))
                                {
                                    html += $@"
            <tr class='item-row'>
                <td>    {item.Code}</td>
                <td>    {item.Designation}</td>
                <td class='text-center'>{item.Unite}</td>
                <td class='text-right'>{item.Quantite:N2}</td>
                <td class='text-right'>{item.PrixUnitaireHT:N0} F</td>
                <td class='text-right'>{item.TotalRevenueHT:N0} F</td>
                <td></td>
            </tr>";
                                }
                            }
                        }
                    }
                }
            }

            html += $@"
        </tbody>
    </table>

    <div class='total-section'>
        <div class='total-row total-ht'>
            <div class='total-label'>TOTAL HT :</div>
            <div class='total-value'>{dqe.TotalRevenueHT:N0} F</div>
        </div>
        <div class='total-row'>
            <div class='total-label'>TVA ({dqe.TauxTVA}%) :</div>
            <div class='total-value'>{dqe.MontantTVA:N0} F</div>
        </div>
        <div class='total-row total-ttc'>
            <div class='total-label'>TOTAL TTC :</div>
            <div class='total-value'>{dqe.TotalTTC:N0} F</div>
        </div>
    </div>

    {(dqe.IsConverted && dqe.LinkedProject != null ? $@"
    <div style='margin-top: 30px; padding: 15px; background-color: #FFF3CD; border-left: 4px solid #FF6B35;'>
        <strong>⚠️ Ce DQE a été converti en projet</strong><br>
        Projet : {dqe.LinkedProject.Numero} - {dqe.LinkedProject.Nom}<br>
        Date de conversion : {dqe.LinkedProject.ConvertedAt:dd/MM/yyyy HH:mm}
    </div>" : "")}

    <div class='footer'>
        <p>Document généré le {DateTime.Now:dd/MM/yyyy à HH:mm} - SAF ALU CI</p>
    </div>
</body>
</html>";

            return html;
        }

    }
}