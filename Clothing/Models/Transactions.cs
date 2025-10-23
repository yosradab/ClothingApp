using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Clothing.Models
{
    /// <summary>
    /// Représente une transaction financière sur la plateforme, liée à une annonce.
    /// Elle est maintenant liée à un paiement par une relation un-à-un.
    /// </summary>
    public class Transactions
    {
        public int Id { get; set; }

        [Required]
        public int AnnonceId { get; set; }

        // Changement de type pour correspondre au HasColumnType("decimal(10,2)")
        [Required(ErrorMessage = "Le montant de la transaction est requis.")]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal Montant { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Le statut de la transaction est requis.")]
        [MaxLength(50)]
        public string Statut { get; set; } // e.g., "Complétée", "En attente"

        [Required(ErrorMessage = "Le type de transaction est requis.")]
        [MaxLength(50)]
        public string Type { get; set; } // e.g., "Achat", "Location"

        // Propriétés de navigation
        public Annonce? Annonce { get; set; }
        public Commande? Commande { get; set; }

        // Navigation property for the one-to-one relationship with Paiement
        public Paiement? Paiement { get; set; }
    }
}
