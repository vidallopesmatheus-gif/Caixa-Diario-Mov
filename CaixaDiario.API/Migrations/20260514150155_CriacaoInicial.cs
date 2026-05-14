using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class CriacaoInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_usuario = table.Column<string>(type: "text", nullable: false),
                    senha_hash = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    loja = table.Column<string>(type: "text", nullable: true),
                    perfil = table.Column<string>(type: "text", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_atualizacao = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "registros_diarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    inicio = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    entrada = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    saidas = table.Column<string>(type: "jsonb", nullable: false),
                    contas_receber = table.Column<string>(type: "jsonb", nullable: false),
                    contas_pagar = table.Column<string>(type: "jsonb", nullable: false),
                    saldo_final = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    excluido = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    motivo_exclusao = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    salvo_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_atualizacao = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_diarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_registros_diarios_usuarios_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_registros_diarios_cliente_id_data",
                table: "registros_diarios",
                columns: new[] { "cliente_id", "data" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_nome_usuario",
                table: "usuarios",
                column: "nome_usuario",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registros_diarios");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
