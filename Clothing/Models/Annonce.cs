using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Clothing.Models
{
    // Énumération pour le type d'annonce (Vente ou Location)
    public enum TypeAnnonce
    {
        [JsonStringEnumMemberName("Vente")]
        Vente = 0,
        [JsonStringEnumMemberName("Location")]
        Location = 1
    }


    public class Annonce
    {
        [Key]
        public int Id { get; set; }

        // Clé étrangère pour l'utilisateur qui a créé l'annonce (obligatoire)
        [Required]
        public int UtilisateurId { get; set; }

        [Required(ErrorMessage = "Le titre est requis.")]
        [MaxLength(200)]
        public string Titre { get; set; }

        // La description n'est pas toujours obligatoire
        public string? Description { get; set; }

        [Required(ErrorMessage = "Le prix est requis.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Prix { get; set; }

        [Required(ErrorMessage = "La catégorie est requise.")]
        [MaxLength(50)]
        public string Categorie { get; set; }

        [Required(ErrorMessage = "La taille est requise.")]
        [MaxLength(20)]
        public string Taille { get; set; }

        [Required(ErrorMessage = "La marque est requise.")]
        [MaxLength(100)]
        public string Marque { get; set; }

        [Required(ErrorMessage = "L'état est requis.")]
        [MaxLength(50)]
        public string Etat { get; set; }

        // Utilisation de l'énumération TypeAnnonce pour le type
        [Required(ErrorMessage = "Le type d'annonce (Vente ou Location) est requis.")]
        public TypeAnnonce Type { get; set; }

        // Nouvelle propriété pour la disponibilité
        public bool EstValide { get; set; } = false;

        // Nouvelle propriété pour gérer le statut de l'annonce : "Disponible", "Loué", "Vendu"
        [MaxLength(50)]
        public string Statut { get; set; } = "Disponible";

        // ID de l'utilisateur qui a acheté ou loué l'annonce (facultatif)
        public int? AcheteurId { get; set; }

        // Dates pour la location (facultatives, rendues obligatoires par validation conditionnelle)
        // Note: La validation conditionnelle sera gérée dans le contrôleur ou un DTO.
        public DateTime? DateDebutLocation { get; set; }
        public DateTime? DateFinLocation { get; set; }

        // Date de publication de l'annonce
        public DateTime DatePublication { get; set; } = DateTime.UtcNow;

        // La propriété 'images' est une collection de chaînes.
        public ICollection<string> Images { get; set; } = new List<string>();

        // L'ID de l'adresse de livraison est maintenant requis
        public int? AdresseLivraisonId { get; set; }

        // Propriétés de navigation
        [ForeignKey("UtilisateurId")]
        public Utilisateur? Utilisateur { get; set; }

        [ForeignKey("AdresseLivraisonId")]
        public Adresse? AdresseLivraison { get; set; }

        [ForeignKey("AcheteurId")]
        public Utilisateur? Acheteur { get; set; }

        public ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
        public ICollection<Commande> Commandes { get; set; } = new List<Commande>();
        public ICollection<Demande> Demandes { get; set; } = new List<Demande>();
    }
}