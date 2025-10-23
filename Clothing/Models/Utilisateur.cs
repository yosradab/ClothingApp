using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Clothing.Models
{

    public enum RoleUtilisateur
    {

        Invite,
        ClientInscrit,
        ClientStore,
        ClientPremium,
        Administrateur
    }


    public enum TypeAbonnementUtilisateur
    {

        Gratuit,

        Standard,

        Premium
    }

    public class Utilisateur
    {
        public int Id { get; set; }


        public string? TypeUtilisateur { get; set; }

        [Required(ErrorMessage = "Le nom est requis.")]
        [MaxLength(100)]
        public string Nom { get; set; }

        [Required(ErrorMessage = "Le prénom est requis.")]
        [MaxLength(100)]
        public string Prenom { get; set; }

        [Required(ErrorMessage = "L'email est requis.")]
        [EmailAddress(ErrorMessage = "L'email n'est pas valide.")]
        [MaxLength(255)]
        public string Email { get; set; }

        [Required(ErrorMessage = "Le mot de passe est requis.")]
        [MinLength(6, ErrorMessage = "Le mot de passe doit avoir au moins 6 caractères.")]
        [MaxLength(100)]
        public string MotDePasse { get; set; }

        [Required(ErrorMessage = "Le téléphone est requis.")]
        [MaxLength(20)]
        public string Telephone { get; set; }

        public DateTime? DateNaissance { get; set; }

        public TypeAbonnementUtilisateur? TypeAbonnement { get; set; }

        public DateTime? DateInscription { get; set; }

        [MaxLength(50)]
        public string? LanguePreferee { get; set; }

        [MaxLength(255)]
        public string? PreferencesNotification { get; set; }

        public double? Portefeuille { get; set; }

        public bool EstActif { get; set; } = true;

        // Propriétés de navigation
        public ICollection<Adresse> Adresses { get; set; } = new List<Adresse>();
        public ICollection<Annonce> Annonces { get; set; } = new List<Annonce>();
        public ICollection<Commande> Commandes { get; set; } = new List<Commande>();
        public ICollection<Message> MessagesEnvoyes { get; set; } = new List<Message>();
        public ICollection<Message> MessagesRecus { get; set; } = new List<Message>();
        public ICollection<Evaluation> EvaluationsEnvoyees { get; set; } = new List<Evaluation>();
        public ICollection<Evaluation> EvaluationsRecues { get; set; } = new List<Evaluation>();
    }
}
