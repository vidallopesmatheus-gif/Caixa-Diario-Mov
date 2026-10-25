using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarParcelamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parcelamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_cartao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao_base = table.Column<string>(type: "text", nullable: false),
                    categoria = table.Column<string>(type: "text", nullable: true),
                    valor_parcela = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_parcelas = table.Column<int>(type: "integer", nullable: false),
                    data_primeira = table.Column<DateOnly>(type: "date", nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    meta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Ativo"),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcelamentos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parcelamentos_cliente_id_conta_cartao_id_status",
                table: "parcelamentos",
                columns: new[] { "cliente_id", "conta_cartao_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parcelamentos");
        }
    }
}
