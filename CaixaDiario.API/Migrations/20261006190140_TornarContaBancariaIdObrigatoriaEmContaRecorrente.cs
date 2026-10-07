using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class TornarContaBancariaIdObrigatoriaEmContaRecorrente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill (Fase 0.2): atribui a conta padrão do cliente — Caixa ativa, senão qualquer
            // ativa, senão qualquer uma — a toda conta_recorrente que ainda está sem conta
            // vinculada. Precisa rodar ANTES do ALTER COLUMN NOT NULL abaixo, senão a própria
            // migration falha pra qualquer cliente com recorrência legada sem conta.
            migrationBuilder.Sql(@"
                UPDATE contas_recorrentes cr
                SET conta_bancaria_id = (
                    SELECT cb.id
                    FROM contas_bancarias cb
                    WHERE cb.cliente_id = cr.cliente_id
                    ORDER BY (cb.tipo = 'Caixa' AND cb.ativa) DESC, cb.ativa DESC, cb.data_criacao ASC
                    LIMIT 1
                )
                WHERE cr.conta_bancaria_id IS NULL;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_contas_recorrentes_contas_bancarias_conta_bancaria_id",
                table: "contas_recorrentes");

            migrationBuilder.AlterColumn<Guid>(
                name: "conta_bancaria_id",
                table: "contas_recorrentes",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_contas_recorrentes_contas_bancarias_conta_bancaria_id",
                table: "contas_recorrentes",
                column: "conta_bancaria_id",
                principalTable: "contas_bancarias",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contas_recorrentes_contas_bancarias_conta_bancaria_id",
                table: "contas_recorrentes");

            migrationBuilder.AlterColumn<Guid>(
                name: "conta_bancaria_id",
                table: "contas_recorrentes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_contas_recorrentes_contas_bancarias_conta_bancaria_id",
                table: "contas_recorrentes",
                column: "conta_bancaria_id",
                principalTable: "contas_bancarias",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
