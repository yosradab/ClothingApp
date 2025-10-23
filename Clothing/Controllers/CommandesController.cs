using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Clothing.Models;
using System.Security.Claims;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Authorization;

namespace Clothing.Controllers
{
    // DTOs définis en dehors de la classe du contrôleur, car ils ne sont pas dans votre fichier de modèle.
    public class CommandeDto
    {
        public int Id { get; set; }
        public int UtilisateurId { get; set; }
        public int AnnonceId { get; set; }
        public int DemandeId { get; set; }
        public int TransactionId { get; set; }
        public DateTime Date { get; set; }
        public string Statut { get; set; }
        public string ModeLivraison { get; set; }
        public int? AdresseLivraisonId { get; set; }
        public bool ValidationVendeur { get; set; }
        public bool ValidationAcheteur { get; set; }
    }

    public class ModeLivraisonUpdateDto
    {
        public string ModeLivraison { get; set; }
        public int? AdresseLivraisonId { get; set; }
    }

    public class CommandeStatusUpdateDto
    {
        public string Statut { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CommandeController : ControllerBase
    {
        private readonly ClothingPlatformContext _context;
        private readonly ILogger<CommandeController> _logger;
        private readonly IConfiguration _configuration;

        public CommandeController(ClothingPlatformContext context, ILogger<CommandeController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        // Helper method to get current user ID from JWT claims
        private int GetUtilisateurId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            throw new UnauthorizedAccessException("Utilisateur non authentifié.");
        }

        // Helper method to send email using SMTP settings from configuration
        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var smtpSettings = _configuration.GetSection("SmtpSettings");
                var fromAddress = new MailAddress(smtpSettings["SenderEmail"], "Clothing Platform");
                var toAddress = new MailAddress(toEmail);
                var smtp = new SmtpClient
                {
                    Host = smtpSettings["Host"],
                    Port = int.Parse(smtpSettings["Port"]),
                    EnableSsl = bool.Parse(smtpSettings["EnableSsl"]),
                    Credentials = new NetworkCredential(smtpSettings["Username"], smtpSettings["Password"])
                };

                using var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                await smtp.SendMailAsync(message);
                _logger.LogInformation("Email envoyé à {ToEmail}: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de l'email à {ToEmail}", toEmail);
                throw;
            }
        }

        /// <summary>
        /// Récupère toutes les commandes pour l'utilisateur actuel (en tant qu'acheteur et vendeur).
        /// </summary>
        /// <returns>Une liste de commandes.</returns>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult> GetCommandes()
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                var commandes = await _context.Commandes
                    .Where(c => c.UtilisateurId == utilisateurId || c.Annonce.UtilisateurId == utilisateurId)
                    .Select(c => new CommandeDto
                    {
                        Id = c.Id,
                        UtilisateurId = c.UtilisateurId,
                        AnnonceId = c.AnnonceId,
                        DemandeId = c.DemandeId,
                        TransactionId = c.TransactionId,
                        Date = c.Date,
                        Statut = c.Statut,
                        ModeLivraison = c.ModeLivraison,
                        AdresseLivraisonId = c.AdresseLivraisonId,
                        ValidationVendeur = c.ValidationVendeur,
                        ValidationAcheteur = c.ValidationAcheteur
                    })
                    .ToListAsync();

