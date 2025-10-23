using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Clothing.Models
{
    /// <summary>
    /// Représente un message envoyé entre deux utilisateurs.
    /// </summary>
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ExpediteurId { get; set; }

        [Required]
        public int DestinataireId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Contenu { get; set; }

        [Required]
        public DateTime DateEnvoi { get; set; }

        // Propriétés de navigation pour Entity Framework Core
        [ForeignKey("ExpediteurId")]
        public virtual Utilisateur Expediteur { get; set; }

        [ForeignKey("DestinataireId")]
        public virtual Utilisateur Destinataire { get; set; }
    }
}
