using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarOcorrenciasRecorrentesDispensadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ocorrencias_recorrentes_dispensadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorrencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ocorrencias_recorrentes_dispensadas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ocorrencias_recorrentes_dispensadas_recorrencia_id_data_ven~",
                table: "ocorrencias_recorrentes_dispensadas",
                columns: new[] { "recorrencia_id", "data_vencimento" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ocorrencias_recorrentes_dispensadas");
        }
    }
}
