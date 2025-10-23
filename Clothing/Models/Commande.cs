using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Clothing.Models
{
    // Énumérations pour les statuts et modes de livraison de la Commande
    public enum CommandeStatut
    {
        EnAttenteDePaiement,
        EnAttenteDeTraitement,
        Expediee,
        EnTransit,
        Livre,
        Annulee,
        PreteALaRemise,
        RetourDemande
    }

    public enum CommandeModeLivraison
    {
        Expedition,
        RemiseEnMainPropre
    }

    /// <summary>
    /// Représente une commande sur la plateforme.
    /// </summary>
    public class Commande
    {
        public int Id { get; set; }

        [Required]
        public int UtilisateurId { get; set; }

        [Required]
        public int AnnonceId { get; set; }

        [Required]
        public int DemandeId { get; set; }

        public int TransactionId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public string Statut { get; set; }

        [Required]
        public string ModeLivraison { get; set; }

        public int? AdresseLivraisonId { get; set; }

        public bool ValidationVendeur { get; set; } = false;
        public bool ValidationAcheteur { get; set; } = false;

        // Propriétés de navigation
        public Utilisateur Utilisateur { get; set; }
        public Annonce Annonce { get; set; }
        public Transactions Transaction { get; set; }
        public Adresse AdresseLivraison { get; set; }
        public Demande Demande { get; set; }
    }
}
