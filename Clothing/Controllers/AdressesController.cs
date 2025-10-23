using Clothing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Clothing.Controllers
{
    // A Data Transfer Object (DTO) to handle incoming data for creating or updating an address.
    // This prevents model binding from failing on navigation properties like 'Utilisateur'.
    public class AdresseDto
    {
        public string Rue { get; set; }
        public string Ville { get; set; }
        public string CodePostal { get; set; }
        public string Pays { get; set; }
        public string TypeAdresse { get; set; }
        public bool EstPrincipale { get; set; }
        // Note: Id and UtilisateurId are intentionally omitted as they are set by the controller.
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Require authentication for all endpoints
    public class AdressesController : ControllerBase
    {
        private readonly ClothingPlatformContext _context;

        public AdressesController(ClothingPlatformContext context)
        {
            _context = context;
        }

        // Helper method to get UtilisateurId from JWT claims
        private int GetUtilisateurId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value
                             ?? User.FindFirst("UtilisateurId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var utilisateurId))
            {
                throw new UnauthorizedAccessException("Vous n'êtes pas connecté.");
            }

            return utilisateurId;
        }

        // GET: api/Adresses
        /// <summary>
        /// Retrieves all addresses for the authenticated user.
        /// </summary>
        /// <returns>A list of Adresse objects for the authenticated user.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Adresse>>> GetAdresses()
        {
            try
            {
                // Get the user's ID and return only their addresses
                var utilisateurId = GetUtilisateurId();
                return await _context.Adresses
                                     .Where(a => a.UtilisateurId == utilisateurId)
                                     .Include(a => a.Utilisateur)
                                     .ToListAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // GET: api/Adresses/5
        /// <summary>
        /// Retrieves a specific address by its ID for the authenticated user.
        /// </summary>
        /// <param name="id">The ID of the address to retrieve.</param>
        /// <returns>The Adresse object if found, otherwise NotFound.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Adresse>> GetAdresse(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var adresse = await _context.Adresses
                                            .Include(a => a.Utilisateur)
                                            .FirstOrDefaultAsync(a => a.Id == id && a.UtilisateurId == utilisateurId);

                if (adresse == null)
                {
                    return NotFound();
                }

                return adresse;
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // GET: api/Adresses/ByUser
        /// <summary>
        /// Retrieves all addresses for the authenticated user (replaces ByUser/{userId}).
        /// </summary>
        /// <returns>A list of Adresse objects associated with the authenticated user.</returns>
        [HttpGet("ByUser")]
        public async Task<ActionResult<IEnumerable<Adresse>>> GetAdressesByUser()
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                return await _context.Adresses
                                     .Where(a => a.UtilisateurId == utilisateurId)
                                     .ToListAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // POST: api/Adresses
        /// <summary>
        /// Creates a new address for the authenticated user.
        /// </summary>
        /// <param name="adresseDto">The AdresseDto object to create.</param>
        /// <returns>The created Adresse object with its new ID.</returns>
        [HttpPost]
        public async Task<ActionResult<Adresse>> PostAdresse(AdresseDto adresseDto)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();

                // Map DTO to the database model
                var adresse = new Adresse
                {
                    Rue = adresseDto.Rue,
                    Ville = adresseDto.Ville,
                    CodePostal = adresseDto.CodePostal,
                    Pays = adresseDto.Pays,
                    TypeAdresse = adresseDto.TypeAdresse,
                    EstPrincipale = adresseDto.EstPrincipale,
                    UtilisateurId = utilisateurId // Set the user ID from the JWT
                };

                // Basic validation for required fields
                if (string.IsNullOrEmpty(adresse.Rue) || string.IsNullOrEmpty(adresse.Ville) ||
                    string.IsNullOrEmpty(adresse.CodePostal) || string.IsNullOrEmpty(adresse.Pays))
                {
                    return BadRequest("All address fields (Rue, Ville, CodePostal, Pays) are required.");
                }

                _context.Adresses.Add(adresse);
                await _context.SaveChangesAsync();

                // Return the newly created Adresse object
                return CreatedAtAction(nameof(GetAdresse), new { id = adresse.Id }, adresse);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // PUT: api/Adresses/5
        /// <summary>
        /// Updates an existing address for the authenticated user.
        /// </summary>
        /// <param name="id">The ID of the address to update.</param>
        /// <param name="adresseDto">The updated AdresseDto object.</param>
        /// <returns>NoContent if successful, BadRequest if ID mismatch, NotFound if address not found.</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAdresse(int id, AdresseDto adresseDto)
        {
            try
            {
                var utilisateurId = GetUtilisateurId();
                var existingAdresse = await _context.Adresses
                                                    .FirstOrDefaultAsync(a => a.Id == id && a.UtilisateurId == utilisateurId);

                if (existingAdresse == null)
                {
                    return NotFound();
                }

                // Update fields from the DTO, preserving the original UtilisateurId and Id
                existingAdresse.Rue = adresseDto.Rue;
                existingAdresse.Ville = adresseDto.Ville;
                existingAdresse.CodePostal = adresseDto.CodePostal;
                existingAdresse.Pays = adresseDto.Pays;
                existingAdresse.TypeAdresse = adresseDto.TypeAdresse;
                existingAdresse.EstPrincipale = adresseDto.EstPrincipale;

                _context.Entry(existingAdresse).State = EntityState.Modified;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AdresseExists(id))
                    {
                        return NotFound();
                    }
                    throw;
                }

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        /// <summary>
        /// Deletes an address by its ID for the authenticated user, automatically
        /// disassociating any related Annonces and Commandes first.
        /// </summary>
        /// <param name="id">The ID of the address to delete.</param>
        /// <returns>NoContent if successful, NotFound if address not found.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAdresse(int id)
        {
            try
            {
                var utilisateurId = GetUtilisateurId1();

                if (utilisateurId == null)
                {
                    return Unauthorized("L'utilisateur n'est pas authentifié.");
                }

                var adresse = await _context.Adresses
                                            .FirstOrDefaultAsync(a => a.Id == id && a.UtilisateurId == utilisateurId.Value);

                if (adresse == null)
                {
                    return NotFound();
                }

                // Dissocier les annonces associées à cette adresse en définissant leur ID d'adresse sur null.
                var annoncesToUpdate = await _context.Annonces
                                                     .Where(a => a.AdresseLivraisonId == adresse.Id)
                                                     .ToListAsync();

                foreach (var annonce in annoncesToUpdate)
                {
                    annonce.AdresseLivraisonId = null;
                }

                // Dissocier les commandes associées à cette adresse en définissant leur ID d'adresse sur null.
                var commandesToUpdate = await _context.Commandes
                                                     .Where(c => c.AdresseLivraisonId == adresse.Id)
                                                     .ToListAsync();

                foreach (var commande in commandesToUpdate)
                {
                    commande.AdresseLivraisonId = null;
                }

                _context.Adresses.Remove(adresse);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "Une erreur inattendue est survenue.");
            }
        }

        /// <summary>
        /// Helper method to safely get the user ID from the claims.
        /// Returns null if the user is not authenticated or the claim is missing.
        /// </summary>
        private int? GetUtilisateurId1()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }

            return null;
        }
        private bool AdresseExists(int id)
        {
            return _context.Adresses.Any(e => e.Id == id);
        }
    }
}
