using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarRegrasCategorizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regras_categorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    criterio_tipo = table.Column<string>(type: "text", nullable: false),
                    criterio_valor = table.Column<string>(type: "text", nullable: false),
                    descricao_referencia = table.Column<string>(type: "text", nullable: false),
                    acao_tipo = table.Column<string>(type: "text", nullable: false),
                    categoria = table.Column<string>(type: "text", nullable: true),
                    conta_contrapartida_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regras_categorizacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_regras_categorizacao_contas_bancarias_conta_bancaria_id",
                        column: x => x.conta_bancaria_id,
                        principalTable: "contas_bancarias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_regras_categorizacao_contas_bancarias_conta_contrapartida_id",
                        column: x => x.conta_contrapartida_id,
                        principalTable: "contas_bancarias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_regras_categorizacao_usuarios_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_regras_categorizacao_cliente_id_conta_bancaria_id_tipo_ativa",
                table: "regras_categorizacao",
                columns: new[] { "cliente_id", "conta_bancaria_id", "tipo", "ativa" });

            migrationBuilder.CreateIndex(
                name: "IX_regras_categorizacao_conta_bancaria_id",
                table: "regras_categorizacao",
                column: "conta_bancaria_id");

            migrationBuilder.CreateIndex(
                name: "IX_regras_categorizacao_conta_contrapartida_id",
                table: "regras_categorizacao",
                column: "conta_contrapartida_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regras_categorizacao");
        }
    }
}
