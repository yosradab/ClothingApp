using Clothing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Clothing.Models
{
    public class ClothingPlatformContext : DbContext
    {
        public DbSet<Utilisateur> Utilisateurs { get; set; }
        public DbSet<Adresse> Adresses { get; set; }
        public DbSet<Annonce> Annonces { get; set; }
        public DbSet<Demande> Demandes { get; set; }
        public DbSet<Transactions> Transactions { get; set; }
        public DbSet<Commande> Commandes { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Evaluation> Evaluations { get; set; }
        public DbSet<Paiement> Paiements { get; set; }

        public ClothingPlatformContext(DbContextOptions<ClothingPlatformContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- Configuration for the Utilisateur (User) entity ---
            modelBuilder.Entity<Utilisateur>(entity =>
            {
                entity.ToTable("Utilisateurs");
                entity.HasKey(u => u.Id);

                // Property configurations
                entity.Property(u => u.TypeUtilisateur).HasMaxLength(50);
                entity.Property(u => u.Nom).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Prenom).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
                entity.Property(u => u.MotDePasse).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Telephone).IsRequired().HasMaxLength(20);

                // Convert the enum to a string for database storage
                entity.Property(u => u.TypeAbonnement).HasConversion<string>();

                entity.Property(u => u.LanguePreferee).HasMaxLength(50);
                entity.Property(u => u.PreferencesNotification).HasMaxLength(255);
                entity.Property(u => u.EstActif).IsRequired();
                entity.Property(u => u.DateInscription);
                entity.Property(u => u.Portefeuille).HasColumnType("decimal(18,2)");

                // Relationships: Using NoAction to prevent accidental deletion of a user
                // as long as they have associated data.
                entity.HasMany(u => u.Adresses)
                    .WithOne(a => a.Utilisateur)
                    .HasForeignKey(a => a.UtilisateurId)
                    .OnDelete(DeleteBehavior.Cascade); // CHANGED to Cascade to allow address deletion

                entity.HasMany(u => u.Annonces)
                    .WithOne(a => a.Utilisateur)
                    .HasForeignKey(a => a.UtilisateurId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(u => u.Commandes)
                    .WithOne(c => c.Utilisateur)
                    .HasForeignKey(c => c.UtilisateurId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(u => u.MessagesEnvoyes)
                    .WithOne(m => m.Expediteur)
                    .HasForeignKey(m => m.ExpediteurId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(u => u.MessagesRecus)
                    .WithOne(m => m.Destinataire)
                    .HasForeignKey(m => m.DestinataireId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(u => u.EvaluationsEnvoyees)
                    .WithOne(e => e.Evaluateur)
                    .HasForeignKey(e => e.EvaluateurId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // --- Configuration for the Adresse (Address) entity ---
            modelBuilder.Entity<Adresse>(entity =>
            {
                entity.ToTable("Adresses");
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Rue).IsRequired().HasMaxLength(255);
                entity.Property(a => a.Ville).IsRequired().HasMaxLength(100);
                entity.Property(a => a.CodePostal).IsRequired().HasMaxLength(20);
                entity.Property(a => a.Pays).IsRequired().HasMaxLength(100);
                entity.Property(a => a.TypeAdresse).IsRequired().HasMaxLength(50);
            });

            // --- Configuration for the Annonce (Listing) entity ---
            modelBuilder.Entity<Annonce>(entity =>
            {
                entity.ToTable("Annonces");
                entity.HasKey(a => a.Id);

                // Property configurations
                entity.Property(a => a.Titre).IsRequired().HasMaxLength(200);
                entity.Property(a => a.Description).HasMaxLength(1000);
                entity.Property(a => a.Prix).IsRequired().HasColumnType("decimal(18,2)");
                entity.Property(a => a.Categorie).IsRequired().HasMaxLength(100);
                entity.Property(a => a.Taille).IsRequired().HasMaxLength(50);
                entity.Property(a => a.Marque).IsRequired().HasMaxLength(100);
                entity.Property(a => a.Etat).IsRequired().HasMaxLength(50);

                // Convert the TypeAnnonce enum to a string
                entity.Property(a => a.Type).HasConversion<string>();

                entity.Property(a => a.DatePublication).IsRequired();
                entity.Property(a => a.EstValide).IsRequired();
                entity.Property(a => a.Statut).IsRequired().HasMaxLength(50);

                // Convert the Images collection to a JSON string for database storage
                var valueComparer = new ValueComparer<ICollection<string>>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList());

                entity.Property(a => a.Images)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<ICollection<string>>(v, (JsonSerializerOptions)null) ?? new List<string>(),
                        valueComparer);

                // Relationships
                entity.HasOne(a => a.AdresseLivraison)
                    .WithMany()
                    .HasForeignKey(a => a.AdresseLivraisonId)
 .IsRequired(false)                    
 .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(a => a.Acheteur)
                    .WithMany()
                    .HasForeignKey(a => a.AcheteurId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(a => a.Commandes)
                    .WithOne(c => c.Annonce)
                    .HasForeignKey(c => c.AnnonceId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(a => a.Evaluations)
                    .WithOne(e => e.Annonce)
                    .HasForeignKey(e => e.AnnonceId)
                    .OnDelete(DeleteBehavior.NoAction);

                // This relationship uses a cascade delete
                entity.HasMany(a => a.Demandes)
                    .WithOne(d => d.Annonce)
                    .HasForeignKey(d => d.AnnonceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // --- Configuration for the Demande (Request) entity ---
            modelBuilder.Entity<Demande>(entity =>
            {
                entity.ToTable("Demandes");
                entity.HasKey(d => d.Id);
                entity.Property(d => d.DateDemande).IsRequired();

                // Convert the DemandeStatut enum to a string
                entity.Property(d => d.Statut)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasConversion(
                        v => v.ToString(),
                        v => (DemandeStatut)Enum.Parse(typeof(DemandeStatut), v));

                // Relationships
                entity.HasOne(d => d.Demandeur)
                    .WithMany()
                    .HasForeignKey(d => d.DemandeurId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(d => d.Annonce)
                    .WithMany(a => a.Demandes)
                    .HasForeignKey(d => d.AnnonceId)
                    .OnDelete(DeleteBehavior.Cascade); // Matches the Annonce configuration
            });

            // --- Configuration for the Transactions entity ---
            modelBuilder.Entity<Transactions>(entity =>
            {
                entity.ToTable("Transactions");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Montant).IsRequired().HasColumnType("decimal(18,2)");
                entity.Property(t => t.Date).IsRequired();
                entity.Property(t => t.Statut).IsRequired().HasMaxLength(50);
                entity.Property(t => t.Type).IsRequired().HasMaxLength(50);

                // CORRECTED: One-to-one relationship with Paiement
                // A transaction should not be deleted if a payment is attached.
                entity.HasOne(t => t.Paiement)
                    .WithOne(p => p.Transaction)
                    .HasForeignKey<Paiement>(p => p.TransactionId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.NoAction); // Changed from Cascade to NoAction
            });

            // --- Configuration for the Commande (Order) entity ---
            modelBuilder.Entity<Commande>(entity =>
            {
                entity.ToTable("Commandes");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Date).IsRequired();
                entity.Property(c => c.Statut).IsRequired().HasMaxLength(50);

                // Relationships
                entity.HasOne(c => c.Utilisateur)
                    .WithMany(u => u.Commandes)
                    .HasForeignKey(c => c.UtilisateurId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(c => c.Annonce)
                    .WithMany(a => a.Commandes)
                    .HasForeignKey(c => c.AnnonceId)
                    .OnDelete(DeleteBehavior.NoAction);

                // CORRECTED: One-to-one relationship with Demande
                entity.HasOne(c => c.Demande)
                    .WithOne(d => d.Commande) // This is the corrected line
                    .HasForeignKey<Commande>(c => c.DemandeId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.NoAction);

                // One-to-one relationship with Transaction
                entity.HasOne(c => c.Transaction)
                    .WithOne(t => t.Commande)
                    .HasForeignKey<Commande>(c => c.TransactionId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(c => c.AdresseLivraison)
                    .WithMany()
                    .HasForeignKey(c => c.AdresseLivraisonId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // --- Configuration for the Message (Message) entity ---
            modelBuilder.Entity<Message>(entity =>
            {
                entity.ToTable("Messages");
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Contenu).IsRequired().HasMaxLength(1000);
                entity.Property(m => m.DateEnvoi).IsRequired();

                // Relationships
                entity.HasOne(m => m.Expediteur)
                    .WithMany(u => u.MessagesEnvoyes)
                    .HasForeignKey(m => m.ExpediteurId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(m => m.Destinataire)
                    .WithMany(u => u.MessagesRecus)
                    .HasForeignKey(m => m.DestinataireId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // --- Configuration for the Evaluation (Review) entity ---
            modelBuilder.Entity<Evaluation>(entity =>
            {
                entity.ToTable("Evaluations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Note).IsRequired().HasColumnType("int");
                entity.Property(e => e.Commentaire).HasMaxLength(500);
                entity.Property(e => e.Date).IsRequired();

                // Relationships
                entity.HasOne(e => e.Annonce)
                    .WithMany(a => a.Evaluations)
                    .HasForeignKey(e => e.AnnonceId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Evaluateur)
                    .WithMany(u => u.EvaluationsEnvoyees)
                    .HasForeignKey(e => e.EvaluateurId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // --- Configuration for the new Paiement entity ---
            modelBuilder.Entity<Paiement>(entity =>
            {
                entity.ToTable("Paiements");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Montant).IsRequired().HasColumnType("decimal(18,2)");
                entity.Property(p => p.DateHeurePaiement).IsRequired();
                entity.Property(p => p.MethodePaiement).IsRequired().HasMaxLength(50);
                entity.Property(p => p.Statut).IsRequired().HasMaxLength(50);
                entity.Property(p => p.TransactionId).IsRequired();

                // One-to-one relationship with Transaction
                // CORRECTED: The Paiement depends on the Transaction.
                // It should not delete the parent transaction.
                entity.HasOne(p => p.Transaction)
                    .WithOne(t => t.Paiement)
                    .HasForeignKey<Paiement>(p => p.TransactionId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.NoAction); // Changed from Cascade to NoAction
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
