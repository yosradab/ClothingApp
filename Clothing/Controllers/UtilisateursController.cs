using Clothing.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using System.Linq;

[Route("api/[controller]")]
[ApiController]
public class UtilisateursController : ControllerBase
{
    private readonly ClothingPlatformContext _context;
    private readonly IConfiguration _configuration;

    public UtilisateursController(ClothingPlatformContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    /// <summary>
    /// Hashes a password with a randomly generated salt using PBKDF2.
    /// </summary>
    /// <param name="password">The plain-text password to hash.</param>
    /// <returns>A string containing the salt and the hashed password, separated by a dot.</returns>
    private string HashPassword(string password)
    {
        byte[] salt = new byte[128 / 8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 256 / 8));

        return $"{Convert.ToBase64String(salt)}.{hashed}";
    }

    /// <summary>
    /// Creates a new user account.
    /// </summary>
    /// <param name="newUser">The user data received from the client.</param>
    /// <returns>An ActionResult indicating success or failure.</returns>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> PostUtilisateur([FromBody] Utilisateur newUser)
    {
        // Check if an account with the same email already exists
        if (await _context.Utilisateurs.AnyAsync(u => u.Email == newUser.Email))
        {
            return BadRequest("Un compte avec cette adresse e-mail existe déjà.");
        }

        // Apply default values for optional properties if not provided
        if (string.IsNullOrEmpty(newUser.TypeUtilisateur))
        {
            newUser.TypeUtilisateur = RoleUtilisateur.ClientInscrit.ToString();
        }
        if (newUser.TypeAbonnement == null)
        {
            newUser.TypeAbonnement = TypeAbonnementUtilisateur.Gratuit;
        }
        if (string.IsNullOrEmpty(newUser.LanguePreferee))
        {
            newUser.LanguePreferee = "Français";
        }
        if (string.IsNullOrEmpty(newUser.PreferencesNotification))
        {
            newUser.PreferencesNotification = "Email";
        }
        newUser.EstActif = true;
        newUser.DateInscription = DateTime.UtcNow;

        // Hash the password before saving it to the database
        newUser.MotDePasse = HashPassword(newUser.MotDePasse);

        _context.Utilisateurs.Add(newUser);
        await _context.SaveChangesAsync();

        return CreatedAtAction("GetUtilisateur", new { id = newUser.Id }, newUser);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Utilisateur>> GetUtilisateur(int id)
    {
        var utilisateur = await _context.Utilisateurs.FindAsync(id);
        if (utilisateur == null)
        {
            return NotFound();
        }
        return utilisateur;
    }

    [HttpPut("activer/{id}")]
    [Authorize(Roles = "Administrateur")]
    public async Task<IActionResult> ActiverUtilisateur(int id)
    {
        var utilisateur = await _context.Utilisateurs.FindAsync(id);
        if (utilisateur == null)
        {
            return NotFound("Utilisateur non trouvé.");
        }

        utilisateur.EstActif = true;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("suspendre/{id}")]
    [Authorize(Roles = "Administrateur")]
    public async Task<IActionResult> SuspendreUtilisateur(int id)
    {
        var utilisateur = await _context.Utilisateurs.FindAsync(id);
        if (utilisateur == null)
        {
            return NotFound("Utilisateur non trouvé.");
        }

        utilisateur.EstActif = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Model for submitting a dispute
    public class LitigeModel
    {
        public int AnnonceId { get; set; }
        public string Sujet { get; set; }
        public string Contenu { get; set; }
    }

    [HttpPost("litige")]
    [Authorize(Roles = "ClientInscrit, ClientStore, ClientPremium")]
    // Corps de l'e-mail au format HTML pour une meilleure lisibilité
    public async Task<IActionResult> SoumettreLitige([FromBody] LitigeModel litige)
    {
        try
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int utilisateurId))
            {
                return Unauthorized("Utilisateur non authentifié ou ID manquant.");
            }

            var annonce = await _context.Annonces.FindAsync(litige.AnnonceId);
            if (annonce == null)
            {
                return NotFound("Annonce non trouvée.");
            }

            var expediteur = await _context.Utilisateurs.FindAsync(utilisateurId);
            if (expediteur == null)
            {
                return Unauthorized("Utilisateur non trouvé.");
            }

            var adminEmail = _configuration["AdminEmail"];
            if (string.IsNullOrEmpty(adminEmail))
            {
                Console.WriteLine("Erreur: L'adresse e-mail de l'administrateur n'est pas configurée.");
                return StatusCode(500, "Erreur interne du serveur. Veuillez réessayer plus tard.");
            }

            // Utilise la version HTML améliorée du corps de l'e-mail.
            string emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 20px auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 10px; background-color: #f9f9f9; }}
        h2 {{ color: #d9534f; }}
        .section {{ margin-bottom: 20px; }}
        .section h3 {{ border-bottom: 1px solid #ddd; padding-bottom: 5px; color: #555; }}
        ul {{ list-style-type: none; padding: 0; }}
        ul li {{ background-color: #fff; margin-bottom: 5px; padding: 10px; border-radius: 5px; border: 1px solid #eee; }}
        strong {{ color: #000; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>🚨 Nouveau litige signalé</h2>
        <p>Un nouveau litige a été soumis sur la plateforme. Voici les détails pour votre examen :</p>
        
        <div class='section'>
            <h3>Détails du litige</h3>
            <ul>
               
                <li><strong>Sujet :</strong> {litige.Sujet}</li>
                <li><strong>Date et heure du rapport :</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</li>
            </ul>
        </div>
        
        <div class='section'>
            <h3>Contenu du message</h3>
            <div style='background-color: #fff; padding: 15px; border: 1px solid #ddd; border-radius: 5px; white-space: pre-wrap;'>
                {litige.Contenu}
            </div>
        </div>
        
        <div class='section'>
            <h3>Informations sur l'expéditeur</h3>
            <ul>
                <li><strong>Nom :</strong> {expediteur.Nom} {expediteur.Prenom}</li>
                <li><strong>Email :</strong> {expediteur.Email}</li>
            </ul>
        </div>
        <p>Veuillez vous connecter à l'interface d'administration pour gérer ce litige.</p>
    </div>
</body>
</html>
";

            await SendEmailAsync(adminEmail, $"Nouveau litige: {litige.Sujet}", emailBody);

            return Ok("Litige soumis avec succès à l'administrateur.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors du traitement d'un litige: {ex.Message}");
            return StatusCode(500, "Une erreur est survenue lors de la soumission du litige.");
        }
    }


    private async Task<bool> SendEmailAsync(string recipientEmail, string subject, string body)
    {
        try
        {
            var smtpHost = _configuration["SmtpSettings:Host"];
            var smtpPort = int.Parse(_configuration["SmtpSettings:Port"]);
            var smtpUsername = _configuration["SmtpSettings:Username"];
            var smtpPassword = _configuration["SmtpSettings:Password"];
            var senderEmail = _configuration["SmtpSettings:SenderEmail"];
            var enableSsl = bool.Parse(_configuration["SmtpSettings:EnableSsl"]);

            using (SmtpClient client = new SmtpClient(smtpHost, smtpPort))
            {
                client.EnableSsl = enableSsl;
                client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;

                MailMessage mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(senderEmail, "Clothing Platform");
                mailMessage.To.Add(recipientEmail);
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;

                await client.SendMailAsync(mailMessage);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'envoi de l'e-mail à {recipientEmail}: {ex.Message}");
            return false;
        }
    }
    /// <summary>
    /// Retrieves a list of all users with optional pagination and sorting.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Utilisateur>>> GetAllUtilisateurs(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? sortBy = "id",
        [FromQuery] string? sortDirection = "desc")
    {
        var query = _context.Utilisateurs.AsQueryable();
        var totalUsers = await query.CountAsync();

        // Sorting logic based on the sortBy parameter
        if (!string.IsNullOrEmpty(sortBy))
        {
            switch (sortBy.ToLower())
            {
                case "name":
                    query = sortDirection == "asc" ? query.OrderBy(u => u.Nom) : query.OrderByDescending(u => u.Nom);
                    break;
                case "email":
                    query = sortDirection == "asc" ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email);
                    break;
                case "created_at":
                    query = sortDirection == "asc" ? query.OrderBy(u => u.DateInscription) : query.OrderByDescending(u => u.DateInscription);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(u => u.Id) : query.OrderByDescending(u => u.Id);
                    break;
            }
        }

        var utilisateurs = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        return Ok(new { data = utilisateurs, total = totalUsers });
    }
    /// <summary>
    /// Retrieves the profile of the currently authenticated user.
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<Utilisateur>> GetMyProfile()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int utilisateurId))
        {
            return Unauthorized("Impossible de trouver l'ID utilisateur dans le jeton.");
        }

        var utilisateur = await _context.Utilisateurs
            .FirstOrDefaultAsync(u => u.Id == utilisateurId);

        if (utilisateur == null)
        {
            return NotFound("Utilisateur non trouvé.");
        }

        return utilisateur;
    }
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUtilisateur(int id, [FromBody] Utilisateur updatedUser)
    {
        var utilisateur = await _context.Utilisateurs.FindAsync(id);
        if (utilisateur == null)
        {
            return NotFound("Utilisateur non trouvé.");
        }

        // Update fields (excluding password unless explicitly allowed)
        utilisateur.Nom = updatedUser.Nom;
        utilisateur.Prenom = updatedUser.Prenom;
        utilisateur.Email = updatedUser.Email;
        utilisateur.Telephone = updatedUser.Telephone;
        utilisateur.TypeUtilisateur = updatedUser.TypeUtilisateur ?? RoleUtilisateur.ClientInscrit.ToString();
        utilisateur.TypeAbonnement = updatedUser.TypeAbonnement ?? TypeAbonnementUtilisateur.Gratuit;
        utilisateur.LanguePreferee = updatedUser.LanguePreferee ?? "Français";
        utilisateur.PreferencesNotification = updatedUser.PreferencesNotification ?? "Email";
        utilisateur.EstActif = updatedUser.EstActif;

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
