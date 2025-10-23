using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Clothing.Models; // Assurez-vous que le namespace est correct

[Route("api/[controller]")]
[ApiController]
public class RapportsController : ControllerBase
{
    private readonly ClothingPlatformContext _context;
    private readonly ILogger<RapportsController> _logger;

    public RapportsController(ClothingPlatformContext context, ILogger<RapportsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Récupère des statistiques globales sur la plateforme.
    /// </summary>
    /// <returns>Un objet JSON contenant les statistiques.</returns>
    [HttpGet("statistiques-globales")]
    public async Task<IActionResult> GetStatistiquesGlobales()
    {
        try
        {
            var totalAnnonces = await _context.Annonces.CountAsync();
            var annoncesActives = await _context.Annonces.Where(a => a.Statut == "Active").CountAsync();
            var prixMoyenAnnonce = await _context.Annonces.AverageAsync(a => a.Prix);

            var totalVentes = await _context.Commandes.Where(c => c.Statut == "Livre").CountAsync();
            var chiffreAffaires = await _context.Commandes
                .Where(c => c.Statut == "Livre")
                .SumAsync(c => c.Transaction.Montant);

            var totalUtilisateurs = await _context.Utilisateurs.CountAsync();
            var utilisateursActifs = await _context.Utilisateurs.Where(u => u.EstActif).CountAsync();

            var totalLitiges = await _context.Utilisateurs.CountAsync();

            var rapport = new
            {
                TotalAnnonces = totalAnnonces,
                AnnoncesActives = annoncesActives,
                PrixMoyenAnnonce = prixMoyenAnnonce,
                TotalVentes = totalVentes,
                ChiffreAffairesTotal = chiffreAffaires,
                TotalUtilisateurs = totalUtilisateurs,
                UtilisateursActifs = utilisateursActifs,
                TotalLitiges = totalLitiges
            };

            return Ok(rapport);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des statistiques globales.");
            return StatusCode(500, new { message = "Erreur interne du serveur lors de la récupération des statistiques." });
        }
    }

    /// <summary>
    /// Récupère les statistiques spécifiques pour l'utilisateur connecté.
    /// </summary>
    /// <returns>Un objet JSON contenant les statistiques de l'utilisateur.</returns>
    [HttpGet("mes-statistiques")]
    public async Task<IActionResult> GetMesStatistiques()
    {
        try
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int utilisateurId))
            {
                return Unauthorized(new { message = "Utilisateur non authentifié ou ID manquant." });
            }

            var annoncesCrees = await _context.Annonces.CountAsync(a => a.UtilisateurId == utilisateurId);
            var annoncesVendues = await _context.Commandes
                .Where(c => c.Annonce.UtilisateurId == utilisateurId && c.Statut == "Livre")
                .CountAsync();

            var annoncesAchetees = await _context.Commandes
                .Where(c => c.UtilisateurId == utilisateurId && c.Statut == "Livre")
                .CountAsync();

            var chiffreAffairesVendeur = await _context.Commandes
                .Where(c => c.Annonce.UtilisateurId == utilisateurId && c.Statut == "Livre")
                .SumAsync(c => c.Transaction.Montant);

            var statistiquesUtilisateur = new
            {
                AnnoncesCrees = annoncesCrees,
                AnnoncesVendues = annoncesVendues,
                AnnoncesAchetees = annoncesAchetees,
                ChiffreAffairesVendeur = chiffreAffairesVendeur
            };

            return Ok(statistiquesUtilisateur);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des statistiques pour l'utilisateur connecté.");
            return StatusCode(500, new { message = "Erreur interne du serveur lors de la récupération des statistiques." });
        }
    }
}
