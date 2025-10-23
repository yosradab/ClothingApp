using Clothing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Clothing.Controllers
{
    /// <summary>
    /// Contrôleur API pour la gestion des annonces.
    /// Tous les endpoints de ce contrôleur nécessitent une authentification, sauf indication contraire.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AnnoncesController : ControllerBase
    {
        private readonly ClothingPlatformContext _context;
        private readonly IConfiguration _configuration;

        public AnnoncesController(ClothingPlatformContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        /// <summary>
        /// Méthode d'aide pour extraire l'ID de l'utilisateur des claims JWT.
        /// </summary>
        /// <returns>L'ID de l'utilisateur connecté.</returns>
        /// <exception cref="UnauthorizedAccessException">Levée si l'utilisateur n'est pas connecté ou si l'ID n'est pas valide.</exception>
        private int GetUtilisateurId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value
                             ?? User.FindFirst("UtilisateurId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var utilisateurId))
            {
                throw new UnauthorizedAccessException("L'ID de l'utilisateur n'a pas pu être trouvé ou est invalide.");
            }

            return utilisateurId;
        }

        /// <summary>
        /// Récupère toutes les annonces actives et valides.
        /// Les administrateurs peuvent voir toutes les annonces, y compris celles qui ne sont pas validées.
        /// </summary>
        /// <returns>Une liste de toutes les annonces disponibles.</returns>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<object>>> GetAnnonces()
        {
            try
            {
                var isAdmin = User.FindFirst(ClaimTypes.Role)?.Value == RoleUtilisateur.Administrateur.ToString();

                var query = _context.Annonces.AsQueryable();

                if (!isAdmin)
                {
                    query = query.Where(a => a.EstValide && a.Statut == "Disponible");
                }

                var annonces = await query
                    .Select(a => new
                    {
                        a.Id,
                        a.UtilisateurId,
                        a.Titre,
                        a.Description,
                        a.Prix,
                        a.Categorie,
                        a.Taille,
                        a.Marque,
                        a.Etat,
                        a.Type,
                        a.Images,
                        a.AdresseLivraisonId,
                        a.DateDebutLocation,
                        a.DateFinLocation,
                        a.DatePublication,
                        a.EstValide,
                        a.Statut,
                        Utilisateur = a.Utilisateur != null ? new
                        {
                            a.Utilisateur.Id,
                            a.Utilisateur.Nom,
                            a.Utilisateur.Prenom,
                            a.Utilisateur.Email,
                            a.Utilisateur.Telephone
                        } : null
                    })
                    .ToListAsync();

                if (!annonces.Any())
                {
                    return NotFound(new { Message = "Aucune annonce trouvée." });
                }

                return Ok(annonces);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération des annonces : {ex.Message}");
                return StatusCode(500, new { Message = "Une erreur est survenue lors de la récupération des annonces. Veuillez réessayer plus tard." });
            }
        }

        /// <summary>
        /// Récupère une annonce par son ID.
        /// </summary>
        /// <param name="id">L'ID de l'annonce à récupérer.</param>
        /// <returns>L'annonce correspondante.</returns>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetAnnonce(int id)
        {
            try
            {
                var annonce = await _context.Annonces
                    .Where(a => a.Id == id)
                    .Select(a => new
                    {
                        a.Id,
                        a.UtilisateurId,
                        a.Titre,
                        a.Description,
                        a.Prix,
                        a.Categorie,
                        a.Taille,
                        a.Marque,
                        a.Etat,
                        a.Type,
                        a.Images,
                        a.AdresseLivraisonId,
                        a.DateDebutLocation,
                        a.DateFinLocation,
                        a.DatePublication,
                        a.EstValide,
                        a.Statut,
                        Utilisateur = a.Utilisateur != null ? new
                        {
                            a.Utilisateur.Id,
                            a.Utilisateur.Nom,
                            a.Utilisateur.Prenom,
                            a.Utilisateur.Email,
                            a.Utilisateur.Telephone
                        } : null,
                        AdresseLivraison = a.AdresseLivraison != null ? new
                        {
                            a.AdresseLivraison.Id,
                            a.AdresseLivraison.Rue,
                            a.AdresseLivraison.Ville,
                            a.AdresseLivraison.CodePostal,
                            a.AdresseLivraison.Pays,
                            a.AdresseLivraison.TypeAdresse
                        } : null,
                        Evaluations = a.Evaluations.Select(e => new
                        {
                            e.Id,
                            e.Note,
                            e.Commentaire,
                            e.Date,
                            EvaluateurId = e.EvaluateurId
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (annonce == null)
                {
                    return NotFound(new { Message = "Annonce non trouvée." });
                }

                return Ok(annonce);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération de l'annonce {id} : {ex.Message}");
                return StatusCode(500, new { Message = "Une erreur est survenue lors de la récupération de l'annonce. Veuillez réessayer plus tard." });
            }
        }

        /// <summary>
        /// Récupère toutes les annonces d'un utilisateur spécifique.
        /// </summary>
        /// <param name="utilisateurId">L'ID de l'utilisateur.</param>
        /// <returns>Une liste d'annonces pour l'utilisateur spécifié.</returns>
        [HttpGet("utilisateur/{utilisateurId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetAnnoncesParUtilisateur(int utilisateurId)
        {
            try
            {
                var currentUserId = GetUtilisateurId();
                var isAdmin = User.FindFirst(ClaimTypes.Role)?.Value == RoleUtilisateur.Administrateur.ToString();

                if (currentUserId != utilisateurId && !isAdmin)
                {
                    return StatusCode(403, new { Message = "Vous n'avez pas l'autorisation d'accéder aux annonces de cet utilisateur." });
                }

                var annonces = await _context.Annonces
                    .Where(a => a.UtilisateurId == utilisateurId)
                    .Select(a => new
                    {
                        a.Id,
                        a.UtilisateurId,
                        a.Titre,
                        a.Description,
                        a.Prix,
                        a.Categorie,
                        a.Taille,
                        a.Marque,
                        a.Etat,
                        a.Type,
                        a.Images,
                        a.AdresseLivraisonId,
                        a.DateDebutLocation,
                        a.DateFinLocation,
                        a.DatePublication,
                        a.EstValide,
                        a.Statut
                    })
                    .ToListAsync();

                if (!annonces.Any())
                {
                    return NotFound(new { Message = "Aucune annonce trouvée pour cet utilisateur." });
                }

                return Ok(annonces);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(401, new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération des annonces pour l'utilisateur {utilisateurId} : {ex.Message}");
                return StatusCode(500, new { Message = "Une erreur est survenue lors de la récupération des annonces. Veuillez réessayer plus tard." });
            }
        }

        /// <summary>
        /// Crée une nouvelle annonce.
        /// </summary>
        /// <param name="annonceDto">Les données de la nouvelle annonce.</param>
        /// <returns>La nouvelle annonce créée.</returns>
        [HttpPost]
        public async Task<ActionResult<object>> PostAnnonce([FromBody] AnnonceCreateDto annonceDto)
        {
            try
            {
                // Validate the model
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new { Message = "Données invalides.", Errors = errors });
                }

                var utilisateurId = GetUtilisateurId();
                var utilisateur = await _context.Utilisateurs.FindAsync(utilisateurId);

                if (utilisateur == null)
                {
                    return Unauthorized(new { Message = "Utilisateur non trouvé." });
                }

                // Vérifier la limite d'annonces pour les utilisateurs avec un abonnement Gratuit
                if (utilisateur.TypeAbonnement.HasValue && utilisateur.TypeAbonnement.Value == TypeAbonnementUtilisateur.Gratuit)
                {
                    var annoncesCount = await _context.Annonces.CountAsync(a => a.UtilisateurId == utilisateurId);
                    if (annoncesCount >= 5)
                    {
                        return BadRequest(new { Message = "Les utilisateurs avec un abonnement Gratuit sont limités à 5 annonces." });
                    }
                }

                // Validation conditionnelle pour les annonces de location
                if (annonceDto.Type == TypeAnnonce.Location)
                {
                    if (!annonceDto.DateDebutLocation.HasValue || !annonceDto.DateFinLocation.HasValue)
                    {
                        return BadRequest(new { Message = "Les dates de début et de fin de location sont obligatoires pour une annonce de type 'Location'." });
                    }
                    if (annonceDto.DateDebutLocation.Value >= annonceDto.DateFinLocation.Value)
                    {
                        return BadRequest(new { Message = "La date de début de location doit être antérieure à la date de fin." });
                    }
                }

                // Vérifier si l'adresse de livraison appartient bien à l'utilisateur
                var adresse = await _context.Adresses
                    .Where(a => a.Id == annonceDto.AdresseLivraisonId && a.UtilisateurId == utilisateurId)
                    .FirstOrDefaultAsync();
                if (adresse == null)
                {
                    return BadRequest(new { Message = "L'adresse de livraison spécifiée n'existe pas ou n'appartient pas à l'utilisateur connecté." });
                }

                var annonce = new Annonce
                {
                    UtilisateurId = utilisateurId,
                    Titre = annonceDto.Titre,
                    Description = annonceDto.Description,
                    Prix = annonceDto.Prix,
                    Categorie = annonceDto.Categorie,
                    Taille = annonceDto.Taille,
                    Marque = annonceDto.Marque,
                    Etat = annonceDto.Etat,
                    Type = annonceDto.Type,
                    Images = annonceDto.Images ?? new List<string>(),
                    AdresseLivraisonId = annonceDto.AdresseLivraisonId,
                    DateDebutLocation = annonceDto.DateDebutLocation,
                    DateFinLocation = annonceDto.DateFinLocation,
                    DatePublication = DateTime.UtcNow,
                    EstValide = false,
                    Statut = "Disponible"
                };

                _context.Annonces.Add(annonce);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetAnnonce), new { id = annonce.Id }, new
                {
                    annonce.Id,
                    Message = "Annonce créée avec succès."
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"Erreur de base de données lors de la création de l'annonce : {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { Message = "Une erreur est survenue lors de l'enregistrement de l'annonce dans la base de données." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la création de l'annonce : {ex.Message}");
                return StatusCode(500, new { Message = "Une erreur inattendue est survenue lors de la création de l'annonce. Veuillez réessayer plus tard." });
            }
        }

        /// <summary>
        /// Met à jour une annonce existante.
        /// </summary>
        /// <param name="id">L'ID de l'annonce à mettre à jour.</param>
        /// <param name="annonceDto">Les données de mise à jour de l'annonce.</param>
        /// <returns>Le résultat de l'action.</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAnnonce(int id, [FromBody] AnnonceUpdateDto annonceDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new { Message = "Données invalides.", Errors = errors });
                }

                var utilisateurId = GetUtilisateurId();
                var annonce = await _context.Annonces.FindAsync(id);

                if (annonce == null)
                {
                    return NotFound(new { Message = "Annonce non trouvée." });
                }

                // Vérifier que l'utilisateur est bien le propriétaire de l'annonce
                if (annonce.UtilisateurId != utilisateurId)
                {
                    return StatusCode(403, new { Message = "Vous n'êtes pas autorisé à modifier cette annonce." });
                }

                // Validation conditionnelle pour les annonces de location
                if (annonceDto.Type.HasValue && annonceDto.Type.Value == TypeAnnonce.Location)
                {
                    if (!annonceDto.DateDebutLocation.HasValue || !annonceDto.DateFinLocation.HasValue)
                    {
                        return BadRequest(new { Message = "Les dates de début et de fin de location sont obligatoires pour une annonce de type 'Location'." });
                    }
                    if (annonceDto.DateDebutLocation.Value >= annonceDto.DateFinLocation.Value)
                    {
                        return BadRequest(new { Message = "La date de début de location doit être antérieure à la date de fin." });
                    }
                }

                // Mettre à jour les propriétés si elles sont fournies dans le DTO
                if (annonceDto.Titre != null) annonce.Titre = annonceDto.Titre;
                if (annonceDto.Description != null) annonce.Description = annonceDto.Description;
                if (annonceDto.Prix.HasValue) annonce.Prix = annonceDto.Prix.Value;
                if (annonceDto.Categorie != null) annonce.Categorie = annonceDto.Categorie;
                if (annonceDto.Taille != null) annonce.Taille = annonceDto.Taille;
                if (annonceDto.Marque != null) annonce.Marque = annonceDto.Marque;
                if (annonceDto.Etat != null) annonce.Etat = annonceDto.Etat;
                if (annonceDto.Type.HasValue) annonce.Type = annonceDto.Type.Value;
                if (annonceDto.Images != null) annonce.Images = annonceDto.Images;
                if (annonceDto.AdresseLivraisonId.HasValue)
                {
                    var adresse = await _context.Adresses
                        .Where(a => a.Id == annonceDto.AdresseLivraisonId.Value && a.UtilisateurId == utilisateurId)
                        .FirstOrDefaultAsync();
                    if (adresse == null)
                    {
                        return BadRequest(new { Message = "L'adresse de livraison spécifiée n'existe pas ou n'appartient pas à l'utilisateur connecté." });
                    }
                    annonce.AdresseLivraisonId = annonceDto.AdresseLivraisonId.Value;
                }
                // If not provided, keep the existing address
                if (annonceDto.DateDebutLocation.HasValue) annonce.DateDebutLocation = annonceDto.DateDebutLocation.Value;
                if (annonceDto.DateFinLocation.HasValue) annonce.DateFinLocation = annonceDto.DateFinLocation.Value;

                _context.Entry(annonce).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AnnonceExists(id))
                {
                    return NotFound(new { Message = "Annonce non trouvée." });
                }
                return StatusCode(500, new { Message = "Une erreur de concurrence est survenue lors de la mise à jour de l'annonce." });
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"Erreur de base de données lors de la mise à jour de l'annonce {id} : {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { Message = "Une erreur est survenue lors de la mise à jour de l'annonce dans la base de données." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la mise à jour de l'annonce {id} : {ex.Message}");
                return StatusCode(500, new { Message = "Une erreur inattendue est survenue lors de la mise à jour de l'annonce. Veuillez réessayer plus tard." });
            }
        }

        [HttpPatch("{id}/valider")]
        [Authorize(Roles = "Administrateur")]
        public async Task<IActionResult> ValiderAnnonce(int id)
        {
            try
            {
                // Debug claims
                var userClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}");
                Console.WriteLine($"User claims at {DateTime.Now}: {string.Join(", ", userClaims)}");

                var annonce = await _context.Annonces.FindAsync(id);
                if (annonce == null)
                {
                    return NotFound(new { Message = "Annonce non trouvée." });
                }

                if (annonce.EstValide)
                {
                    return BadRequest(new { Message = "Cette annonce a déjà été validée." });
                }

                annonce.EstValide = true;
                annonce.Statut = "Disponible";
                _context.Entry(annonce).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AnnonceExists(id))
                {
                    return NotFound(new { Message = "Annonce non trouvée." });
                }
                return StatusCode(500, new { Message = "Une erreur de concurrence est survenue lors de la validation de l'annonce." });
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"Erreur de base de données lors de la validation de l'annonce {id} : {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { Message = "Une erreur est survenue lors de la validation de l'annonce dans la base de données." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la validation de l'annonce {id} : {ex.Message}");
                return StatusCode(500, new { Message = "Une erreur inattendue est survenue lors de la validation de l'annonce. Veuillez réessayer plus tard." });
            }
        }
        /// <summary>
        /// Supprime une annonce.
        /// </summary>
        /// <param name="id">L'ID de l'annonce à supprimer.</param>
        /// <returns>Le résultat de l'action.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAnnonce(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                // Inclure les entités dépendantes pour pouvoir les supprimer d'abord
                var annonce = await _context.Annonces
                                            .Include(a => a.Commandes) // Chargement des commandes associées
                                            .Include(a => a.Evaluations) // Chargement des évaluations associées
                                            .FirstOrDefaultAsync(a => a.Id == id);

                if (annonce == null)
                {
                    // Retourne 404 si l'annonce n'existe pas
                    return NotFound(new { Message = "Annonce non trouvée." });
                }

                // Vérifier que l'utilisateur est bien le propriétaire de l'annonce ou un administrateur
                if (annonce.UtilisateurId != utilisateurId && User.FindFirst(ClaimTypes.Role)?.Value != RoleUtilisateur.Administrateur.ToString())
                {
                    return StatusCode(403, new { Message = "Vous n'êtes pas autorisé à supprimer cette annonce." });
                }

                // Les étapes suivantes sont cruciales pour éviter les erreurs de contraintes de clé étrangère
                // Supprimer d'abord les entités dépendantes qui ont DeleteBehavior.NoAction
                if (annonce.Commandes != null && annonce.Commandes.Any())
                {
                    _context.Commandes.RemoveRange(annonce.Commandes);
                }

                if (annonce.Evaluations != null && annonce.Evaluations.Any())
                {
                    _context.Evaluations.RemoveRange(annonce.Evaluations);
                }

                // Les Demandes sont supprimées en cascade (DeleteBehavior.Cascade),
                // mais il est préférable de les inclure pour plus de clarté
                if (annonce.Demandes != null && annonce.Demandes.Any())
                {
                    _context.Demandes.RemoveRange(annonce.Demandes);
                }

                // Enfin, supprimer l'annonce elle-même
                _context.Annonces.Remove(annonce);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // Logging de l'erreur détaillée pour le débogage
                Console.WriteLine($"Erreur de base de données lors de la suppression de l'annonce {id} : {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { Message = "Une erreur est survenue lors de la suppression de l'annonce dans la base de données." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la suppression de l'annonce {id} : {ex.Message}");
                return StatusCode(500, new { Message = "Une erreur inattendue est survenue lors de la suppression de l'annonce. Veuillez réessayer plus tard." });
            }
        }
        /// <summary>
        /// Ajoute une évaluation pour une annonce spécifique.
        /// L'utilisateur ne peut pas évaluer sa propre annonce.
        /// </summary>
        /// <param name="id">L'ID de l'annonce à évaluer.</param>
        /// <param name="evaluationDto">Les données de l'évaluation.</param>
        /// <returns>Le résultat de l'action.</returns>
        [HttpPost("{id}/evaluations")]
        public async Task<ActionResult> PostEvaluation(int id, [FromBody] EvaluationDto evaluationDto)
        {
            try
            {
                // Vérifier si le modèle est valide
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new { Message = "Données d'évaluation invalides.", Errors = errors });
                }

                var utilisateurId = GetUtilisateurId();
                var annonce = await _context.Annonces.FindAsync(id);

                if (annonce == null)
                {
                    return NotFound(new { Message = "Annonce non trouvée." });
                }

                // Empêcher l'utilisateur d'évaluer sa propre annonce
                if (annonce.UtilisateurId == utilisateurId)
                {
                    return BadRequest(new { Message = "Vous ne pouvez pas évaluer votre propre annonce." });
                }

                // Vérifier si l'utilisateur a déjà évalué cette annonce
                var existingEvaluation = await _context.Evaluations
                    .FirstOrDefaultAsync(e => e.AnnonceId == id && e.EvaluateurId == utilisateurId);

                if (existingEvaluation != null)
                {
                    return BadRequest(new { Message = "Vous avez déjà évalué cette annonce." });
                }

                var evaluation = new Evaluation
                {
                    AnnonceId = id,
                    EvaluateurId = utilisateurId,
                    Note = evaluationDto.Note,
                    Commentaire = evaluationDto.Commentaire,
                    Date = DateTime.UtcNow
                };

                _context.Evaluations.Add(evaluation);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Évaluation ajoutée avec succès.", EvaluationId = evaluation.Id });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'ajout d'une évaluation pour l'annonce {id} : {ex.Message}");
                return StatusCode(500, new { Message = "Une erreur inattendue est survenue lors de l'ajout de l'évaluation." });
            }
        }

        private bool AnnonceExists(int id)
        {
            return _context.Annonces.Any(e => e.Id == id);
        }
    }

    public class AnnonceCreateDto
    {
        [Required(ErrorMessage = "Le titre est requis.")]
        [MaxLength(200, ErrorMessage = "Le titre ne peut pas dépasser 200 caractères.")]
        public string Titre { get; set; }

        [MaxLength(1000, ErrorMessage = "La description ne peut pas dépasser 1000 caractères.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Le prix est requis.")]
        [Range(0.01, 1000000, ErrorMessage = "Le prix doit être compris entre 0.01 et 1,000,000.")]
        public decimal Prix { get; set; }

        [Required(ErrorMessage = "La catégorie est requise.")]
        [MaxLength(50, ErrorMessage = "La catégorie ne peut pas dépasser 50 caractères.")]
        public string Categorie { get; set; }

        [Required(ErrorMessage = "La taille est requise.")]
        [MaxLength(20, ErrorMessage = "La taille ne peut pas dépasser 20 caractères.")]
        public string Taille { get; set; }

        [Required(ErrorMessage = "La marque est requise.")]
        [MaxLength(100, ErrorMessage = "La marque ne peut pas dépasser 100 caractères.")]
        public string Marque { get; set; }

        [Required(ErrorMessage = "L'état est requis.")]
        [MaxLength(50, ErrorMessage = "L'état ne peut pas dépasser 50 caractères.")]
        public string Etat { get; set; }

        [Required(ErrorMessage = "Le type d'annonce (Vente ou Location) est requis.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TypeAnnonce Type { get; set; }

        [MaxLength(10, ErrorMessage = "La liste des images ne peut pas contenir plus de 10 éléments.")]
        public List<string>? Images { get; set; }

        [Required(ErrorMessage = "L'ID de l'adresse de livraison est requis.")]
        public int AdresseLivraisonId { get; set; }

        public DateTime? DateDebutLocation { get; set; }
        public DateTime? DateFinLocation { get; set; }
    }

    public class AnnonceUpdateDto
    {
        [MaxLength(200, ErrorMessage = "Le titre ne peut pas dépasser 200 caractères.")]
        public string? Titre { get; set; }

        [MaxLength(1000, ErrorMessage = "La description ne peut pas dépasser 1000 caractères.")]
        public string? Description { get; set; }

        [Range(0.01, 1000000, ErrorMessage = "Le prix doit être compris entre 0.01 et 1,000,000.")]
        public decimal? Prix { get; set; }

        [MaxLength(50, ErrorMessage = "La catégorie ne peut pas dépasser 50 caractères.")]
        public string? Categorie { get; set; }

        [MaxLength(20, ErrorMessage = "La taille ne peut pas dépasser 20 caractères.")]
        public string? Taille { get; set; }

        [MaxLength(100, ErrorMessage = "La marque ne peut pas dépasser 100 caractères.")]
        public string? Marque { get; set; }

        [MaxLength(50, ErrorMessage = "L'état ne peut pas dépasser 50 caractères.")]
        public string? Etat { get; set; }

        public TypeAnnonce? Type { get; set; }

        [MaxLength(10, ErrorMessage = "La liste des images ne peut pas contenir plus de 10 éléments.")]
        public List<string>? Images { get; set; }

        public int? AdresseLivraisonId { get; set; }

        public DateTime? DateDebutLocation { get; set; }
        public DateTime? DateFinLocation { get; set; }
    }
    public class EvaluationDto
    {
        [Required(ErrorMessage = "La note est requise.")]
        [Range(1, 5, ErrorMessage = "La note doit être comprise entre 1 et 5.")]
        public int Note { get; set; }

        [MaxLength(500, ErrorMessage = "Le commentaire ne peut pas dépasser 500 caractères.")]
        public string? Commentaire { get; set; }
    }
}