using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarHierarquiaBlocoGrupo : Migration
    {
        // Blocos fixos do demonstrativo (ver CaixaDiario.API.Services.Blocos) — replicados aqui como
        // texto puro porque migrations não podem referenciar código da aplicação (que pode mudar
        // depois; a migration precisa continuar reproduzível do jeito que rodou da primeira vez).
        private const string ReceitasOperacionais = "RECEITAS OPERACIONAIS";
        private const string DeducoesDaReceita = "DEDUÇÕES DA RECEITA";
        private const string CustosOperacionais = "CUSTOS OPERACIONAIS";
        private const string DespesasOperacionais = "DESPESAS OPERACIONAIS";
        private const string AtividadesDeInvestimento = "ATIVIDADES DE INVESTIMENTO";
        private const string AtividadesDeFinanciamento = "ATIVIDADES DE FINANCIAMENTO";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Tabela grupos (nível novo entre Bloco fixo e Categoria) ──────────────────────
            migrationBuilder.CreateTable(
                name: "grupos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    bloco = table.Column<string>(type: "text", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grupos", x => x.id);
                });

            migrationBuilder.CreateIndex(name: "IX_grupos_bloco_ordem", table: "grupos", columns: new[] { "bloco", "ordem" });
            migrationBuilder.CreateIndex(name: "IX_grupos_nome", table: "grupos", column: "nome", unique: true);

            // ── 2. Seed dos grupos — mapeamento aprovado na conversa (Bloco → Grupo) ────────────
            var agora = DateTime.UtcNow;
            var gVendasServicos = Guid.NewGuid();
            var gPagamentoImpostos = Guid.NewGuid();
            var gDevolucaoEstorno = Guid.NewGuid();
            var gCustosDiretos = Guid.NewGuid();
            var gOcupacao = Guid.NewGuid();
            var gPessoal = Guid.NewGuid();
            var gProLabore = Guid.NewGuid();
            var gGeraisAdministrativas = Guid.NewGuid();
            var gManutencao = Guid.NewGuid();
            var gFinanceiras = Guid.NewGuid();
            var gMarketing = Guid.NewGuid();
            var gImobilizado = Guid.NewGuid();
            var gRendimentoInvestimentos = Guid.NewGuid();
            var gEmprestimosFinanciamentos = Guid.NewGuid();
            var gConsorcios = Guid.NewGuid();

            var grupos = new (Guid Id, string Nome, string Bloco, int Ordem)[]
            {
                (gVendasServicos, "Vendas e Serviços", ReceitasOperacionais, 0),
                (gPagamentoImpostos, "Pagamento de Impostos", DeducoesDaReceita, 0),
                (gDevolucaoEstorno, "Devolução e Estorno", DeducoesDaReceita, 1),
                (gCustosDiretos, "Custos Diretos", CustosOperacionais, 0),
                (gOcupacao, "Despesas com Ocupação", DespesasOperacionais, 0),
                (gPessoal, "Despesas com Pessoal", DespesasOperacionais, 1),
                (gProLabore, "Pró-Labore", DespesasOperacionais, 2),
                (gGeraisAdministrativas, "Despesas Gerais/Administrativas", DespesasOperacionais, 3),
                (gManutencao, "Despesas com Manutenção", DespesasOperacionais, 4),
                (gFinanceiras, "Despesas Financeiras", DespesasOperacionais, 5),
                (gMarketing, "Despesas com Marketing", DespesasOperacionais, 6),
                (gImobilizado, "Imobilizado", AtividadesDeInvestimento, 0),
                (gRendimentoInvestimentos, "Rendimento de Investimentos", AtividadesDeInvestimento, 1),
                (gEmprestimosFinanciamentos, "Empréstimos/Financiamentos", AtividadesDeFinanciamento, 0),
                (gConsorcios, "Consórcios", AtividadesDeFinanciamento, 1),
            };
            foreach (var g in grupos)
            {
                migrationBuilder.InsertData(
                    table: "grupos",
                    columns: new[] { "id", "nome", "bloco", "ordem", "ativo", "criado_em" },
                    values: new object[] { g.Id, g.Nome, g.Bloco, g.Ordem, true, agora });
            }

            // ── 3. categorias.grupo_id (nullable por enquanto — backfill vem antes do NOT NULL) ──
            migrationBuilder.AddColumn<Guid>(
                name: "grupo_id",
                table: "categorias",
                type: "uuid",
                nullable: true);

            // ── 4. Backfill: mapeamento por NOME de categoria, aprovado na conversa ─────────────
            // Cobre as 33 categorias cadastradas em produção no momento desta migration (27 do seed
            // original + 6 criadas manualmente sem grupo). Categoria que não bater com nenhum nome
            // aqui fica com grupo_id NULL e é pega pela checagem de segurança no passo 5 — a
            // migration falha alto a raspar em vez de deixar qualquer categoria órfã silenciosamente.
            var mapeamento = new (string Nome, Guid GrupoId)[]
            {
                ("Vendas", gVendasServicos),
                ("Serviços Prestados", gVendasServicos),
                ("Outras Receitas", gVendasServicos),
                ("Simples/DAS", gPagamentoImpostos),
                ("ISS", gPagamentoImpostos),
                ("Outros tributos", gPagamentoImpostos),
                ("Insumos/Mercadoria", gCustosDiretos),
                ("Embalagens", gCustosDiretos),
                ("Comissões", gCustosDiretos),
                ("Frete e entrega", gCustosDiretos),
                ("Aluguel", gOcupacao),
                ("Energia/Água/Internet", gOcupacao),
                ("Telefone e Internet", gOcupacao),
                ("Salários/Folha", gPessoal),
                ("Encargos", gPessoal),
                ("Benefícios", gPessoal),
                ("Refeição", gPessoal),
                ("Pró-labore", gProLabore),
                ("Seguros", gGeraisAdministrativas),
                ("Material de Escritório", gGeraisAdministrativas),
                ("Psicologa", gGeraisAdministrativas),
                ("Cabelereiro", gGeraisAdministrativas),
                ("Manutenção", gManutencao),
                ("Tarifas bancárias", gFinanceiras),
                ("Juros", gFinanceiras),
                ("IOF", gFinanceiras),
                ("Cartão de crédito", gFinanceiras),
                ("Publicidade", gMarketing),
                ("Mídia paga", gMarketing),
                ("Material gráfico", gMarketing),
                ("Equipamentos", gImobilizado),
                ("Reformas", gImobilizado),
                ("Software", gImobilizado),
            };
            foreach (var m in mapeamento)
            {
                migrationBuilder.Sql(
                    $"UPDATE categorias SET grupo_id = '{m.GrupoId}' WHERE nome = '{m.Nome.Replace("'", "''")}';");
            }

            // Reclassificação combinada com o novo grupo: Marketing e Imobilizado mudam de Tipo
            // junto (ver Blocos.TipoPadrao) — deixavam de reduzir Margem de Contribuição/Resultado
            // Operacional como CustoVariavel/CustoFixo comuns, passam a valer pelo Bloco do grupo.
            migrationBuilder.Sql($"UPDATE categorias SET tipo = 'CustoFixo' WHERE grupo_id = '{gMarketing}';");
            migrationBuilder.Sql($"UPDATE categorias SET tipo = 'Investimento' WHERE grupo_id = '{gImobilizado}';");

            // ── 5. Trava de segurança: nenhuma categoria pode ficar sem grupo ───────────────────
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    orfas INTEGER;
                BEGIN
                    SELECT COUNT(*) INTO orfas FROM categorias WHERE grupo_id IS NULL;
                    IF orfas > 0 THEN
                        RAISE EXCEPTION 'Migration AdicionarHierarquiaBlocoGrupo: % categoria(s) sem grupo_id após o backfill — mapeamento incompleto, corrija antes de continuar.', orfas;
                    END IF;
                END $$;
            ");

            // ── 6. Fecha o esquema: grupo_id vira obrigatório, remove a coluna texto antiga ─────
            migrationBuilder.AlterColumn<Guid>(
                name: "grupo_id",
                table: "categorias",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(name: "grupo", table: "categorias");

            migrationBuilder.CreateIndex(name: "IX_categorias_grupo_id", table: "categorias", column: "grupo_id");

            migrationBuilder.AddForeignKey(
                name: "FK_categorias_grupos_grupo_id",
                table: "categorias",
                column: "grupo_id",
                principalTable: "grupos",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_categorias_grupos_grupo_id",
                table: "categorias");

            migrationBuilder.DropIndex(name: "IX_categorias_grupo_id", table: "categorias");

            migrationBuilder.AddColumn<string>(
                name: "grupo",
                table: "categorias",
                type: "text",
                nullable: true);

            // Restaura o texto de Grupo a partir do cadastro (perde só a distinção fina de Bloco,
            // que não existia antes desta migration).
            migrationBuilder.Sql(@"
                UPDATE categorias c
                SET grupo = g.nome
                FROM grupos g
                WHERE c.grupo_id = g.id;
            ");

            migrationBuilder.DropColumn(name: "grupo_id", table: "categorias");

            migrationBuilder.DropTable(name: "grupos");
        }
    }
}
