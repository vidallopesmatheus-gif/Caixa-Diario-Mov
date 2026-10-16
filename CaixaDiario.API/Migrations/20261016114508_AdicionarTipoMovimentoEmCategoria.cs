using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTipoMovimentoEmCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo_movimento",
                table: "categorias",
                type: "text",
                nullable: false,
                defaultValue: "Ambos");

            // Backfill: reproduz, pra cada categoria já cadastrada, exatamente a mesma regra que
            // antes vivia em CategoriaService.EhEntrada/EhSaida (computada em tempo de leitura a
            // partir de Tipo + Bloco do grupo) — essa migration só move essa regra pra dentro da
            // coluna nova, não muda o comportamento de nenhuma categoria existente. A
            // reclassificação por padrão real de uso (item 8 da rodada de ajustes Money Vision) é
            // um passo manual separado, revisado via relatório antes de aplicar (ver
            // ReclassificacaoCategoriasService) — nunca automática numa migration de schema.
            migrationBuilder.Sql(@"
                UPDATE categorias c
                SET tipo_movimento = CASE
                    WHEN (c.tipo IN ('Receita','Investimento','Financiamento') OR g.bloco = 'DEDUÇÕES DA RECEITA')
                         AND c.tipo IN ('CustoVariavel','CustoFixo','DespesaNaoOperacional','Investimento','Financiamento','RetiradaSocios')
                    THEN 'Ambos'
                    WHEN c.tipo IN ('Receita','Investimento','Financiamento') OR g.bloco = 'DEDUÇÕES DA RECEITA'
                    THEN 'Entrada'
                    WHEN c.tipo IN ('CustoVariavel','CustoFixo','DespesaNaoOperacional','Investimento','Financiamento','RetiradaSocios')
                    THEN 'Saida'
                    ELSE 'Ambos'
                END
                FROM grupos g
                WHERE c.grupo_id = g.id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo_movimento",
                table: "categorias");
        }
    }
}
