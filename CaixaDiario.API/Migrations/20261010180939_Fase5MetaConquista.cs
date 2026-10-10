using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class Fase5MetaConquista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "categoria",
                table: "metas_anuais",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "conta_pagamento_id",
                table: "metas_anuais",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_conquista",
                table: "metas_anuais",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_primeira_parcela",
                table: "metas_anuais",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "financiamento_id",
                table: "metas_anuais",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "forma_pagamento",
                table: "metas_anuais",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacao",
                table: "metas_anuais",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "parcelas",
                table: "metas_anuais",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "metas_anuais",
                type: "text",
                nullable: false,
                defaultValue: "EmAndamento");

            migrationBuilder.AddColumn<Guid>(
                name: "transferencia_resgate_id",
                table: "metas_anuais",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_pago",
                table: "metas_anuais",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_metas_anuais_conta_pagamento_id",
                table: "metas_anuais",
                column: "conta_pagamento_id");

            migrationBuilder.AddForeignKey(
                name: "FK_metas_anuais_contas_bancarias_conta_pagamento_id",
                table: "metas_anuais",
                column: "conta_pagamento_id",
                principalTable: "contas_bancarias",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_metas_anuais_contas_bancarias_conta_pagamento_id",
                table: "metas_anuais");

            migrationBuilder.DropIndex(
                name: "IX_metas_anuais_conta_pagamento_id",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "categoria",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "conta_pagamento_id",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "data_conquista",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "data_primeira_parcela",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "financiamento_id",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "forma_pagamento",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "observacao",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "parcelas",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "status",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "transferencia_resgate_id",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "valor_pago",
                table: "metas_anuais");
        }
    }
}
