using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCartaoDeCredito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "dia_fechamento",
                table: "contas_bancarias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "dia_vencimento",
                table: "contas_bancarias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "limite",
                table: "contas_bancarias",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pagamentos_fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_cartao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competencia = table.Column<string>(type: "text", nullable: false),
                    valor_pago = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamentos_fatura", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagamentos_fatura_contas_bancarias_conta_cartao_id",
                        column: x => x.conta_cartao_id,
                        principalTable: "contas_bancarias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pagamentos_fatura_contas_bancarias_conta_origem_id",
                        column: x => x.conta_origem_id,
                        principalTable: "contas_bancarias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pagamentos_fatura_usuarios_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pagamentos_fatura_cliente_id",
                table: "pagamentos_fatura",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_pagamentos_fatura_conta_cartao_id_competencia",
                table: "pagamentos_fatura",
                columns: new[] { "conta_cartao_id", "competencia" });

            migrationBuilder.CreateIndex(
                name: "IX_pagamentos_fatura_conta_origem_id",
                table: "pagamentos_fatura",
                column: "conta_origem_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagamentos_fatura");

            migrationBuilder.DropColumn(
                name: "dia_fechamento",
                table: "contas_bancarias");

            migrationBuilder.DropColumn(
                name: "dia_vencimento",
                table: "contas_bancarias");

            migrationBuilder.DropColumn(
                name: "limite",
                table: "contas_bancarias");
        }
    }
}
