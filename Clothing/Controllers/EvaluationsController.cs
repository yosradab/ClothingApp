using Clothing.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Linq;

namespace Clothing.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EvaluationsController : ControllerBase
    {
        private readonly ClothingPlatformContext _context;

        public EvaluationsController(ClothingPlatformContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Evaluation>>> GetEvaluations()
        {
            return await _context.Evaluations
                                 .Include(e => e.Annonce)
                                 .Include(e => e.Evaluateur)
                                 .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Evaluation>> GetEvaluation(int id)
        {
            var evaluation = await _context.Evaluations
                                          .Include(e => e.Annonce)
                                          .Include(e => e.Evaluateur)
                                          .FirstOrDefaultAsync(e => e.Id == id);

            if (evaluation == null)
            {
                return NotFound();
            }

            return evaluation;
        }

        [HttpGet("ByAnnonce/{annonceId}")]
        public async Task<ActionResult<IEnumerable<Evaluation>>> GetEvaluationsByAnnonceId(int annonceId)
        {
            var annonceExists = await _context.Annonces.AnyAsync(a => a.Id == annonceId);
            if (!annonceExists)
            {
                return NotFound("Annonce non trouvée.");
            }

            return await _context.Evaluations
                                 .Where(e => e.AnnonceId == annonceId)
                                 .Include(e => e.Evaluateur)
                                 .ToListAsync();
        }

        [HttpGet("ByEvaluateur/{evaluateurId}")]
        public async Task<ActionResult<IEnumerable<Evaluation>>> GetEvaluationsByEvaluateurId(int evaluateurId)
        {
            var evaluateurExists = await _context.Utilisateurs.AnyAsync(u => u.Id == evaluateurId);
            if (!evaluateurExists)
            {
                return NotFound("Utilisateur évaluateur non trouvé.");
            }

            return await _context.Evaluations
                                 .Where(e => e.EvaluateurId == evaluateurId)
                                 .Include(e => e.Annonce)
                                 .ToListAsync();
        }

        [HttpGet("average/{annonceId}")]
        public async Task<ActionResult<decimal>> GetAverageRating(int annonceId)
        {
            var average = await _context.Evaluations
                                      .Where(e => e.AnnonceId == annonceId)
                                      .AverageAsync(e => (decimal?)e.Note) ?? 0;

            return Ok(average);
        }

        [HttpPost]
        [Authorize(Roles = "Acheteur,Loueur,Vendeur,Admnistrateur")]
        public async Task<ActionResult<Evaluation>> PostEvaluation(Evaluation evaluation)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null || evaluation.EvaluateurId != int.Parse(userId))
            {
                return Forbid("Vous n'êtes pas autorisé à évaluer pour un autre utilisateur.");
            }

            var annonceExists = await _context.Annonces.AnyAsync(a => a.Id == evaluation.AnnonceId && a.EstValide);
            var evaluateurExists = await _context.Utilisateurs.AnyAsync(u => u.Id == evaluation.EvaluateurId);

            if (!annonceExists || !evaluateurExists)
            {
                return BadRequest("AnnonceId ou EvaluateurId invalide. Vérifiez leur existence.");
            }

            if (evaluation.Note < 1 || evaluation.Note > 5)
            {
                return BadRequest("La note doit être entre 1 et 5.");
            }

            evaluation.Date = DateTime.UtcNow;
            _context.Evaluations.Add(evaluation);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEvaluation), new { id = evaluation.Id }, evaluation);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Acheteur,Loueur,Vendeur,Admnistrateur")]
        public async Task<IActionResult> PutEvaluation(int id, Evaluation evaluation)
        {
            if (id != evaluation.Id)
            {
                return BadRequest("L'ID de l'évaluation dans l'URL ne correspond pas à l'ID dans le corps.");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null || evaluation.EvaluateurId != int.Parse(userId))
            {
                return Forbid("Vous n'êtes pas autorisé à modifier cette évaluation.");
            }

            var annonceExists = await _context.Annonces.AnyAsync(a => a.Id == evaluation.AnnonceId);
            var evaluateurExists = await _context.Utilisateurs.AnyAsync(u => u.Id == evaluation.EvaluateurId);

            if (!annonceExists || !evaluateurExists || evaluation.Note < 1 || evaluation.Note > 5)
            {
                return BadRequest("Données invalides.");
            }

            _context.Entry(evaluation).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EvaluationExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Acheteur,Loueur,Vendeur,Admnistrateur")]
        public async Task<IActionResult> DeleteEvaluation(int id)
        {
            var evaluation = await _context.Evaluations.FindAsync(id);
            if (evaluation == null)
            {
                return NotFound();
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null && evaluation.EvaluateurId != int.Parse(userId) && User.IsInRole("Administrateur") == false)
            {
                return Forbid("Vous n'êtes pas autorisé à supprimer cette évaluation.");
            }

            _context.Evaluations.Remove(evaluation);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool EvaluationExists(int id)
        {
            return _context.Evaluations.Any(e => e.Id == id);
        }
    }
}