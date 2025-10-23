using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Clothing.Models
{
    public class Demande
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DemandeurId { get; set; }

        [Required]
        public int AnnonceId { get; set; }

        [Required]
        public DemandeStatut Statut { get; set; } = DemandeStatut.EnAttente;

        public DateTime DateDemande { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("DemandeurId")]
        public Utilisateur? Demandeur { get; set; }

        [ForeignKey("AnnonceId")]
        public Annonce? Annonce { get; set; }
        [JsonIgnore]
        public int? DureeLocationEnJours { get; internal set; }
        [JsonIgnore]
        // Navigation property for the one-to-one relationship with Commande
        public Commande? Commande { get; set; }
    }

    /// <summary>
    /// Enumeration of possible statuses for a request.
    /// Added "Canceled" state for clarity.
    /// </summary>
    public enum DemandeStatut
    {
        EnAttente,
        Acceptee,
        Refusee,
        Annulee
    }
}