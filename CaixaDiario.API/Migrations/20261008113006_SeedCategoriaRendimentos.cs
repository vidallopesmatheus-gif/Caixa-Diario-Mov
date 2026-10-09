using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class SeedCategoriaRendimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Item 1.14: o grupo "Rendimento de Investimentos" existe desde a migration
            // AdicionarHierarquiaBlocoGrupo, mas nunca teve nenhuma categoria cadastrada dentro —
            // na prática o combobox não tinha nada selecionável pra classificar rendimento de
            // investimento (ex.: juros de CDB, dividendos). Busca o grupo pelo NOME (não por um id
            // fixo) pra funcionar em qualquer ambiente, e só insere se ainda não existir uma
            // categoria com esse nome (idempotente).
            var id = Guid.NewGuid();
            migrationBuilder.Sql($@"
                INSERT INTO categorias (id, nome, tipo, grupo_id, ordem, ativa, criado_em)
                SELECT '{id}', 'Rendimentos', 'Investimento', g.id, 0, true, NOW()
                FROM grupos g
                WHERE g.nome = 'Rendimento de Investimentos'
                AND NOT EXISTS (SELECT 1 FROM categorias WHERE nome = 'Rendimentos');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM categorias
                WHERE nome = 'Rendimentos'
                AND grupo_id = (SELECT id FROM grupos WHERE nome = 'Rendimento de Investimentos');
            ");
        }
    }
}
