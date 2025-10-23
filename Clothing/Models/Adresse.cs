using System.Text.Json.Serialization;

namespace Clothing.Models
{
    public class Adresse
    {
        public int Id { get; set; }
        public int UtilisateurId { get; set; }
        public string Rue { get; set; }
        public string Ville { get; set; }
        public string CodePostal { get; set; }
        public string Pays { get; set; }
        public string TypeAdresse { get; set; }
        public bool EstPrincipale { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [JsonIgnore] 
        public Utilisateur Utilisateur { get; set; }
    }
}
