using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarContasBancarias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_registros_diarios_cliente_id_data",
                table: "registros_diarios");

            migrationBuilder.AddColumn<Guid>(
                name: "conta_bancaria_id",
                table: "registros_diarios",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contas_bancarias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false, defaultValue: "Caixa"),
                    saldo_inicial = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    ativa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contas_bancarias", x => x.id);
                    table.ForeignKey(
                        name: "FK_contas_bancarias_usuarios_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_registros_diarios_cliente_id_data",
                table: "registros_diarios",
                columns: new[] { "cliente_id", "data" });

            migrationBuilder.CreateIndex(
                name: "IX_registros_diarios_conta_bancaria_id_data",
                table: "registros_diarios",
                columns: new[] { "conta_bancaria_id", "data" },
                unique: true,
                filter: "conta_bancaria_id IS NOT NULL AND excluido = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_contas_bancarias_cliente_id_ativa",
                table: "contas_bancarias",
                columns: new[] { "cliente_id", "ativa" });

            migrationBuilder.AddForeignKey(
                name: "FK_registros_diarios_contas_bancarias_conta_bancaria_id",
                table: "registros_diarios",
                column: "conta_bancaria_id",
                principalTable: "contas_bancarias",
                principalColumn: "id");

            // DATA MIGRATION: cria conta Caixa para cada cliente e associa registros históricos
            migrationBuilder.Sql(@"
                INSERT INTO contas_bancarias (id, cliente_id, nome, tipo, saldo_inicial, ativa, data_criacao)
                SELECT gen_random_uuid(), id, 'Caixa', 'Caixa', 0, TRUE, NOW()
                FROM usuarios
                WHERE perfil = 'cliente';
            ");

            migrationBuilder.Sql(@"
                UPDATE registros_diarios rd
                SET conta_bancaria_id = cb.id
                FROM contas_bancarias cb
                WHERE cb.cliente_id = rd.cliente_id
                  AND cb.tipo = 'Caixa';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_registros_diarios_contas_bancarias_conta_bancaria_id",
                table: "registros_diarios");

            migrationBuilder.DropTable(
                name: "contas_bancarias");

            migrationBuilder.DropIndex(
                name: "IX_registros_diarios_cliente_id_data",
                table: "registros_diarios");

            migrationBuilder.DropIndex(
                name: "IX_registros_diarios_conta_bancaria_id_data",
                table: "registros_diarios");

            migrationBuilder.DropColumn(
                name: "conta_bancaria_id",
                table: "registros_diarios");

            migrationBuilder.CreateIndex(
                name: "IX_registros_diarios_cliente_id_data",
                table: "registros_diarios",
                columns: new[] { "cliente_id", "data" },
                unique: true);
        }
    }
}
