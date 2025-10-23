using Clothing.Models;
using FluentValidation;

public class UtilisateurValidator : AbstractValidator<Utilisateur>
{
    public UtilisateurValidator()
    {
        // These fields are required and must not be empty.
        RuleFor(u => u.Nom).NotEmpty();
        RuleFor(u => u.Prenom).NotEmpty();
        RuleFor(u => u.Email).NotEmpty().EmailAddress();
        RuleFor(u => u.MotDePasse).NotEmpty().MinimumLength(6);
        RuleFor(u => u.Telephone).NotEmpty();

    }
}
