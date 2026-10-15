using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarFaturaCartao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "fatura_id",
                table: "pagamentos_fatura",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "faturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco = table.Column<string>(type: "text", nullable: false),
                    competencia = table.Column<string>(type: "text", nullable: false),
                    data_fechamento = table.Column<DateOnly>(type: "date", nullable: true),
                    data_vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faturas", x => x.id);
                    table.ForeignKey(
                        name: "FK_faturas_contas_bancarias_conta_bancaria_id",
                        column: x => x.conta_bancaria_id,
                        principalTable: "contas_bancarias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_faturas_usuarios_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pagamentos_fatura_fatura_id",
                table: "pagamentos_fatura",
                column: "fatura_id");

            migrationBuilder.CreateIndex(
                name: "IX_faturas_cliente_id",
                table: "faturas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_faturas_conta_bancaria_id_data_vencimento",
                table: "faturas",
                columns: new[] { "conta_bancaria_id", "data_vencimento" });

            migrationBuilder.AddForeignKey(
                name: "FK_pagamentos_fatura_faturas_fatura_id",
                table: "pagamentos_fatura",
                column: "fatura_id",
                principalTable: "faturas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pagamentos_fatura_faturas_fatura_id",
                table: "pagamentos_fatura");

            migrationBuilder.DropTable(
                name: "faturas");

            migrationBuilder.DropIndex(
                name: "IX_pagamentos_fatura_fatura_id",
                table: "pagamentos_fatura");

            migrationBuilder.DropColumn(
                name: "fatura_id",
                table: "pagamentos_fatura");
        }
    }
}
