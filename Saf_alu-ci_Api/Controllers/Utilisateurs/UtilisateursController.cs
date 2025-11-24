using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Saf_alu_ci_Api.Services;
using Saf_alu_ci_Api.Services.Jw;

namespace Saf_alu_ci_Api.Controllers.Utilisateurs
{
    namespace SafAlu_API.Modules.Utilisateurs
    {
        [ApiController]
        [Route("api/[controller]")]
        public class UtilisateursController : ControllerBase
        {
            private readonly UtilisateurService _utilisateurService;
            private readonly JwtService _jwtService;

            public UtilisateursController(UtilisateurService utilisateurService, JwtService jwtService)
            {
                _utilisateurService = utilisateurService;
                _jwtService = jwtService;
            }

            [HttpGet]
            //[Authorize]
            public async Task<IActionResult> GetAll()
            {
                try
                {
                    var utilisateurs = await _utilisateurService.GetAllAsync();
                    var result = utilisateurs.Select(u => new
                    {
                        u.Id,
                        u.Email,
                        u.Username,
                        u.Prenom,
                        u.Nom,
                        u.Telephone,
                        u.Photo,
                        Role = u.Role?.Nom,
                        u.DerniereConnexion,
                        u.DateCreation,
                        u.Actif
                    });
                    return Ok(result);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Erreur serveur : {ex.Message}");
                }
            }

            [HttpGet("{id}")]
            [Authorize]
            public async Task<IActionResult> Get(int id)
            {
                try
                {
                    var user = await _utilisateurService.GetByIdAsync(id);
                    if (user == null) return NotFound("Utilisateur non trouvé");

                    var result = new
                    {
                        user.Id,
                        user.Email,
                        user.Username,
                        user.Prenom,
                        user.Nom,
                        user.Telephone,
                        user.Photo,
                        Role = user.Role?.Nom,
                        Permissions = user.Role?.Permissions,
                        user.DerniereConnexion,
                        user.DateCreation
                    };

                    return Ok(result);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Erreur serveur : {ex.Message}");
                }
            }

            [HttpPost("register")]
            public async Task<IActionResult> Register([FromBody] RegisterRequest model)
            {
                try
                {
                    if (await _utilisateurService.EmailExistsAsync(model.Email))
                    {
                        return BadRequest("Cet email est déjà utilisé.");
                    }

                    var hashedPassword = PasswordHasher.Hash(model.Password);
                    var user = new Utilisateur
                    {
                        Email = model.Email,
                        Username = model.Username,
                        MotDePasseHash = hashedPassword,
                        Prenom = model.Prenom,
                        Nom = model.Nom,
                        Telephone = model.Telephone,
                        RoleId = model.RoleId,
                        DateCreation = DateTime.UtcNow,
                        DateModification = DateTime.UtcNow,
                        Actif = true
                    };

                    var userId = await _utilisateurService.CreateAsync(user);
                    user.Id = userId;

                    var token = _jwtService.GenerateToken(user);

                    return Ok(new
                    {
                        message = "Utilisateur créé avec succès",
                        token,
                        user = new
                        {
                            user.Id,
                            user.Email,
                            user.Username,
                            user.Prenom,
                            user.Nom,
                            user.Telephone
                        }
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Erreur serveur : {ex.Message}");
                }
            }

            [HttpPost("login")]
            public async Task<IActionResult> Login([FromBody] LoginRequest model)
            {
                try
                {
                    var user = await _utilisateurService.GetByEmailAsync(model.Email);

                    if (user == null || !PasswordHasher.Verify(model.Password, user.MotDePasseHash))
                    {
                        return Unauthorized("Email ou mot de passe incorrect.");
                    }

                    if (!user.Actif)
                    {
                        return Unauthorized("Votre compte a été désactivé.");
                    }

                    // Mettre à jour la dernière connexion
                    await _utilisateurService.UpdateDerniereConnexionAsync(user.Id);

                    var token = _jwtService.GenerateToken(user);

                    return Ok(new
                    {
                        token,
                        user = new
                        {
                            id = user.Id,
                            email = user.Email,
                            username = user.Username,
                            prenom = user.Prenom,
                            nom = user.Nom,
                            telephone = user.Telephone,
                            photo = user.Photo,
                            role = user.Role?.Nom,
                            permissions = user.Role?.GetPermissionsList() ?? RolePermissions.GetPermissions(user.Role?.Nom ?? ""), // Permissions depuis la DB ou fallback
                            derniereConnexion = user.DerniereConnexion,
                            dateCreation = user.DateCreation,
                            actif = user.Actif
                        }
                    });

                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Erreur serveur : {ex.Message}");
                }
            }

            [HttpPut("{id}")]
            //[Authorize]
            public async Task<IActionResult> Update(int id, [FromBody] Utilisateur model)
            {
                try
                {
                    var existing = await _utilisateurService.GetByIdAsync(id);
                    if (existing == null) return NotFound("Utilisateur non trouvé");

                    model.Id = id;
                    model.DateModification = DateTime.UtcNow;
                    await _utilisateurService.UpdateAsync(model);

                    return Ok(new { message = "Utilisateur modifié avec succès" });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Erreur serveur : {ex.Message}");
                }
            }

            [HttpDelete("{id}")]
            [Authorize]
            public async Task<IActionResult> Delete(int id)
            {
                try
                {
                    var existing = await _utilisateurService.GetByIdAsync(id);
                    if (existing == null) return NotFound("Utilisateur non trouvé");

                    await _utilisateurService.DeleteAsync(id);
                    return Ok(new { message = "Utilisateur supprimé avec succès" });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Erreur serveur : {ex.Message}");
                }
            }
        }
    }
}