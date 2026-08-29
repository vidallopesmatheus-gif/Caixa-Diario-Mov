using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDataAlvoMetaERemoverUnicidadeAno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DataAlvo",
                table: "metas_anuais",
                type: "date",
                nullable: true);

            // Metas "método" (objetivo) já cadastradas guardavam só um prazo relativo em anos —
            // sem uma data-alvo fixa. Calcula uma data equivalente a partir de quando a meta foi
            // criada, pra elas não perderem a configuração de prazo/ritmo já em uso.
            migrationBuilder.Sql(@"
                UPDATE metas_anuais
                SET ""DataAlvo"" = (criado_em::date + (""PrazoAnos"" || ' years')::interval)::date
                WHERE ""ModoMeta"" = 'metodo' AND ""PrazoAnos"" > 0 AND ""DataAlvo"" IS NULL;
            ");

            migrationBuilder.DropIndex(
                name: "IX_metas_anuais_cliente_id_ano",
                table: "metas_anuais");

            // O modo "simples" (Meta de Faturamento Mensal) continua 1-por-ano-civil; o modo
            // "metodo" (objetivos) passa a permitir N metas simultâneas pro mesmo cliente,
            // independente do ano — por isso a unicidade agora só vale quando ModoMeta = 'simples'.
            migrationBuilder.CreateIndex(
                name: "IX_metas_anuais_cliente_id_ano",
                table: "metas_anuais",
                columns: new[] { "cliente_id", "ano" },
                unique: true,
                filter: "\"ModoMeta\" = 'simples'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_metas_anuais_cliente_id_ano",
                table: "metas_anuais");

            migrationBuilder.CreateIndex(
                name: "IX_metas_anuais_cliente_id_ano",
                table: "metas_anuais",
                columns: new[] { "cliente_id", "ano" },
                unique: true);

            migrationBuilder.DropColumn(
                name: "DataAlvo",
                table: "metas_anuais");
        }
    }
}
