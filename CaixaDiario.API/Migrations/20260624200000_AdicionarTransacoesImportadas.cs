using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTransacoesImportadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transacoes_importadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    fit_id = table.Column<string>(type: "text", nullable: true),
                    tipo = table.Column<string>(type: "text", nullable: false, defaultValue: "Entrada"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pendente"),
                    categoria = table.Column<string>(type: "text", nullable: true),
                    importado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transacoes_importadas", x => x.id);
                    table.ForeignKey(
                        name: "FK_transacoes_importadas_contas_bancarias_conta_bancaria_id",
                        column: x => x.conta_bancaria_id,
                        principalTable: "contas_bancarias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transacoes_importadas_conta_bancaria_id_status",
                table: "transacoes_importadas",
                columns: new[] { "conta_bancaria_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_transacoes_importadas_conta_bancaria_id_fit_id",
                table: "transacoes_importadas",
                columns: new[] { "conta_bancaria_id", "fit_id" },
                filter: "fit_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "transacoes_importadas");
        }
    }
}
