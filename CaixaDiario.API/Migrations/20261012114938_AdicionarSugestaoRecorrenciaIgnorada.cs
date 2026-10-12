using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSugestaoRecorrenciaIgnorada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sugestoes_recorrencia_ignoradas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_normalizada = table.Column<string>(type: "text", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sugestoes_recorrencia_ignoradas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sugestoes_recorrencia_ignoradas_cliente_id_chave_normalizada",
                table: "sugestoes_recorrencia_ignoradas",
                columns: new[] { "cliente_id", "chave_normalizada" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sugestoes_recorrencia_ignoradas");
        }
    }
}
