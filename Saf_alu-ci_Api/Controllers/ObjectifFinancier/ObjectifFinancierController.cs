using Microsoft.AspNetCore.Mvc;
using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.Devis;

using Microsoft.AspNetCore.Mvc;

namespace Saf_alu_ci_Api.Controllers.ObjectifFinancier
{
    [ApiController]
    [Route("api/[controller]")]
    public class ObjectifFinancierController : BaseController
    {
        private readonly ObjectifFinacierService _objectifFinacierService;

        public ObjectifFinancierController(ObjectifFinacierService objectifFinacierService)
        {
            _objectifFinacierService = objectifFinacierService;
        }

        // ===============================
        // 1️⃣ GET ALL
        // ===============================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var data = await _objectifFinacierService.GetAllAsync();
                return Ok(new
                {
                    success = true,
                    message = "Liste des objectifs financiers",
                    data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        // ===============================
        // 2️⃣ GET BY ID
        // ===============================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var objectif = await _objectifFinacierService.GetByIdAsync(id);

                if (objectif == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Objectif financier introuvable"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = objectif
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        // ===============================
        // 3️⃣ CREATE
        // ===============================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateObjectifRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var objectif = new ObjectifFinancierModel
                {
                    Montant = model.Montant,
                    DateFinPrevue = model.DateFinPrevue,
                    Statut = "EnCours",
                    DateCreation = DateTime.UtcNow,
                    UtilisateurCreation = model.UtilisateurCreation
                };

                var id = await _objectifFinacierService.CreateAsync(objectif);

                return CreatedAtAction(nameof(GetById),
                    new { id },
                    new
                    {
                        success = true,
                        message = "Objectif financier créé avec succès",
                        data = new { id }
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        // ===============================
        // 4️⃣ UPDATE
        // ===============================
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateObjectifRequest model)
        {
            try
            {
                var existing = await _objectifFinacierService.GetByIdAsync(id);

                if (existing == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Objectif financier introuvable"
                    });
                }

                await _objectifFinacierService.UpdateAsync(id, model);

                var updated = await _objectifFinacierService.GetByIdAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "Objectif financier mis à jour avec succès",
                    data = updated
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        // ===============================
        // 5️⃣ DELETE (désactivation)
        // ===============================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var objectif = await _objectifFinacierService.GetByIdAsync(id);

                if (objectif == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Objectif financier introuvable"
                    });
                }

                //    await _objectifFinacierService.DisableAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "Objectif financier désactivé avec succès"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }
    }
}
