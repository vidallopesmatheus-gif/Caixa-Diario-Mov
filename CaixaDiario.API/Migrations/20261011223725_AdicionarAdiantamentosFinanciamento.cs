using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAdiantamentosFinanciamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValueSql (não defaultValue: "") — string vazia não é JSON válido; já houve
            // bug exatamente assim com "entradas" (ver CorrigirEntradasJsonInvalido) e as linhas
            // existentes de financiamentos quebrariam a desserialização na primeira leitura.
            migrationBuilder.AddColumn<string>(
                name: "adiantamentos",
                table: "financiamentos",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "adiantamentos",
                table: "financiamentos");
        }
    }
}
