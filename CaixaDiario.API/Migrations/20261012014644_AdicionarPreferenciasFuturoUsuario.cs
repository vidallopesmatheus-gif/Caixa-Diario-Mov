using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPreferenciasFuturoUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReservaMinima",
                table: "usuarios",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoPessoa",
                table: "usuarios",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservaMinima",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "TipoPessoa",
                table: "usuarios");
        }
    }
}
