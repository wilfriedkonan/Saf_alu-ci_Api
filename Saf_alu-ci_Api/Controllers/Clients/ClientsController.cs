using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saf_alu_ci_Api.Controllers.Devis;

namespace Saf_alu_ci_Api.Controllers.Clients
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClientsController : BaseController
    {
        private readonly ClientService _clientService;

        public ClientsController(ClientService clientService)
        {
            _clientService = clientService;
        }

        /// <summary>
        /// Récupère tous les clients actifs avec leurs statistiques
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var clients = await _clientService.GetAllAsync();
                return Ok(new
                {
                    success = true,
                    data = clients,
                    count = clients.Count
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

        /// <summary>
        /// Récupère un client par son ID avec ses statistiques
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                var client = await _clientService.GetByIdAsync(id);
                if (client == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Client non trouvé"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = client
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

        /// <summary>
        /// Récupère uniquement les statistiques d'un client
        /// </summary>
        [HttpGet("{id}/statistiques")]
        public async Task<IActionResult> GetStatistiques(int id)
        {
            try
            {
                var statistiques = await _clientService.GetClientStatistiquesAsync(id);
                return Ok(new
                {
                    success = true,
                    data = statistiques
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la récupération des statistiques",
                    error = ex.Message
                });
            }
        }

        // statistique Global
        [HttpGet("statistiqueGlobal")]
        public async Task<IActionResult> GetStatistique()
        {
            try
            {
                var stats = await _clientService.GetStatistiquesAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Crée un nouveau client
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateClientRequest model)
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(model.TypeClient) || string.IsNullOrWhiteSpace(model.Nom))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "TypeClient et Nom sont obligatoires"
                    });
                }

                var client = new Client
                {
                    TypeClient = model.TypeClient,
                    Nom = model.Nom,
                    RaisonSociale = model.RaisonSociale,
                    Email = model.Email,
                    Telephone = model.Telephone,
                    Adresse = model.Adresse,
                    Ville = model.Ville,
                    Ncc = model.Ncc,
                    Status = "prospect", // Statut par défaut
                    DateCreation = DateTime.UtcNow,
                    DateModification = DateTime.UtcNow,
                    Actif = true
                    // TODO: Récupérer l'ID de l'utilisateur connecté depuis le JWT
                    // UtilisateurCreation = GetCurrentUserId()
                };

                var clientId = await _clientService.CreateAsync(client);

                // Récupérer le client créé avec ses statistiques
                var clientCree = await _clientService.GetByIdAsync(clientId);

                return CreatedAtAction(nameof(Get), new { id = clientId }, new
                {
                    success = true,
                    message = "Client créé avec succès",
                    data = clientCree
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la création du client",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Met à jour un client existant
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Client model)
        {
            try
            {
                var existing = await _clientService.GetByIdAsync(id);
                if (existing == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Client non trouvé"
                    });
                }

                // Validation
                if (string.IsNullOrWhiteSpace(model.TypeClient) || string.IsNullOrWhiteSpace(model.Nom))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "TypeClient et Nom sont obligatoires"
                    });
                }

                model.Id = id;
                model.DateCreation = existing.DateCreation; // Préserver la date de création
                model.DateModification = DateTime.UtcNow;
                model.UtilisateurCreation = existing.UtilisateurCreation; // Préserver l'utilisateur créateur

                await _clientService.UpdateAsync(model);

                // Récupérer le client mis à jour avec ses statistiques
                var clientMisAJour = await _clientService.GetByIdAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "Client modifié avec succès",
                    data = clientMisAJour
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la modification du client",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Supprime un client (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existing = await _clientService.GetByIdAsync(id);
                if (existing == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Client non trouvé"
                    });
                }

                await _clientService.DeleteAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "Client supprimé avec succès"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la suppression du client",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Change le statut d'un client
        /// </summary>
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                var client = await _clientService.GetByIdAsync(id);
                if (client == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Client non trouvé"
                    });
                }

                // Validation du statut
                var validStatuses = new[] { "actif", "inactif", "prospect" };
                if (!validStatuses.Contains(request.Status?.ToLower()))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Statut invalide. Valeurs acceptées: actif, inactif, prospect"
                    });
                }

                // Créer un objet Client pour la mise à jour
                var clientToUpdate = new Client
                {
                    Id = client.Id,
                    TypeClient = client.TypeClient,
                    Nom = client.Nom,
                    RaisonSociale = client.RaisonSociale,
                    Email = client.Email,
                    Telephone = client.Telephone,
                    Adresse = client.Adresse,
                    Ville = client.Ville,
                    Ncc = client.Ncc,
                    Status = request.Status,
                    DateCreation = client.DateCreation,
                    DateModification = DateTime.UtcNow,
                    Actif = client.Actif,
                    UtilisateurCreation = client.UtilisateurCreation
                };

                await _clientService.UpdateAsync(clientToUpdate);

                // Récupérer le client mis à jour avec ses statistiques
                var clientMisAJour = await _clientService.GetByIdAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "Statut mis à jour avec succès",
                    data = clientMisAJour
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la mise à jour du statut",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Recherche des clients par critères avec statistiques
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? nom, [FromQuery] string? typeClient, [FromQuery] string? status)
        {
            try
            {
                var clients = await _clientService.SearchAsync(nom, typeClient, status);
                return Ok(new
                {
                    success = true,
                    data = clients,
                    count = clients.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la recherche",
                    error = ex.Message
                });
            }
        }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; }
    }
}