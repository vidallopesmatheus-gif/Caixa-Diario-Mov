using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarConciliacaoSaldo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "periodos_extrato",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    de = table.Column<DateOnly>(type: "date", nullable: false),
                    ate = table.Column<DateOnly>(type: "date", nullable: false),
                    importado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodos_extrato", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pontos_conciliacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    saldo_banco = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    saldo_app = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    origem = table.Column<string>(type: "text", nullable: false, defaultValue: "Informado"),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pontos_conciliacao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_periodos_extrato_conta_bancaria_id",
                table: "periodos_extrato",
                column: "conta_bancaria_id");

            migrationBuilder.CreateIndex(
                name: "IX_pontos_conciliacao_cliente_id",
                table: "pontos_conciliacao",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_pontos_conciliacao_conta_bancaria_id_data",
                table: "pontos_conciliacao",
                columns: new[] { "conta_bancaria_id", "data" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "periodos_extrato");

            migrationBuilder.DropTable(
                name: "pontos_conciliacao");
        }
    }
}
