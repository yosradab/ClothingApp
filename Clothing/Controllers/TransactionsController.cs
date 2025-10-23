using Clothing.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Clothing.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly ClothingPlatformContext _context;

        public TransactionsController(ClothingPlatformContext context)
        {
            _context = context;
        }

        // GET: api/Transactions
        /// <summary>
        /// Retrieves a list of all transactions.
        /// </summary>
        /// <returns>A list of Transactions objects, including related Annonce data.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Transactions>>> GetTransactions()
        {
            return await _context.Transactions
                                 .Include(t => t.Annonce) // Include related Annonce data
                                 .ToListAsync();
        }

        // GET: api/Transactions/5
        /// <summary>
        /// Retrieves a specific transaction by its ID.
        /// </summary>
        /// <param name="id">The ID of the transaction to retrieve.</param>
        /// <returns>The Transactions object if found, otherwise NotFound.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Transactions>> GetTransaction(int id)
        {
            var transaction = await _context.Transactions
                                            .Include(t => t.Annonce)
                                            .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
            {
                return NotFound();
            }

            return transaction;
        }

        // POST: api/Transactions
        /// <summary>
        /// Creates a new transaction.
        /// </summary>
        /// <param name="transaction">The Transactions object to create.</param>
        /// <returns>The created Transactions object with its new ID.</returns>
        [HttpPost]
        public async Task<ActionResult<Transactions>> PostTransaction(Transactions transaction)
        {
            // Basic validation: ensure the AnnonceId exists
            var annonceExists = await _context.Annonces.AnyAsync(a => a.Id == transaction.AnnonceId);
            if (!annonceExists)
            {
                return BadRequest("Invalid AnnonceId. The associated advertisement does not exist.");
            }

            transaction.Date = DateTime.Now; // Automatically set the transaction date
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
        }

        // PUT: api/Transactions/5
        /// <summary>
        /// Updates an existing transaction.
        /// </summary>
        /// <param name="id">The ID of the transaction to update.</param>
        /// <param name="transaction">The updated Transactions object.</param>
        /// <returns>NoContent if successful, BadRequest if ID mismatch, NotFound if transaction not found.</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTransaction(int id, Transactions transaction)
        {
            if (id != transaction.Id)
            {
                return BadRequest();
            }

            // Validate AnnonceId if it's being updated
            var annonceExists = await _context.Annonces.AnyAsync(a => a.Id == transaction.AnnonceId);
            if (!annonceExists)
            {
                return BadRequest("Invalid AnnonceId. The associated advertisement does not exist.");
            }

            _context.Entry(transaction).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TransactionExists(id))
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

        // DELETE: api/Transactions/5
        /// <summary>
        /// Deletes a transaction by its ID.
        /// </summary>
        /// <param name="id">The ID of the transaction to delete.</param>
        /// <returns>NoContent if successful, NotFound if transaction not found.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
            {
                return NotFound();
            }

            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TransactionExists(int id)
        {
            return _context.Transactions.Any(e => e.Id == id);
        }
    }
}
