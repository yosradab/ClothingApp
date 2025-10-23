using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clothing.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAddressDeletionBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Adresses_Utilisateurs_UtilisateurId",
                table: "Adresses");

            migrationBuilder.DropForeignKey(
                name: "FK_Paiements_Transactions_TransactionId",
                table: "Paiements");

            migrationBuilder.AddForeignKey(
                name: "FK_Adresses_Utilisateurs_UtilisateurId",
                table: "Adresses",
                column: "UtilisateurId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Paiements_Transactions_TransactionId",
                table: "Paiements",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Adresses_Utilisateurs_UtilisateurId",
                table: "Adresses");

            migrationBuilder.DropForeignKey(
                name: "FK_Paiements_Transactions_TransactionId",
                table: "Paiements");

            migrationBuilder.AddForeignKey(
                name: "FK_Adresses_Utilisateurs_UtilisateurId",
                table: "Adresses",
                column: "UtilisateurId",
                principalTable: "Utilisateurs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Paiements_Transactions_TransactionId",
                table: "Paiements",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
