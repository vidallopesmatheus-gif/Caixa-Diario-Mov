using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeNomeColunaDataAlvoParaSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A migration anterior criou a coluna como "DataAlvo" (PascalCase, entre aspas) por um
            // AddColumn com o nome errado — o mapeamento em AppDbContext.cs sempre esperou
            // data_alvo (snake_case, o padrão do resto do schema), causando
            // "column m.data_alvo does not exist" em runtime. RENAME preserva os dados já
            // gravados (inclusive o backfill de PrazoAnos feito na mesma migration anterior),
            // só corrige o nome físico da coluna.
            migrationBuilder.RenameColumn(
                name: "DataAlvo",
                table: "metas_anuais",
                newName: "data_alvo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "data_alvo",
                table: "metas_anuais",
                newName: "DataAlvo");
        }
    }
}
