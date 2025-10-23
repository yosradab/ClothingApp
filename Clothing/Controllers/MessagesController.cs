using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Clothing.Models;

namespace Clothing.Controllers
{
    /// <summary>
    /// DTO pour la création d'un nouveau message.
    /// Empêche un utilisateur de définir des champs sensibles comme l'ID de l'expéditeur.
    /// </summary>
    public class MessageCreationDto
    {
        public int DestinataireId { get; set; }
        public string Contenu { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // S'assure que seul un utilisateur authentifié peut accéder à ce contrôleur
    public class MessagesController : ControllerBase
    {
        private readonly ClothingPlatformContext _context;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(ClothingPlatformContext context, ILogger<MessagesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Méthode d'assistance pour obtenir l'ID de l'utilisateur actuel à partir des revendications JWT
        private int GetUtilisateurId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            throw new UnauthorizedAccessException("Utilisateur non authentifié.");
        }

        /// <summary>
        /// Récupère tous les messages envoyés ou reçus par l'utilisateur actuel.
        /// </summary>
        /// <returns>Une liste des messages de l'utilisateur.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Message>>> GetMessages()
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                var messages = await _context.Messages
                    .Where(m => m.ExpediteurId == utilisateurId || m.DestinataireId == utilisateurId)
                    .Include(m => m.Expediteur)
                    .Include(m => m.Destinataire)
                    .ToListAsync();

                return Ok(messages);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Accès non autorisé dans GetMessages");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans GetMessages");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        /// <summary>
        /// Récupère un message spécifique par ID, si l'utilisateur est l'expéditeur ou le destinataire.
        /// </summary>
        /// <param name="id">L'ID du message.</param>
        /// <returns>Le message si trouvé.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Message>> GetMessage(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                var message = await _context.Messages
                    .Include(m => m.Expediteur)
                    .Include(m => m.Destinataire)
                    .FirstOrDefaultAsync(m => m.Id == id && (m.ExpediteurId == utilisateurId || m.DestinataireId == utilisateurId));

                if (message == null)
                {
                    return NotFound();
                }

                return Ok(message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Accès non autorisé dans GetMessage");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans GetMessage");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        /// <summary>
        /// Envoie un nouveau message. L'ID de l'expéditeur est automatiquement défini en fonction de l'utilisateur authentifié.
        /// </summary>
        /// <param name="messageDto">Le DTO contenant l'ID du destinataire et le contenu du message.</param>
        /// <returns>Le message créé.</returns>
        [HttpPost]
        public async Task<ActionResult<Message>> PostMessage(MessageCreationDto messageDto)
        {
            try
            {
                var expediteurId = GetUtilisateurId();

                var destinataireExists = await _context.Utilisateurs.AnyAsync(u => u.Id == messageDto.DestinataireId);

                if (!destinataireExists)
                {
                    return BadRequest("DestinataireId invalide. Le destinataire n'existe pas.");
                }

                // Crée le message à partir du DTO et des informations de l'utilisateur
                var message = new Message
                {
                    ExpediteurId = expediteurId,
                    DestinataireId = messageDto.DestinataireId,
                    Contenu = messageDto.Contenu,
                    DateEnvoi = DateTime.UtcNow // Utilise l'heure du serveur
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                // Récupère le message avec les entités de navigation pour la réponse
                var createdMessage = await _context.Messages
                    .Include(m => m.Expediteur)
                    .Include(m => m.Destinataire)
                    .FirstOrDefaultAsync(m => m.Id == message.Id);

                return CreatedAtAction(nameof(GetMessage), new { id = createdMessage.Id }, createdMessage);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Accès non autorisé dans PostMessage");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans PostMessage");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        /// <summary>
        /// Supprime un message par ID, si l'utilisateur est l'expéditeur.
        /// </summary>
        /// <param name="id">L'ID du message à supprimer.</param>
        /// <returns>Aucun contenu si réussi.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                var message = await _context.Messages.FindAsync(id);
                if (message == null)
                {
                    return NotFound();
                }

                if (message.ExpediteurId != utilisateurId)
                {
                    return Forbid("Vous n'êtes pas autorisé à supprimer ce message.");
                }

                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Accès non autorisé dans DeleteMessage");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans DeleteMessage");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }
    }
}
