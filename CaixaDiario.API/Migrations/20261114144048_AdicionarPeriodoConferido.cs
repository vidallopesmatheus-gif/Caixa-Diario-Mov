using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPeriodoConferido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "trava_saldo_banco",
                table: "contas_bancarias",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "travado_ate",
                table: "contas_bancarias",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "travado_por_consultor",
                table: "contas_bancarias",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "conferencias_saldo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: true),
                    saldo_banco = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    travado_ate_antes = table.Column<DateOnly>(type: "date", nullable: true),
                    travado_ate_depois = table.Column<DateOnly>(type: "date", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil = table.Column<string>(type: "text", nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: true),
                    retrato = table.Column<string>(type: "jsonb", nullable: true),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conferencias_saldo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conferencias_saldo_conta_bancaria_id_ocorrido_em",
                table: "conferencias_saldo",
                columns: new[] { "conta_bancaria_id", "ocorrido_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conferencias_saldo");

            migrationBuilder.DropColumn(
                name: "trava_saldo_banco",
                table: "contas_bancarias");

            migrationBuilder.DropColumn(
                name: "travado_ate",
                table: "contas_bancarias");

            migrationBuilder.DropColumn(
                name: "travado_por_consultor",
                table: "contas_bancarias");
        }
    }
}
