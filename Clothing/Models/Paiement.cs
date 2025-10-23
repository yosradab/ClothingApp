using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Clothing.Models
{
    /// <summary>
    /// Représente un paiement sur la plateforme.
    /// Ceci est une entité distincte des Transactions et des Commandes pour une meilleure granularité.
    /// Elle est liée à une transaction par une relation un-à-un.
    /// </summary>
    public class Paiement
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Le montant du paiement est requis.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Montant { get; set; }

        [Required(ErrorMessage = "La date et l'heure du paiement sont requises.")]
        public DateTime DateHeurePaiement { get; set; }

        [Required(ErrorMessage = "La méthode de paiement est requise.")]
        [MaxLength(50)]
        public string MethodePaiement { get; set; } // e.g., "Carte de crédit", "PayPal"

        [Required(ErrorMessage = "Le statut du paiement est requis.")]
        [MaxLength(50)]
        public string Statut { get; set; } // e.g., "Complété", "Échoué", "En attente"

        // Foreign key to the related Transaction
        [Required]
        public int TransactionId { get; set; }

        // Navigation property for the one-to-one relationship with Transaction
        [ForeignKey("TransactionId")]
        public Transactions? Transaction { get; set; }
    }
}
