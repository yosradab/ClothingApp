using Clothing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Clothing.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DemandeController : ControllerBase
    {
        private readonly ClothingPlatformContext _context;
        private readonly ILogger<DemandeController> _logger;

        public DemandeController(ClothingPlatformContext context, ILogger<DemandeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region DTOs for API endpoints
        public class DemandeCreateDto
        {
            [Required(ErrorMessage = "L'ID de l'annonce est requis.")]
            public int AnnonceId { get; set; }

            [Range(1, 365, ErrorMessage = "La durée de la location doit être comprise entre 1 et 365 jours.")]
            public int? DureeLocationEnJours { get; set; }
        }

        public class UtilisateurSummaryDto
        {
            public int Id { get; set; }
            public string Prenom { get; set; }
            public string Nom { get; set; }
        }

        public class AnnonceSummaryDto
        {
            public int Id { get; set; }
            public string Titre { get; set; }
            public TypeAnnonce Type { get; set; }
            public decimal Prix { get; set; }
            public string Statut { get; set; }
            public UtilisateurSummaryDto Utilisateur { get; set; }
        }

        public class DemandeDetailsDto
        {
            public int Id { get; set; }
            public AnnonceSummaryDto Annonce { get; set; }
            public UtilisateurSummaryDto Demandeur { get; set; }
            public string Statut { get; set; }
            public DateTime DateDemande { get; set; }
            public int? DureeLocationEnJours { get; set; }
        }

        public class DemandeListDto
        {
            public int Id { get; set; }
            public int AnnonceId { get; set; }
            public UtilisateurSummaryDto Demandeur { get; set; }
            public string Statut { get; set; }
            public DateTime DateDemande { get; set; }
            public int? DureeLocationEnJours { get; set; }
        }

        public class DemandeCreatedDto
        {
            public int Id { get; set; }
            public int AnnonceId { get; set; }
            public int DemandeurId { get; set; }
            public string Statut { get; set; } = string.Empty;
            public DateTime DateDemande { get; set; }
            public int? DureeLocationEnJours { get; set; }
        }

        #endregion

        /// <summary>
        /// Retrieves the user ID from the authentication claims.
        /// Throws an UnauthorizedAccessException if the user ID cannot be found or is invalid.
        /// </summary>
        /// <returns>The ID of the current authenticated user.</returns>
        private int GetUtilisateurId()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                 ?? User.FindFirst("sub")?.Value
                                 ?? User.FindFirst("UtilisateurId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var utilisateurId))
                {
                    _logger.LogError("Invalid or missing user ID claim");
                    throw new UnauthorizedAccessException("Vous n'êtes pas connecté ou l'identifiant utilisateur est invalide.");
                }

                return utilisateurId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting UtilisateurId from claims");
                throw;
            }
        }

        /// <summary>
        /// Creates a new demand for an ad.
        /// </summary>
        /// <param name="demandeDto">The demand creation data transfer object.</param>
        /// <returns>The newly created demand.</returns>
        [HttpPost]
        public async Task<ActionResult<DemandeCreatedDto>> PostDemande([FromBody] DemandeCreateDto demandeDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var utilisateurId = GetUtilisateurId();

                var utilisateur = await _context.Utilisateurs
                    .FirstOrDefaultAsync(u => u.Id == utilisateurId);
                if (utilisateur == null)
                {
                    return NotFound("L'utilisateur n'a pas été trouvé. Veuillez vous reconnecter.");
                }

                var annonce = await _context.Annonces
                    .FirstOrDefaultAsync(a => a.Id == demandeDto.AnnonceId);

                if (annonce == null)
                {
                    return NotFound("L'annonce spécifiée n'existe pas.");
                }

                if (annonce.UtilisateurId == utilisateurId)
                {
                    return BadRequest("Vous ne pouvez pas faire une demande pour votre propre annonce.");
                }

                if (annonce.Type == TypeAnnonce.Vente)
                {
                    if (annonce.Statut == "Vendu")
                    {
                        return BadRequest("Cette annonce a déjà été vendue.");
                    }
                    if (annonce.Statut != "Disponible" || !annonce.EstValide)
                    {
                        return BadRequest("Cette annonce n'est pas disponible pour la vente.");
                    }
                }
                else if (annonce.Statut != "Disponible" || !annonce.EstValide)
                {
                    return BadRequest("L'annonce n'est pas disponible.");
                }

                var existingDemande = await _context.Demandes
                    .FirstOrDefaultAsync(d => d.DemandeurId == utilisateurId && d.AnnonceId == demandeDto.AnnonceId && d.Statut == DemandeStatut.EnAttente);

                if (existingDemande != null)
                {
                    return BadRequest("Vous avez déjà une demande en attente pour cette annonce.");
                }

                int? dureeLocation = demandeDto.DureeLocationEnJours;

                if (annonce.Type == TypeAnnonce.Location)
                {
                    if (annonce.DateDebutLocation.HasValue && annonce.DateFinLocation.HasValue)
                    {
                        var maxDuration = (annonce.DateFinLocation.Value - annonce.DateDebutLocation.Value).TotalDays;

                        if (dureeLocation.HasValue)
                        {
                            if (dureeLocation.Value <= 0 || dureeLocation.Value > maxDuration)
                            {
                                return BadRequest($"La durée de la location ({dureeLocation.Value} jours) doit être comprise entre 1 et {maxDuration} jours.");
                            }
                        }
                        else
                        {
                            dureeLocation = (int)Math.Round(maxDuration);
                            _logger.LogInformation("No duration provided for rental. Calculated default duration of {Duration} days for AnnonceId: {AnnonceId}", dureeLocation, annonce.Id);
                        }
                    }
                    else
                    {
                        return BadRequest("La durée de la location est requise pour ce type d'annonce, et l'annonce ne contient pas de dates pour la calculer automatiquement.");
                    }
                }
                else
                {
                    if (dureeLocation.HasValue)
                    {
                        return BadRequest("La durée de location ne peut pas être spécifiée pour ce type d'annonce.");
                    }
                }

                var demande = new Demande
                {
                    DemandeurId = utilisateurId,
                    AnnonceId = demandeDto.AnnonceId,
                    Statut = DemandeStatut.EnAttente,
                    DateDemande = DateTime.UtcNow,
                    DureeLocationEnJours = dureeLocation
                };

                _context.Demandes.Add(demande);
                await _context.SaveChangesAsync();

                var demandeCreatedDto = new DemandeCreatedDto
                {
                    Id = demande.Id,
                    AnnonceId = demande.AnnonceId,
                    DemandeurId = demande.DemandeurId,
                    Statut = demande.Statut.ToString(),
                    DateDemande = demande.DateDemande,
                    DureeLocationEnJours = demande.DureeLocationEnJours
                };

                return CreatedAtAction(nameof(GetDemande), new { id = demande.Id }, demandeCreatedDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in PostDemande");
                return Unauthorized(ex.Message);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error in PostDemande. Inner Exception: {InnerExceptionMessage}", ex.InnerException?.Message);
                return StatusCode(500, $"Une erreur est survenue lors de l'enregistrement de la demande. Veuillez réessayer. Détails techniques : {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostDemande");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DemandeDetailsDto>> GetDemande(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var isAdmin = User.HasClaim(ClaimTypes.Role, "Administrateur");

                var demande = await _context.Demandes
                    .Include(d => d.Annonce)
                        .ThenInclude(a => a.Utilisateur)
                    .Include(d => d.Demandeur)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (demande == null)
                {
                    return NotFound("Demande non trouvée.");
                }

                if (demande.DemandeurId != utilisateurId && demande.Annonce?.UtilisateurId != utilisateurId && !isAdmin)
                {
                    return Forbid("Vous n'êtes pas autorisé à voir cette demande.");
                }

                var result = new DemandeDetailsDto
                {
                    Id = demande.Id,
                    Statut = demande.Statut.ToString(),
                    DateDemande = demande.DateDemande,
                    DureeLocationEnJours = demande.DureeLocationEnJours,
                    Demandeur = new UtilisateurSummaryDto
                    {
                        Id = demande.Demandeur.Id,
                        Prenom = demande.Demandeur.Prenom,
                        Nom = demande.Demandeur.Nom
                    },
                    Annonce = new AnnonceSummaryDto
                    {
                        Id = demande.Annonce.Id,
                        Titre = demande.Annonce.Titre,
                        Type = demande.Annonce.Type,
                        Prix = demande.Annonce.Prix,
                        Statut = demande.Annonce.Statut,
                        Utilisateur = new UtilisateurSummaryDto
                        {
                            Id = demande.Annonce.Utilisateur.Id,
                            Prenom = demande.Annonce.Utilisateur.Prenom,
                            Nom = demande.Annonce.Utilisateur.Nom
                        }
                    }
                };

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in GetDemande");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDemande for DemandeId: {DemandeId}", id);
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        [HttpGet("{id}/statut")]
        public async Task<ActionResult<object>> GetDemandeStatus(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var isAdmin = User.HasClaim(ClaimTypes.Role, "Administrateur");

                var demande = await _context.Demandes
                    .Include(d => d.Annonce)
                    .Where(d => d.Id == id)
                    .Select(d => new { d.DemandeurId, AnnonceUtilisateurId = d.Annonce.UtilisateurId, d.Statut })
                    .FirstOrDefaultAsync();

                if (demande == null)
                {
                    return NotFound("Demande non trouvée.");
                }

                if (demande.DemandeurId != utilisateurId && demande.AnnonceUtilisateurId != utilisateurId && !isAdmin)
                {
                    _logger.LogWarning("Unauthorized access attempt for DemandeId: {DemandeId} by UtilisateurId: {UtilisateurId}", id, utilisateurId);
                    return Forbid("Vous n'êtes pas autorisé à voir le statut de cette demande.");
                }

                return Ok(new { Statut = demande.Statut.ToString() });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in GetDemandeStatus");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDemandeStatus for DemandeId: {DemandeId}", id);
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        [HttpGet("annonce/{annonceId}")]
        public async Task<ActionResult<IEnumerable<DemandeListDto>>> GetDemandesParAnnonce(int annonceId)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var isAdmin = User.HasClaim(ClaimTypes.Role, "Administrateur");

                var annonce = await _context.Annonces.FirstOrDefaultAsync(a => a.Id == annonceId);

                if (annonce == null)
                {
                    return NotFound("Annonce non trouvée.");
                }

                if (annonce.UtilisateurId != utilisateurId && !isAdmin)
                {
                    return Forbid("Vous n'êtes pas autorisé à voir les demandes de cette annonce.");
                }

                var demandes = await _context.Demandes
                    .Where(d => d.AnnonceId == annonceId)
                    .Include(d => d.Demandeur)
                    .Select(d => new DemandeListDto
                    {
                        Id = d.Id,
                        AnnonceId = d.AnnonceId,
                        Statut = d.Statut.ToString(),
                        DateDemande = d.DateDemande,
                        DureeLocationEnJours = d.DureeLocationEnJours,
                        Demandeur = new UtilisateurSummaryDto
                        {
                            Id = d.Demandeur.Id,
                            Prenom = d.Demandeur.Prenom,
                            Nom = d.Demandeur.Nom
                        }
                    })
                    .ToListAsync();

                if (!demandes.Any())
                {
                    return Ok(new List<DemandeListDto>());
                }

                return Ok(demandes);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in GetDemandesParAnnonce");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDemandesParAnnonce for AnnonceId: {AnnonceId}", annonceId);
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        [HttpGet("mes-demandes")]
        public async Task<ActionResult<IEnumerable<DemandeDetailsDto>>> GetMesDemandes()
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                var demandes = await _context.Demandes
                    .Where(d => d.DemandeurId == utilisateurId)
                    .Include(d => d.Annonce)
                        .ThenInclude(a => a.Utilisateur)
                    .Include(d => d.Demandeur)
                    .Select(d => new DemandeDetailsDto
                    {
                        Id = d.Id,
                        Statut = d.Statut.ToString(),
                        DateDemande = d.DateDemande,
                        DureeLocationEnJours = d.DureeLocationEnJours,
                        Demandeur = new UtilisateurSummaryDto
                        {
                            Id = d.Demandeur.Id,
                            Prenom = d.Demandeur.Prenom,
                            Nom = d.Demandeur.Nom
                        },
                        Annonce = new AnnonceSummaryDto
                        {
                            Id = d.Annonce.Id,
                            Titre = d.Annonce.Titre,
                            Type = d.Annonce.Type,
                            Prix = d.Annonce.Prix,
                            Statut = d.Annonce.Statut,
                            Utilisateur = new UtilisateurSummaryDto
                            {
                                Id = d.Annonce.Utilisateur.Id,
                                Prenom = d.Annonce.Utilisateur.Prenom,
                                Nom = d.Annonce.Utilisateur.Nom
                            }
                        }
                    })
                    .ToListAsync();

                return Ok(demandes);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in GetMesDemandes");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetMesDemandes");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        [HttpGet("demandes-pour-mes-annonces")]
        public async Task<ActionResult<IEnumerable<DemandeDetailsDto>>> GetDemandesPourMesAnnonces()
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var isAdmin = User.HasClaim(ClaimTypes.Role, "Administrateur");
                var demandes = await _context.Demandes
                    .Where(d => d.Annonce.UtilisateurId == utilisateurId || isAdmin)
                    .Include(d => d.Annonce)
                    .ThenInclude(a => a.Utilisateur)
                    .Include(d => d.Demandeur)
                    .Select(d => new DemandeDetailsDto
                    {
                        Id = d.Id,
                        Statut = d.Statut.ToString(),
                        DateDemande = d.DateDemande,
                        DureeLocationEnJours = d.DureeLocationEnJours,
                        Demandeur = new UtilisateurSummaryDto
                        {
                            Id = d.Demandeur.Id,
                            Prenom = d.Demandeur.Prenom,
                            Nom = d.Demandeur.Nom
                        },
                        Annonce = new AnnonceSummaryDto
                        {
                            Id = d.Annonce.Id,
                            Titre = d.Annonce.Titre,
                            Type = d.Annonce.Type,
                            Prix = d.Annonce.Prix,
                            Statut = d.Annonce.Statut,
                            Utilisateur = new UtilisateurSummaryDto
                            {
                                Id = d.Annonce.Utilisateur.Id,
                                Prenom = d.Annonce.Utilisateur.Prenom,
                                Nom = d.Annonce.Utilisateur.Nom
                            }
                        }
                    })
                    .ToListAsync();
                return Ok(demandes);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in GetDemandesPourMesAnnonces");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDemandesPourMesAnnonces");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        [HttpGet("accepted")]
        public async Task<ActionResult<IEnumerable<DemandeDetailsDto>>> GetAcceptedDemands()
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var acceptedDemands = await _context.Demandes
                    .Where(d => d.DemandeurId == utilisateurId && d.Statut == DemandeStatut.Acceptee)
                    .Include(d => d.Annonce)
                    .ThenInclude(a => a.Utilisateur)
                    .Include(d => d.Demandeur)
                    .Select(d => new DemandeDetailsDto
                    {
                        Id = d.Id,
                        Statut = d.Statut.ToString(),
                        DateDemande = d.DateDemande,
                        DureeLocationEnJours = d.DureeLocationEnJours,
                        Demandeur = new UtilisateurSummaryDto
                        {
                            Id = d.Demandeur.Id,
                            Prenom = d.Demandeur.Prenom,
                            Nom = d.Demandeur.Nom
                        },
                        Annonce = new AnnonceSummaryDto
                        {
                            Id = d.Annonce.Id,
                            Titre = d.Annonce.Titre,
                            Type = d.Annonce.Type,
                            Prix = d.Annonce.Prix,
                            Statut = d.Annonce.Statut,
                            Utilisateur = new UtilisateurSummaryDto
                            {
                                Id = d.Annonce.Utilisateur.Id,
                                Prenom = d.Annonce.Utilisateur.Prenom,
                                Nom = d.Annonce.Utilisateur.Nom
                            }
                        }
                    })
                    .ToListAsync();
                return Ok(acceptedDemands);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in GetAcceptedDemands");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetAcceptedDemands");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        [HttpGet("cancelled")]
        public async Task<ActionResult<IEnumerable<DemandeDetailsDto>>> GetCancelledDemands()
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var cancelledDemands = await _context.Demandes
                    .Where(d => d.DemandeurId == utilisateurId && d.Statut == DemandeStatut.Annulee)
                    .Include(d => d.Annonce)
                    .ThenInclude(a => a.Utilisateur)
                    .Include(d => d.Demandeur)
                    .Select(d => new DemandeDetailsDto
                    {
                        Id = d.Id,
                        Statut = d.Statut.ToString(),
                        DateDemande = d.DateDemande,
                        DureeLocationEnJours = d.DureeLocationEnJours,
                        Demandeur = new UtilisateurSummaryDto
                        {
                            Id = d.Demandeur.Id,
                            Prenom = d.Demandeur.Prenom,
                            Nom = d.Demandeur.Nom
                        },
                        Annonce = new AnnonceSummaryDto
                        {
                            Id = d.Annonce.Id,
                            Titre = d.Annonce.Titre,
                            Type = d.Annonce.Type,
                            Prix = d.Annonce.Prix,
                            Statut = d.Annonce.Statut,
                            Utilisateur = new UtilisateurSummaryDto
                            {
                                Id = d.Annonce.Utilisateur.Id,
                                Prenom = d.Annonce.Utilisateur.Prenom,
                                Nom = d.Annonce.Utilisateur.Nom
                            }
                        }
                    })
                    .ToListAsync();
                return Ok(cancelledDemands);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in GetCancelledDemands");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetCancelledDemands");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        /// <summary>
        /// Accepte une demande, crée une commande et une transaction associées.
        /// Corrige l'erreur de contrainte de clé étrangère et ajoute l'ID de l'adresse de livraison.
        /// </summary>
        [HttpPut("{id}/AccepterDemande")]
        public async Task<IActionResult> AccepterDemande(int id)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                {
                    return Unauthorized("Accès non autorisé.");
                }

                // 1. Charger la demande et l'annonce associée
                var demande = await _context.Demandes
                    .Include(d => d.Annonce)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (demande == null)
                {
                    return NotFound("Demande non trouvée.");
                }

                // Vérifier que l'utilisateur est bien le propriétaire de l'annonce
                if (demande.Annonce.UtilisateurId != userId)
                {
                    return Unauthorized("Vous n'êtes pas autorisé à accepter cette demande.");
                }

                // Vérifier que la demande est bien en attente
                if (demande.Statut != DemandeStatut.EnAttente)
                {
                    return BadRequest("La demande ne peut pas être acceptée dans son état actuel.");
                }

                // 2. Créer la transaction avant la commande
                var transaction = new Transactions
                {
                    AnnonceId = demande.AnnonceId,
                    Montant = demande.Annonce.Prix,
                    Date = DateTime.UtcNow,
                    Statut = "En attente", 
                    Type = demande.Annonce.Type.ToString()
                };
                _context.Transactions.Add(transaction);

                var commande = new Commande
                {
                    UtilisateurId = demande.DemandeurId,
                    AnnonceId = demande.AnnonceId,
                    DemandeId = demande.Id,
                    Transaction = transaction, 
                    Date = DateTime.UtcNow,
                    Statut = CommandeStatut.EnAttenteDeTraitement.ToString(),
                    ModeLivraison = "RemiseEnMainPropre", 
                    AdresseLivraisonId = demande.Annonce.AdresseLivraisonId 
                };
                _context.Commandes.Add(commande);

                // 4. Mettre à jour l'état de la demande et de l'annonce
                demande.Statut = DemandeStatut.Acceptee;

                // 5. Sauvegarder toutes les modifications en une seule fois
                await _context.SaveChangesAsync();

                _logger.LogInformation("Demande {DemandeId} accepted. Commande {CommandeId} created.", demande.Id, commande.Id);

                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Demandes.Any(d => d.Id == id))
                {
                    return NotFound("Demande non trouvée.");
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Une erreur inattendue est survenue lors de l'acceptation de la demande pour DemandeId: {DemandeId}", id);
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }


        [HttpPut("{id}/refuser")]
        public async Task<IActionResult> RefuserDemande(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var demande = await _context.Demandes
                    .Include(d => d.Annonce)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (demande == null)
                {
                    return NotFound("Demande non trouvée.");
                }

                if (demande.Annonce?.UtilisateurId != utilisateurId)
                {
                    return Forbid("Vous n'êtes pas autorisé à refuser cette demande.");
                }

                if (demande.Statut != DemandeStatut.EnAttente)
                {
                    return BadRequest("La demande ne peut être refusée que si son statut est 'EnAttente'.");
                }

                demande.Statut = DemandeStatut.Refusee;

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in RefuserDemande");
                return Unauthorized(ex.Message);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error in RefuserDemande. Inner Exception: {InnerExceptionMessage}", ex.InnerException?.Message);
                return StatusCode(500, $"Une erreur est survenue lors de la mise à jour de la demande. Veuillez réessayer. Détails techniques : {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in RefuserDemande for DemandeId: {DemandeId}", id);
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        [HttpPut("{id}/annuler")]
        public async Task<IActionResult> AnnulerDemande(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var demande = await _context.Demandes.FindAsync(id);
                if (demande == null)
                {
                    return NotFound("Demande non trouvée.");
                }

                if (demande.DemandeurId != utilisateurId)
                {
                    return Forbid("Vous n'êtes pas autorisé à annuler cette demande.");
                }

                if (demande.Statut != DemandeStatut.EnAttente)
                {
                    return BadRequest("Seules les demandes en attente peuvent être annulées.");
                }

                demande.Statut = DemandeStatut.Annulee;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in AnnulerDemande");
                return Unauthorized(ex.Message);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error in AnnulerDemande. Inner Exception: {InnerExceptionMessage}", ex.InnerException?.Message);
                return StatusCode(500, $"Une erreur est survenue lors de la mise à jour de la demande. Veuillez réessayer. Détails techniques : {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in AnnulerDemande for DemandeId: {DemandeId}", id);
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        /// <summary>
        /// Revokes the refusal of a demand, changing its status back to 'EnAttente'.
        /// </summary>
        /// <param name="id">The ID of the demand to revoke refusal for.</param>
        [HttpPut("{id}/revoquer-refus")]
        public async Task<IActionResult> RevoquerRefusDemande(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var demande = await _context.Demandes
                    .Include(d => d.Annonce)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (demande == null)
                {
                    return NotFound("Demande non trouvée.");
                }

                if (demande.Annonce?.UtilisateurId != utilisateurId)
                {
                    return Forbid("Vous n'êtes pas autorisé à révoquer le refus de cette demande.");
                }

                if (demande.Statut != DemandeStatut.Refusee)
                {
                    return BadRequest("La révocation du refus n'est possible que pour une demande refusée.");
                }

                demande.Statut = DemandeStatut.EnAttente;

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in RevoquerRefusDemande");
                return Unauthorized(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Demandes.Any(d => d.Id == id))
                {
                    return NotFound("Demande non trouvée.");
                }
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error in RevoquerRefusDemande");
                return StatusCode(500, $"Une erreur est survenue lors de la mise à jour de la demande. Veuillez réessayer. Détails techniques : {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in RevoquerRefusDemande for DemandeId: {DemandeId}", id);
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        [HttpPut("{id}/revoquer-acceptation")]
        public async Task<IActionResult> RevoquerAcceptationDemande(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var demande = await _context.Demandes
                    .Include(d => d.Annonce)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (demande == null)
                {
                    return NotFound("Demande non trouvée.");
                }

                if (demande.Annonce?.UtilisateurId != utilisateurId)
                {
                    return Forbid("Vous n'êtes pas autorisé à révoquer l'acceptation de cette demande.");
                }

                if (demande.Statut != DemandeStatut.Acceptee)
                {
                    return BadRequest("La révocation n'est possible que pour une demande acceptée.");
                }

                demande.Statut = DemandeStatut.EnAttente;

                if (demande.Annonce != null)
                {
                    demande.Annonce.Statut = "Disponible";
                    demande.Annonce.AcheteurId = null;

                    if (demande.Annonce.Type == TypeAnnonce.Location)
                    {
                        demande.Annonce.DateDebutLocation = null;
                        demande.Annonce.DateFinLocation = null;
                    }
                }

                // Remove the associated Commande when revoking the acceptance
                var commande = await _context.Commandes.FirstOrDefaultAsync(c => c.DemandeId == demande.Id);
                if (commande != null)
                {
                    _context.Commandes.Remove(commande);
                }

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access in RevoquerAcceptationDemande");
                return Unauthorized(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Demandes.Any(d => d.Id == id))
                {
                    return NotFound("Demande non trouvée.");
                }
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error in RevoquerAcceptationDemande");
                return StatusCode(500, $"Une erreur est survenue lors de la mise à jour de la demande. Veuillez réessayer. Détails techniques : {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in RevoquerAcceptationDemande for DemandeId: {DemandeId}", id);
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }
    
     /// <summary>
        /// Supprime une demande spécifique par ID pour un utilisateur autorisé.
        /// </summary>
        /// <param name="id">L'ID de la demande.</param>
        /// <returns>Le résultat de l'action.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDemande(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var demande = await _context.Demandes
                                            .Include(d => d.Annonce)
                                            .FirstOrDefaultAsync(d => d.Id == id);

                if (demande == null)
                {
                    return NotFound(new { Message = "Demande non trouvée." });
                }

                // Vérifier que l'utilisateur est soit le demandeur, soit le propriétaire de l'annonce
                // ou un administrateur.
                var isAdmin = User.FindFirst(ClaimTypes.Role)?.Value == RoleUtilisateur.Administrateur.ToString();
                if (demande.DemandeurId != utilisateurId && demande.Annonce.UtilisateurId != utilisateurId && !isAdmin)
                {
                    return StatusCode(403, new { Message = "Vous n'êtes pas autorisé à supprimer cette demande." });
                }

                // Si une commande est liée à cette demande, la supprimer en premier pour éviter les erreurs de contraintes
                var commandeLiee = await _context.Commandes.FirstOrDefaultAsync(c => c.DemandeId == id);
                if (commandeLiee != null)
                {
                    _context.Commandes.Remove(commandeLiee);
                }

                _context.Demandes.Remove(demande);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Erreur de base de données lors de la suppression de la demande {DemandeId}: {ErrorMessage}", id, ex.InnerException?.Message ?? ex.Message);
                return StatusCode(500, new { Message = "Une erreur est survenue lors de la suppression de la demande dans la base de données." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue lors de la suppression de la demande {DemandeId}: {ErrorMessage}", id, ex.Message);
                return StatusCode(500, new { Message = "Une erreur inattendue est survenue lors de la suppression de la demande. Veuillez réessayer plus tard." });
            }
        }
    }
}
