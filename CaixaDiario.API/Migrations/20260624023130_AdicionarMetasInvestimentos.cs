using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMetasInvestimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MesInicio",
                table: "metas_anuais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ModoMeta",
                table: "metas_anuais",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PeriodoMeses",
                table: "metas_anuais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrazoAnos",
                table: "metas_anuais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Sonho",
                table: "metas_anuais",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxaRetorno",
                table: "metas_anuais",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalInvestido",
                table: "metas_anuais",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorSonho",
                table: "metas_anuais",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MesInicio",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "ModoMeta",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "PeriodoMeses",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "PrazoAnos",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "Sonho",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "TaxaRetorno",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "TotalInvestido",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "ValorSonho",
                table: "metas_anuais");
        }
    }
}
