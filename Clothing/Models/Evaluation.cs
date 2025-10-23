using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Clothing.Models
{
    public class Evaluation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AnnonceId { get; set; }

        [Required]
        public int EvaluateurId { get; set; }

        [Required(ErrorMessage = "La note est requise.")]
        [Range(1, 5, ErrorMessage = "La note doit être comprise entre 1 et 5.")]
        public int Note { get; set; }

        [MaxLength(500)]
        public string? Commentaire { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.UtcNow;

        [ForeignKey("AnnonceId")]
        public Annonce? Annonce { get; set; }

        [ForeignKey("EvaluateurId")]
        public Utilisateur? Evaluateur { get; set; }
    }
}
