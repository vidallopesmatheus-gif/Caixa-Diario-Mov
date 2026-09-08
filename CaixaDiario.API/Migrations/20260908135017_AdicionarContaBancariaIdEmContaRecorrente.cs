using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarContaBancariaIdEmContaRecorrente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "conta_bancaria_id",
                table: "contas_recorrentes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_recorrentes_conta_bancaria_id",
                table: "contas_recorrentes",
                column: "conta_bancaria_id");

            migrationBuilder.AddForeignKey(
                name: "FK_contas_recorrentes_contas_bancarias_conta_bancaria_id",
                table: "contas_recorrentes",
                column: "conta_bancaria_id",
                principalTable: "contas_bancarias",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contas_recorrentes_contas_bancarias_conta_bancaria_id",
                table: "contas_recorrentes");

            migrationBuilder.DropIndex(
                name: "IX_contas_recorrentes_conta_bancaria_id",
                table: "contas_recorrentes");

            migrationBuilder.DropColumn(
                name: "conta_bancaria_id",
                table: "contas_recorrentes");
        }
    }
}
