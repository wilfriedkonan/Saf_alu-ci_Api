using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Saf_alu_ci_Api.Controllers.Clients
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class ClientsController : ControllerBase
    {
        private readonly ClientService _clientService;

        public ClientsController(ClientService clientService)
        {
            _clientService = clientService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var clients = await _clientService.GetAllAsync();
                return Ok(clients);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                var client = await _clientService.GetByIdAsync(id);
                if (client == null) return NotFound("Client non trouvé");
                return Ok(client);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateClientRequest model)
        {
            try
            {
                var client = new Client
                {
                    TypeClient = model.TypeClient,
                    Nom = model.Nom,
                    Prenom = model.Prenom,
                    RaisonSociale = model.RaisonSociale,
                    Email = model.Email,
                    Telephone = model.Telephone,
                    TelephoneMobile = model.TelephoneMobile,
                    Adresse = model.Adresse,
                    CodePostal = model.CodePostal,
                    Ville = model.Ville,
                    Siret = model.Siret,
                    NumeroTVA = model.NumeroTVA,
                    DateCreation = DateTime.UtcNow,
                    DateModification = DateTime.UtcNow,
                    Actif = true
                    // TODO: Récupérer l'ID de l'utilisateur connecté depuis le JWT
                    // UtilisateurCreation = GetCurrentUserId()
                };

                var clientId = await _clientService.CreateAsync(client);
                client.Id = clientId;

                return CreatedAtAction(nameof(Get), new { id = clientId }, new
                {
                    message = "Client créé avec succès",
                    client
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Client model)
        {
            try
            {
                var existing = await _clientService.GetByIdAsync(id);
                if (existing == null) return NotFound("Client non trouvé");

                model.Id = id;
                model.DateModification = DateTime.UtcNow;
                await _clientService.UpdateAsync(model);

                return Ok(new { message = "Client modifié avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existing = await _clientService.GetByIdAsync(id);
                if (existing == null) return NotFound("Client non trouvé");

                await _clientService.DeleteAsync(id);
                return Ok(new { message = "Client supprimé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }
    }
}
