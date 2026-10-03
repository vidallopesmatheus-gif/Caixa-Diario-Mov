using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarConfiguracaoFinanceiraFire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "custo_vida_mensal_manual",
                table: "usuarios",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "taxa_retirada_fire",
                table: "usuarios",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 4m);

            migrationBuilder.AddColumn<bool>(
                name: "eh_pessoal",
                table: "categorias",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "custo_vida_mensal_manual",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "taxa_retirada_fire",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "eh_pessoal",
                table: "categorias");
        }
    }
}
