using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class Fase6Financiamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financiamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false, defaultValue: "Financiamento"),
                    valor_bem = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_entrada = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_financiado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    numero_parcelas = table.Column<int>(type: "integer", nullable: false),
                    valor_parcela = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    taxa_juros_mensal = table.Column<decimal>(type: "numeric(10,8)", nullable: true),
                    sistema = table.Column<string>(type: "text", nullable: false, defaultValue: "Price"),
                    data_primeira_parcela = table.Column<DateOnly>(type: "date", nullable: false),
                    conta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bem_vinculado = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Ativo"),
                    origem_meta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    valor_adiantado = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    economia_juros_acumulada = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financiamentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_financiamentos_contas_bancarias_conta_bancaria_id",
                        column: x => x.conta_bancaria_id,
                        principalTable: "contas_bancarias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financiamentos_usuarios_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financiamentos_cliente_id_status",
                table: "financiamentos",
                columns: new[] { "cliente_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_financiamentos_conta_bancaria_id",
                table: "financiamentos",
                column: "conta_bancaria_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financiamentos");
        }
    }
}
