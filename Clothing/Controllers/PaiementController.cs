using Clothing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Clothing.Controllers
{
    /// <summary>
    /// Contrôleur API pour la gestion des paiements.
    /// Gère la création et le traitement des transactions de paiement.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PaiementController : ControllerBase
    {
        private readonly ClothingPlatformContext _context;
        private readonly ILogger<PaiementController> _logger;

        public PaiementController(ClothingPlatformContext context, ILogger<PaiementController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region DTOs for API endpoints

        /// <summary>
        /// DTO (Data Transfer Object) pour la demande de paiement.
        /// </summary>
        public class PaiementRequestDto
        {
            [Required(ErrorMessage = "L'ID de la commande est requis.")]
            public int CommandeId { get; set; }

            [Required(ErrorMessage = "Le mode de paiement est requis.")]
            [MaxLength(50)]
            public string ModeDePaiement { get; set; } // e.g., "Carte de crédit", "PayPal", etc.
        }

        #endregion

        /// <summary>
        /// Gère le traitement d'un paiement pour une commande donnée.
        /// Ceci est une implémentation simulée. Dans une application réelle,
        /// cela ferait appel à un service de paiement externe.
        /// </summary>
        /// <param name="paiementRequest">Les détails de la demande de paiement.</param>
        /// <returns>IActionResult indiquant le succès ou l'échec de la transaction.</returns>
        [HttpPost("processus")]
        public async Task<IActionResult> ProcessusPaiement([FromBody] PaiementRequestDto paiementRequest)
        {
            try
            {
                // Récupérer l'ID de l'utilisateur actuel
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                {
                    _logger.LogWarning("Unauthorized access attempt: User ID not found in claims.");
                    return Unauthorized("Accès non autorisé.");
                }

                _logger.LogInformation("Processing payment for CommandeId: {CommandeId} by user: {UserId}", paiementRequest.CommandeId, userId);

                // 1. Rechercher la commande
                var commande = await _context.Commandes
                    .Include(c => c.Annonce) // Inclure l'annonce pour obtenir le prix
                    .FirstOrDefaultAsync(c => c.Id == paiementRequest.CommandeId && c.UtilisateurId == userId);

                if (commande == null)
                {
                    _logger.LogWarning("Payment failed: Commande with ID {CommandeId} not found or doesn't belong to user {UserId}.", paiementRequest.CommandeId, userId);
                    return NotFound("Commande non trouvée ou non autorisée.");
                }

                // 2. Vérifier si la commande est dans le bon état pour être payée
                if (commande.Statut != CommandeStatut.EnAttenteDePaiement.ToString())
                {
                    _logger.LogWarning("Payment failed: Commande {CommandeId} is not in 'EnAttenteDePaiement' status. Current status: {CurrentStatus}", commande.Id, commande.Statut);
                    return BadRequest("La commande ne peut pas être payée dans son état actuel.");
                }

                // 3. Simuler le processus de paiement
                // Dans un scénario réel, cette partie appellerait une API de paiement (ex: Stripe, PayPal).
                var paiementReussi = true;

                if (paiementReussi)
                {
                    // 4. Créer une nouvelle transaction et l'ajouter au contexte
                    var transaction = new Transactions
                    {
                        AnnonceId = commande.AnnonceId,
                        Montant = commande.Annonce.Prix,
                        Date = DateTime.UtcNow,
                        Statut = "Complétée", // Ou un enum si vous en créez un
                        Type = commande.Annonce.Type.ToString()
                    };
                    _context.Transactions.Add(transaction);

                    // 5. Créer une nouvelle entité Paiement liée à la transaction
                    var paiement = new Paiement
                    {
                        Montant = commande.Annonce.Prix,
                        DateHeurePaiement = DateTime.UtcNow,
                        MethodePaiement = paiementRequest.ModeDePaiement,
                        Statut = "Complété", // Ou un enum si vous en créez un
                        Transaction = transaction // Utiliser la propriété de navigation pour lier
                    };
                    _context.Paiements.Add(paiement);

                    // 6. Mettre à jour la commande. Lier la commande à la transaction.
                    commande.Statut = CommandeStatut.EnAttenteDeTraitement.ToString();
                    commande.Transaction = transaction; // Utiliser la propriété de navigation pour lier

                    // Sauvegarder toutes les entités en une seule fois.
                    // EF Core gérera la création de la transaction, du paiement,
                    // et la mise à jour de la commande avec l'ID correct.
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Payment successful for CommandeId: {CommandeId}. TransactionId: {TransactionId}", commande.Id, transaction.Id);
                    return Ok(new { Message = "Paiement traité avec succès.", TransactionId = transaction.Id });
                }
                else
                {
                    // Gérer l'échec du paiement
                    _logger.LogWarning("Payment failed for CommandeId: {CommandeId} with mode: {Mode}", commande.Id, paiementRequest.ModeDePaiement);
                    // Vous pouvez mettre à jour la commande avec un statut d'échec de paiement ici
                    return BadRequest("Le paiement a échoué. Veuillez réessayer ou utiliser un autre mode de paiement.");
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error during payment for CommandeId: {CommandeId}", paiementRequest.CommandeId);
                return Conflict("Une erreur de concurrence est survenue. La commande a peut-être été modifiée par un autre processus. Veuillez réessayer.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ProcessusPaiement for CommandeId: {CommandeId}", paiementRequest.CommandeId);
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }
    }
}
