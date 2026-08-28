using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTransferenciasEVinculoMeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "conta_investimento_id",
                table: "metas_anuais",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "transferencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferencias", x => x.id);
                    table.ForeignKey(
                        name: "FK_transferencias_contas_bancarias_conta_destino_id",
                        column: x => x.conta_destino_id,
                        principalTable: "contas_bancarias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transferencias_contas_bancarias_conta_origem_id",
                        column: x => x.conta_origem_id,
                        principalTable: "contas_bancarias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transferencias_usuarios_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_metas_anuais_conta_investimento_id",
                table: "metas_anuais",
                column: "conta_investimento_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_cliente_id_data",
                table: "transferencias",
                columns: new[] { "cliente_id", "data" });

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_conta_destino_id",
                table: "transferencias",
                column: "conta_destino_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_conta_origem_id",
                table: "transferencias",
                column: "conta_origem_id");

            migrationBuilder.AddForeignKey(
                name: "FK_metas_anuais_contas_bancarias_conta_investimento_id",
                table: "metas_anuais",
                column: "conta_investimento_id",
                principalTable: "contas_bancarias",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_metas_anuais_contas_bancarias_conta_investimento_id",
                table: "metas_anuais");

            migrationBuilder.DropTable(
                name: "transferencias");

            migrationBuilder.DropIndex(
                name: "IX_metas_anuais_conta_investimento_id",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "conta_investimento_id",
                table: "metas_anuais");
        }
    }
}
