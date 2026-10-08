using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class IndiceUnicoFitIdTransacaoImportada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O índice "IX_transacoes_importadas_conta_bancaria_id_fit_id" já existe desde a
            // migration AdicionarTransacoesImportadas (2026-06-24), mas NÃO único e nunca foi
            // modelado no EF (snapshot não sabia dele) — precisa cair antes de recriar como único
            // com o mesmo nome, senão o CREATE INDEX abaixo falha com "já existe".
            migrationBuilder.DropIndex(
                name: "IX_transacoes_importadas_conta_bancaria_id_fit_id",
                table: "transacoes_importadas");

            migrationBuilder.CreateIndex(
                name: "IX_transacoes_importadas_conta_bancaria_id_fit_id",
                table: "transacoes_importadas",
                columns: new[] { "conta_bancaria_id", "fit_id" },
                unique: true,
                filter: "fit_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transacoes_importadas_conta_bancaria_id_fit_id",
                table: "transacoes_importadas");

            // Restaura o índice não-único original.
            migrationBuilder.CreateIndex(
                name: "IX_transacoes_importadas_conta_bancaria_id_fit_id",
                table: "transacoes_importadas",
                columns: new[] { "conta_bancaria_id", "fit_id" },
                filter: "fit_id IS NOT NULL");
        }
    }
}
