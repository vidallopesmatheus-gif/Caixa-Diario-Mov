using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCategorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    grupo = table.Column<string>(type: "text", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ativa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_ativa_ordem",
                table: "categorias",
                columns: new[] { "ativa", "ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_nome",
                table: "categorias",
                column: "nome",
                unique: true);

            // Semeia com a lista fixa que existia hoje em CategoriasController, preservando
            // nome/tipo/grupo/ordem exatamente como já apareciam (nenhuma mudança visual no DRE).
            var agora = DateTime.UtcNow;
            var seed = new (string Nome, string Tipo, string Grupo)[]
            {
                ("Vendas", "Receita", null),
                ("Serviços Prestados", "Receita", null),
                ("Outras Receitas", "Receita", null),

                ("Insumos/Mercadoria", "CustoVariavel", "Custos Diretos"),
                ("Embalagens", "CustoVariavel", "Custos Diretos"),
                ("Comissões", "CustoVariavel", "Custos Diretos"),

                ("Salários/Folha", "CustoFixo", "Pessoas"),
                ("Encargos", "CustoFixo", "Pessoas"),
                ("Benefícios", "CustoFixo", "Pessoas"),
                ("Pró-labore", "CustoFixo", "Pessoas"),

                ("Aluguel", "CustoFixo", "Despesas Administrativas"),
                ("Energia/Água/Internet", "CustoFixo", "Despesas Administrativas"),
                ("Seguros", "CustoFixo", "Despesas Administrativas"),
                ("Manutenção", "CustoFixo", "Despesas Administrativas"),
                ("Material de Escritório", "CustoFixo", "Despesas Administrativas"),

                ("Publicidade", "CustoVariavel", "Marketing"),
                ("Mídia paga", "CustoVariavel", "Marketing"),
                ("Material gráfico", "CustoVariavel", "Marketing"),

                ("Simples/DAS", "CustoFixo", "Impostos"),
                ("ISS", "CustoFixo", "Impostos"),
                ("Outros tributos", "CustoFixo", "Impostos"),

                ("Tarifas bancárias", "CustoFixo", "Financeiras"),
                ("Juros", "CustoFixo", "Financeiras"),
                ("IOF", "CustoFixo", "Financeiras"),

                ("Equipamentos", "CustoFixo", "Investimentos"),
                ("Reformas", "CustoFixo", "Investimentos"),
                ("Software", "CustoFixo", "Investimentos"),
            };

            for (var i = 0; i < seed.Length; i++)
            {
                migrationBuilder.InsertData(
                    table: "categorias",
                    columns: new[] { "id", "nome", "tipo", "grupo", "ordem", "ativa", "criado_em" },
                    values: new object[] { Guid.NewGuid(), seed[i].Nome, seed[i].Tipo, (object)seed[i].Grupo ?? DBNull.Value, i, true, agora });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categorias");
        }
    }
}
