# ClothingApp — API de vente et location de vêtements entre particuliers (ASP.NET Core)

API REST d'une **marketplace de vêtements** : les utilisateurs publient des annonces de **vente** ou de **location**, envoient et gèrent des demandes, passent commande, paient, notent les articles et échangent par messages. Un administrateur valide les annonces et modère les comptes.

## Fonctionnalités

### Comptes et authentification
- Connexion **JWT** et **connexion Google via Firebase** (`firebase-login`)
- Mot de passe oublié (envoi d'e-mail par SMTP)
- Validation des données utilisateur avec **FluentValidation**
- Adresses de livraison multiples par utilisateur
- Administration : activation, suspension des comptes, signalement de litiges

### Annonces
- Annonces de type **Vente** ou **Location** : titre, description, prix, catégorie, taille, marque, état, photos
- **Validation des annonces** par l'administrateur avant publication
- **Évaluations** des annonces, avec note moyenne

### Demandes et commandes
- Demandes d'achat ou de location : envoi, acceptation, refus, annulation, révocation
- Vue « mes demandes » et « demandes reçues pour mes annonces »
- Commandes : choix du mode de livraison, suivi du statut, validation de la remise
- **Paiement** (`/api/Paiement/processus`, simulé) et historique des **transactions**

### Location
- **Service en arrière-plan** (`RentalExpirationService`) : chaque minute, les locations expirées repassent automatiquement en « Disponible »

### Autres
- **Messagerie** entre utilisateurs
- **Rapports** : statistiques globales (admin) et statistiques personnelles
- Documentation **Swagger** avec authentification JWT

## Stack technique

| Élément | Technologie |
|---|---|
| Framework | ASP.NET Core Web API (.NET 8) |
| ORM | Entity Framework Core 9 (migrations) |
| Base de données | SQL Server |
| Authentification | JWT Bearer, Firebase Admin SDK, Google Auth |
| Validation | FluentValidation |
| Documentation | Swashbuckle (Swagger) |

## Architecture

```
Clothing/
├── Controllers/              # Auth, Utilisateurs, Adresses, Annonces, Demande, Commandes,
│                             # Paiement, Transactions, Evaluations, Messages, Rapports
├── Models/                   # Entités EF Core + ClothingPlatformContext + validateurs
├── Migrations/               # Migrations EF Core
├── RentalExpirationService/  # Tâche planifiée d'expiration des locations
└── Program.cs                # Configuration (JWT, Swagger, EF Core, services)
```

## Lancer le projet

Prérequis : SDK .NET 8, SQL Server (ou LocalDB).

1. Configurer `Clothing/appsettings.json` :
   - `ConnectionStrings:DefaultConnection` (base `ClothingPlatformDB` par défaut)
   - `Jwt:Key`
   - `Firebase:ServiceAccountKeyPath` : chemin vers la clé du compte de service Firebase
   - `SmtpSettings` : serveur SMTP et identifiants pour les e-mails
2. Appliquer les migrations et lancer l'API :
   ```bash
   cd Clothing
   dotnet ef database update
   dotnet run
   ```
3. Ouvrir Swagger sur l'URL affichée dans la console, suivie de `/swagger`.

Le projet s'ouvre aussi dans Visual Studio via `Clothing.sln`.

## Auteur

**Yosra Dab** — Ingénieure logiciel Full Stack · [Portfolio](https://yosradab.vercel.app) · [GitHub](https://github.com/yosradab) · [LinkedIn](https://linkedin.com/in/yosradab)
