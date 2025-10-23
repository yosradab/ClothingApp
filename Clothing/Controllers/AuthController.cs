using Clothing.Models;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly ClothingPlatformContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(ClothingPlatformContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;

        // Initialize Firebase Admin SDK if it hasn't been already.
        if (FirebaseApp.DefaultInstance == null)
        {
            try
            {
                var firebaseConfigPath = _configuration["Firebase:ServiceAccountKeyPath"];
                if (string.IsNullOrEmpty(firebaseConfigPath) || !System.IO.File.Exists(firebaseConfigPath))
                {
                    Console.WriteLine("Firebase:ServiceAccountKeyPath non configuré ou fichier non trouvé. L'intégration Firebase pourrait échouer.");
                }
                else
                {
                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = GoogleCredential.FromFile(firebaseConfigPath),
                    });
                    Console.WriteLine("Firebase Admin SDK initialisé.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'initialisation de Firebase Admin SDK : {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Hashes a password with a randomly generated salt using PBKDF2.
    /// This method is duplicated from UtilisateursController for self-contained functionality.
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

        string hashed = Convert.ToBase64String(Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 256 / 8));

        return $"{Convert.ToBase64String(salt)}.{hashed}";
    }

    /// <summary>
    /// Verifies a plain-text password against a stored hash and salt.
    /// </summary>
    /// <param name="enteredPassword">The password provided by the user.</param>
    /// <param name="storedHashWithSalt">The stored hash and salt string.</param>
    /// <returns>True if the passwords match, otherwise false.</returns>
    private bool VerifyPassword(string enteredPassword, string storedHashWithSalt)
    {
        var parts = storedHashWithSalt.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var storedHash = parts[1];

        string hashOfEnteredPassword = Convert.ToBase64String(Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivation.Pbkdf2(
            password: enteredPassword,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 256 / 8));

        return hashOfEnteredPassword == storedHash;
    }

    /// <summary>
    /// Generates a JWT token for an authenticated user.
    /// </summary>
    /// <param name="user">The user for whom to generate the token.</param>
    /// <returns>A JWT token as a string.</returns>
    private string GenerateJwtToken(Utilisateur user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);

        // Create a list of claims to include in the token payload.
        var claims = new List<Claim>
    {
        // Add the user's ID as a claim.
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        // Add the user's email as a claim.
        new Claim(ClaimTypes.Email, user.Email),
        // THIS IS THE CRITICAL LINE: Add the user's type as a claim.
        // Ensure this claim type matches what the front-end is looking for.
        new Claim("TypeUtilisateur", user.TypeUtilisateur)
    };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"]
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Handles standard email/password login.
    /// </summary>
    /// <param name="login">Login credentials.</param>
    /// <returns>A JWT token if successful, otherwise an unauthorized result.</returns>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginModel login)
    {
        var user = _context.Utilisateurs.FirstOrDefault(u => u.Email == login.Email);

        if (user == null)
        {
            return Unauthorized("Vous n'êtes pas inscrit.");
        }

        if (!VerifyPassword(login.Password, user.MotDePasse))
        {
            return Unauthorized("Mot de passe incorrect.");
        }

        if (!user.EstActif)
        {
            return Unauthorized("Votre compte est suspendu. Veuillez contacter l'administrateur.");
        }

        return Ok(new { token = GenerateJwtToken(user) });
    }

    /// <summary>
    /// Handles login using a Firebase ID token.  
    /// - Verifies the token with Firebase.  
    /// - Creates a new user if one doesn't exist.  
    /// - Returns a JWT token if successful.  
    /// </summary>
    /// <param name="firebaseLogin">The Firebase ID token model.</param>
    /// <returns>A JWT token if successful, otherwise an unauthorized or bad request result.</returns>
    [HttpPost("firebase-login")]
    public async Task<IActionResult> FirebaseLogin([FromBody] FirebaseLoginModel firebaseLogin)
    {
        if (string.IsNullOrEmpty(firebaseLogin.IdToken))
        {
            return BadRequest("Firebase ID token is required.");
        }

        FirebaseToken decodedToken;
        try
        {
            decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(firebaseLogin.IdToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Firebase token validation failed: {ex.Message}");
            return Unauthorized("Invalid Firebase token.");
        }

        string firebaseUid = decodedToken.Uid;
        string email = decodedToken.Claims.ContainsKey("email")
            ? decodedToken.Claims["email"].ToString()
            : null;

        if (string.IsNullOrEmpty(email))
        {
            return BadRequest("Email not found in Firebase token.");
        }

        var user = await _context.Utilisateurs.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new Utilisateur
            {
                Email = email,
                Nom = decodedToken.Claims.ContainsKey("name")
                    ? decodedToken.Claims["name"].ToString().Split(' ').LastOrDefault()
                    : "User",
                Prenom = decodedToken.Claims.ContainsKey("name")
                    ? decodedToken.Claims["name"].ToString().Split(' ').FirstOrDefault()
                    : "New",
                MotDePasse = HashPassword(Guid.NewGuid().ToString()),
                Telephone = "N/A",
                LanguePreferee = "English",
                PreferencesNotification = "Email",
                EstActif = true,
                DateInscription = DateTime.UtcNow,
                TypeUtilisateur = RoleUtilisateur.ClientInscrit.ToString(),
                TypeAbonnement = TypeAbonnementUtilisateur.Gratuit
            };

            _context.Utilisateurs.Add(user);
            await _context.SaveChangesAsync();
        }
        else
        {
            if (!user.EstActif)
            {
                return Unauthorized("Your account is suspended. Please contact support.");
            }
        }

        return Ok(new { token = GenerateJwtToken(user) });
    }


    /// <summary>
    /// Handles the forgot password flow by generating a new random password and emailing it to the user.
    /// </summary>
    /// <param name="model">The forgot password model containing the user's email.</param>
    /// <returns>A status indicating success or failure of the operation.</returns>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel model)
    {
        if (string.IsNullOrEmpty(model.Email))
        {
            return BadRequest("L'adresse e-mail est requise.");
        }

        var user = await _context.Utilisateurs.FirstOrDefaultAsync(u => u.Email == model.Email);

        if (user == null)
        {
            return NotFound("Vous n'êtes pas inscrit.");
        }

        string newPassword = GenerateRandomPassword();
        user.MotDePasse = HashPassword(newPassword);

        _context.Entry(user).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        bool emailSent = await SendPasswordByEmail(user.Email, newPassword);

        if (emailSent)
        {
            return Ok("Un nouveau mot de passe a été envoyé à votre adresse e-mail.");
        }
        else
        {
            return StatusCode(500, "Erreur lors de l'envoi du mot de passe par e-mail. Veuillez réessayer plus tard.");
        }
    }

    /// <summary>
    /// Generates a random password string of a specified length.
    /// </summary>
    /// <param name="length">The length of the password to generate.</param>
    /// <returns>A random password string.</returns>
    private string GenerateRandomPassword(int length = 12)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()";
        var random = new Random();
        var passwordBuilder = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            passwordBuilder.Append(chars[random.Next(chars.Length)]);
        }

        return passwordBuilder.ToString();
    }

    /// <summary>
    /// Sends an email with a new password to the recipient.
    /// </summary>
    /// <param name="recipientEmail">The email address of the recipient.</param>
    /// <param name="newPassword">The new password to send.</param>
    /// <returns>True if the email was sent successfully, otherwise false.</returns>
    private async Task<bool> SendPasswordByEmail(string recipientEmail, string newPassword)
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
                mailMessage.From = new MailAddress(senderEmail, "Votre Plateforme de Vêtements");
                mailMessage.To.Add(recipientEmail);
                mailMessage.Subject = "Réinitialisation de votre mot de passe";
                mailMessage.Body = $"Bonjour,\n\nVotre nouveau mot de passe est : {newPassword}\n\nVeuillez vous connecter et le changer immédiatement.\n\nCordialement,\nL'équipe de la Plateforme de Vêtements";
                mailMessage.IsBodyHtml = false;

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
    /// A model for the standard login request.
    /// </summary>
    public class LoginModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }

    /// <summary>
    /// A model for the Firebase login request.
    /// </summary>
    public class FirebaseLoginModel
    {
        [Required]
        public string IdToken { get; set; }
    }

    /// <summary>
    /// A model for the forgot password request.
    /// </summary>
    public class ForgotPasswordModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