                return Ok(commandes);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Accès non autorisé dans GetCommandes");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans GetCommandes");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }
        /// <summary>
        /// Récupère une commande spécifique par ID pour un utilisateur autorisé.
        /// </summary>
        /// <param name="id">L'ID de la commande.</param>
        /// <returns>La commande si trouvée et si l'utilisateur est autorisé.</returns>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetCommande(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                // Combiner la requête de récupération et la vérification de l'autorisation
                // en une seule requête à la base de données.
                var commande = await _context.Commandes
                    .Include(c => c.Annonce) // Inclure Annonce pour accéder à Annonce.UtilisateurId
                    .Where(c => c.Id == id && (c.UtilisateurId == utilisateurId || c.Annonce.UtilisateurId == utilisateurId))
                    .Select(c => new CommandeDto
                    {
                        Id = c.Id,
                        UtilisateurId = c.UtilisateurId,
                        AnnonceId = c.AnnonceId,
                        DemandeId = c.DemandeId,
                        TransactionId = c.TransactionId,
                        Date = c.Date,
                        Statut = c.Statut,
                        ModeLivraison = c.ModeLivraison,
                        AdresseLivraisonId = c.AdresseLivraisonId,
                        ValidationVendeur = c.ValidationVendeur,
                        ValidationAcheteur = c.ValidationAcheteur
                    })
                    .FirstOrDefaultAsync();

                if (commande == null)
                {
                    // Si la commande n'est pas trouvée ou si l'utilisateur n'est pas autorisé,
                    // retourner une erreur 404. Cela évite de révéler l'existence de commandes
                    // auxquelles l'utilisateur n'a pas accès.
                    return NotFound("Commande non trouvée ou accès non autorisé.");
                }

                return Ok(commande);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Accès non autorisé dans GetCommande");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans GetCommande");
                return StatusCode(500, new { message = "Une erreur interne est survenue.", details = ex.Message });
            }
        }


        /// <summary>
        /// Met à jour le mode de livraison d'une commande existante.
        /// </summary>
        /// <param name="id">L'ID de la commande.</param>
        /// <param name="updateDto">L'objet de mise à jour contenant le nouveau mode de livraison.</param>
        /// <returns>Aucun contenu si réussi.</returns>
        [HttpPut("{id}/mode-livraison")]
        [Authorize]
        public async Task<IActionResult> UpdateModeLivraison(int id, [FromBody] ModeLivraisonUpdateDto updateDto)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                var commande = await _context.Commandes
                    .Include(c => c.Annonce)
                        .ThenInclude(a => a.Utilisateur)
                    .Include(c => c.Utilisateur)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (commande == null)
                {
                    return NotFound("Commande non trouvée.");
                }

                if (commande.UtilisateurId != utilisateurId && commande.Annonce.UtilisateurId != utilisateurId)
                {
                    return Forbid("Vous n'êtes pas autorisé à modifier le mode de livraison.");
                }

                if (commande.Statut != CommandeStatut.EnAttenteDePaiement.ToString() && commande.Statut != CommandeStatut.EnAttenteDeTraitement.ToString())
                {
                    return BadRequest("Le mode de livraison ne peut être modifié qu'avant l'expédition.");
                }

                if (!Enum.TryParse<CommandeModeLivraison>(updateDto.ModeLivraison, out var modeLivraison))
                {
                    return BadRequest("Mode de livraison invalide. Doit être 'Expedition' ou 'RemiseEnMainPropre'.");
                }

                if (modeLivraison == CommandeModeLivraison.Expedition && !updateDto.AdresseLivraisonId.HasValue)
                {
                    return BadRequest("Une adresse de livraison est requise pour l'expédition.");
                }

                commande.ModeLivraison = modeLivraison.ToString();
                commande.AdresseLivraisonId = updateDto.AdresseLivraisonId;

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Accès non autorisé dans UpdateModeLivraison");
                return Unauthorized(ex.Message);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Erreur de base de données dans UpdateModeLivraison");
                return StatusCode(500, $"Erreur de base de données : {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans UpdateModeLivraison");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }

        /// <summary>
        /// Met à jour le statut d'une commande (pour le suivi et la gestion).
        /// </summary>
        /// <param name="id">L'ID de la commande.</param>
        /// <param name="updateDto">L'objet de mise à jour contenant le nouveau statut.</param>
        /// <returns>Aucun contenu si réussi.</returns>
        [HttpPut("{id}/UpdateCommandeStatus")]
        [Authorize]
        public async Task<IActionResult> UpdateCommandeStatus(int id, [FromBody] CommandeStatusUpdateDto updateDto)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                var commande = await _context.Commandes
                    .Include(c => c.Annonce)
                        .ThenInclude(a => a.Utilisateur)
                    .Include(c => c.Utilisateur)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (commande == null)
                {
                    return NotFound("Commande non trouvée.");
                }

                var isSeller = commande.Annonce.UtilisateurId == utilisateurId;
                var isBuyer = commande.UtilisateurId == utilisateurId;

                if (!isSeller && !isBuyer)
                {
                    return Forbid("Vous n'êtes ni le vendeur ni l'acheteur de cette commande.");
                }

                if (!Enum.TryParse<CommandeStatut>(updateDto.Statut, out var newStatus))
                {
                    return BadRequest("Statut invalide.");
                }

                switch (newStatus)
                {
                    case CommandeStatut.Expediee:
                    case CommandeStatut.EnTransit:
                    case CommandeStatut.PreteALaRemise:
                        if (!isSeller)
                        {
                            return Forbid("Seul le vendeur peut mettre à jour le statut de la commande à 'Expédiée', 'En Transit' ou 'Prête à la remise'.");
                        }
                        break;
                    case CommandeStatut.Annulee:
                        if (!isBuyer)
                        {
                            return Forbid("Seul l'acheteur peut annuler la commande.");
                        }
                        break;
                    case CommandeStatut.Livre:
                        return BadRequest("Le statut 'Livre' ne peut pas être mis à jour directement via cet endpoint.");

                    default:
                        return BadRequest("Mise à jour de ce statut non autorisée.");
                }

                commande.Statut = updateDto.Statut;

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Accès non autorisé dans UpdateCommandeStatus");
                return Unauthorized(ex.Message);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Erreur de base de données dans UpdateCommandeStatus");
                return StatusCode(500, $"Erreur de base de données : {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans UpdateCommandeStatus");
                return StatusCode(500, $"Une erreur inattendue est survenue : {ex.Message}");
            }
        }
        /// <summary>
        /// Valide la remise en main propre pour une commande.
        /// Nécessite les validations des deux parties pour confirmer la livraison.
        /// Envoie des confirmations par email aux deux parties lorsque pleinement validé.
        /// </summary>
        /// <param name="id">L'ID de la commande.</param>
        /// <returns>Aucun contenu si réussi.</returns>
        [HttpPost("{id}/valider-remise")]
        [Authorize]
        public async Task<IActionResult> ValiderRemise(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                // Récupérer la commande avec les relations nécessaires
                var commande = await _context.Commandes
                    .Include(c => c.Annonce)
                        .ThenInclude(a => a.Utilisateur)
                    .Include(c => c.Utilisateur)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (commande == null)
                {
                    return NotFound(new { message = "Commande introuvable." });
                }

                bool isSeller = commande.Annonce?.UtilisateurId == utilisateurId;
                bool isBuyer = commande.UtilisateurId == utilisateurId;

                if (!isSeller && !isBuyer)
                {
                    return Forbid("Vous n'êtes ni le vendeur ni l'acheteur de cette commande.");
                }

                // Vérifier le mode de livraison et le statut actuel de la commande
                if (commande.ModeLivraison != CommandeModeLivraison.RemiseEnMainPropre.ToString())
                {
                    return BadRequest(new { message = "Cette commande n'est pas une remise en main propre." });
                }

                if (commande.Statut == CommandeStatut.Livre.ToString() || commande.Statut == CommandeStatut.Annulee.ToString())
                {
                    return BadRequest(new { message = "La commande a déjà été complétée ou annulée." });
                }

                // Mettre à jour la validation en fonction du rôle
                if (isSeller)
                {
                    if (commande.ValidationVendeur)
                        return BadRequest(new { message = "Le vendeur a déjà validé cette remise." });

                    commande.ValidationVendeur = true;
                }
                else // isBuyer
                {
                    if (commande.ValidationAcheteur)
                        return BadRequest(new { message = "L'acheteur a déjà validé cette remise." });

                    commande.ValidationAcheteur = true;
                }

                // Déterminer le nouveau statut de la commande
                if (commande.ValidationVendeur && commande.ValidationAcheteur)
                {
                    // Les deux parties ont validé, la transaction est complète
                    commande.Statut = CommandeStatut.Livre.ToString();

                    if (commande.Annonce != null)
                    {
                        commande.Annonce.Statut = commande.Annonce.Type == TypeAnnonce.Vente ? "Vendu" : "Louee";
                    }

                    // Vérifications de nullité pour les e-mails
                    var buyerEmail = commande.Utilisateur?.Email;
                    var sellerEmail = commande.Annonce?.Utilisateur?.Email;
                    var annonceTitre = commande.Annonce?.Titre ?? "Article non spécifié";

                    // Envoi des e-mails de confirmation (sans bloquer la transaction en cas d'échec)
                    if (!string.IsNullOrEmpty(buyerEmail) && !string.IsNullOrEmpty(sellerEmail))
                    {
                        var currentTime = DateTime.UtcNow;
                        var subject = $"Confirmation de livraison - Commande #{commande.Id}";

                        var buyerBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 20px auto; padding: 20px; border: 1px solid #dcdcdc; border-radius: 10px; background-color: #f8f8f8; text-align: center; }}
        h2 {{ color: #556B2F; }}
        .highlight {{ color: #BDB76B; font-weight: bold; }}
        .button {{
            background-color: #BDB76B;
            color: white;
            padding: 12px 24px;
            text-decoration: none;
            border-radius: 5px;
            font-weight: bold;
            display: inline-block;
            margin-top: 15px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>✅ Félicitations ! Livraison confirmée !</h2>
        <p>Bonjour,</p>
        <p>Nous sommes ravis de vous informer que votre commande <span class='highlight'>#{commande.Id}</span> pour l'article <span class='highlight'>'{annonceTitre}'</span> a été confirmée comme livrée par les deux parties. Nous espérons que l'article correspond à vos attentes !</p>
        <p><strong>Date de confirmation :</strong> {currentTime:dd/MM/yyyy HH:mm:ss}</p>
        <p>Merci d'avoir fait confiance à notre plateforme. Nous espérons vous revoir bientôt !</p>
        <a href='#' class='button'>Laisser un avis</a>
    </div>
</body>
</html>";

                        var sellerBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 20px auto; padding: 20px; border: 1px solid #dcdcdc; border-radius: 10px; background-color: #f8f8f8; text-align: center; }}
        h2 {{ color: #556B2F; }}
        .highlight {{ color: #BDB76B; font-weight: bold; }}
        .button {{
            background-color: #BDB76B;
            color: white;
            padding: 12px 24px;
            text-decoration: none;
            border-radius: 5px;
            font-weight: bold;
            display: inline-block;
            margin-top: 15px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>🎉 Vente confirmée ! Félicitations !</h2>
        <p>Bonjour,</p>
        <p>Félicitations ! Votre article <span class='highlight'>'{annonceTitre}'</span>, vendu via la commande <span class='highlight'>#{commande.Id}</span>, a été confirmé comme livré par les deux parties.</p>
        <p><strong>Date de confirmation :</strong> {currentTime:dd/MM/yyyy HH:mm:ss}</p>
        <p>Le paiement est en cours de traitement et vous sera transféré sous peu. Merci d'avoir utilisé notre plateforme !</p>
        <a href='#' class='button'>Voir mes ventes</a>
    </div>
</body>
</html>";

                        try
                        {
                            await SendEmailAsync(buyerEmail, subject, buyerBody);
                            await SendEmailAsync(sellerEmail, subject, sellerBody);
                        }
                        catch (Exception emailEx)
                        {
                            // L'échec de l'envoi d'e-mail ne doit pas empêcher la transaction de se terminer.
                            _logger.LogError(emailEx, "Erreur lors de l'envoi des e-mails de confirmation pour la commande #{CommandeId}.", commande.Id);
                        }
                    }
                }
                else
                {
                    // Un seul des deux a validé, mise à jour du statut intermédiaire
                    if (isSeller)
                    {
                        commande.Statut = CommandeStatut.EnTransit.ToString();
                    }
                    else // isBuyer
                    {
                        commande.Statut = CommandeStatut.PreteALaRemise.ToString();
                    }
                }

                await _context.SaveChangesAsync();

                // Retourner l'objet commande mis à jour pour que le front-end puisse l'afficher
                return Ok(new { message = "Remise validée avec succès.", commande });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Accès non autorisé dans ValiderRemise");
                return Unauthorized(new { message = "Vous n'êtes pas autorisé à effectuer cette action." });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Erreur de concurrence de la base de données dans ValiderRemise. La commande a peut-être été modifiée par un autre utilisateur.");
                return StatusCode(409, new { message = "La commande a été modifiée par un autre utilisateur. Veuillez la recharger et réessayer." });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Erreur de base de données dans ValiderRemise.");
                return StatusCode(500, new { message = "Erreur de base de données.", details = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue dans ValiderRemise.");
                return StatusCode(500, new { message = "Une erreur interne est survenue. Détails : " + ex.Message });
            }
        }

        /// <summary>
        /// Supprime une commande spécifique par ID pour un utilisateur autorisé.
        /// </summary>
        /// <param name="id">L'ID de la commande.</param>
        /// <returns>Le résultat de l'action.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCommande(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var isAdmin = User.FindFirst(ClaimTypes.Role)?.Value == RoleUtilisateur.Administrateur.ToString();
                var commande = await _context.Commandes
                                             .Include(c => c.Demande)
                                             .Include(c => c.Annonce)
                                             .FirstOrDefaultAsync(c => c.Id == id);

                if (commande == null)
                {
                    return NotFound(new { Message = "Commande non trouvée." });
                }

                // Vérifier que l'utilisateur est soit l'acheteur, le vendeur ou un administrateur.
                if (commande.UtilisateurId != utilisateurId && commande.Annonce.UtilisateurId != utilisateurId && !isAdmin)
                {
                    return StatusCode(403, new { Message = "Vous n'êtes pas autorisé à supprimer cette commande." });
                }

                // Si une demande est liée, la supprimer en premier si nécessaire
                if (commande.Demande != null)
                {
                    _context.Demandes.Remove(commande.Demande);
                }

                // Supprimer la commande elle-même
                _context.Commandes.Remove(commande);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Erreur de base de données lors de la suppression de la commande {CommandeId}: {ErrorMessage}", id, ex.InnerException?.Message ?? ex.Message);
                return StatusCode(500, new { Message = "Une erreur est survenue lors de la suppression de la commande dans la base de données." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue lors de la suppression de la commande {CommandeId}: {ErrorMessage}", id, ex.Message);
                return StatusCode(500, new { Message = "Une erreur inattendue est survenue lors de la suppression de la commande. Veuillez réessayer plus tard." });
            }
        }
    }
}
