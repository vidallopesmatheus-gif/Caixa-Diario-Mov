using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class SeedCategoriaAmortizacaoFinanciamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fase 6.7: FinanciamentoService lança a amortização de cada parcela sob estas duas
            // categorias (a de Consórcio é usada só quando Financiamento.Tipo == "Consorcio") — sem
            // elas o combobox de categorias ficaria sem opção pra uma saída que, por TipoCusto, já
            // cai em Atividades de Financiamento de qualquer forma. Busca o grupo pelo NOME (não por
            // id fixo) e só insere se ainda não existir (idempotente), igual SeedCategoriaRendimentos.
            var idAmortizacao = Guid.NewGuid();
            migrationBuilder.Sql($@"
                INSERT INTO categorias (id, nome, tipo, grupo_id, ordem, ativa, criado_em)
                SELECT '{idAmortizacao}', 'Amortização de financiamento', 'Financiamento', g.id, 0, true, NOW()
                FROM grupos g
                WHERE g.nome = 'Empréstimos/Financiamentos'
                AND NOT EXISTS (SELECT 1 FROM categorias WHERE nome = 'Amortização de financiamento');
            ");

            var idConsorcio = Guid.NewGuid();
            migrationBuilder.Sql($@"
                INSERT INTO categorias (id, nome, tipo, grupo_id, ordem, ativa, criado_em)
                SELECT '{idConsorcio}', 'Parcela de consórcio', 'Financiamento', g.id, 0, true, NOW()
                FROM grupos g
                WHERE g.nome = 'Consórcios'
                AND NOT EXISTS (SELECT 1 FROM categorias WHERE nome = 'Parcela de consórcio');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM categorias
                WHERE nome = 'Amortização de financiamento'
                AND grupo_id = (SELECT id FROM grupos WHERE nome = 'Empréstimos/Financiamentos');
            ");
            migrationBuilder.Sql(@"
                DELETE FROM categorias
                WHERE nome = 'Parcela de consórcio'
                AND grupo_id = (SELECT id FROM grupos WHERE nome = 'Consórcios');
            ");
        }
    }
}
