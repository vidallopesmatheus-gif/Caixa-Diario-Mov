using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPeriodicidadeEParcelas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "periodicidade",
                table: "contas_recorrentes",
                type: "text",
                nullable: false,
                defaultValue: "Mensal");

            migrationBuilder.AddColumn<int>(
                name: "quantidade_parcelas",
                table: "contas_recorrentes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "periodicidade",
                table: "contas_recorrentes");

            migrationBuilder.DropColumn(
                name: "quantidade_parcelas",
                table: "contas_recorrentes");
        }
    }
}
