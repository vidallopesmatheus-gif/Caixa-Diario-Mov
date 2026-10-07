using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarValorVariavelEDiaVencimentoEmContaRecorrente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "dia_vencimento",
                table: "contas_recorrentes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "valor_variavel",
                table: "contas_recorrentes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dia_vencimento",
                table: "contas_recorrentes");

            migrationBuilder.DropColumn(
                name: "valor_variavel",
                table: "contas_recorrentes");
        }
    }
}
