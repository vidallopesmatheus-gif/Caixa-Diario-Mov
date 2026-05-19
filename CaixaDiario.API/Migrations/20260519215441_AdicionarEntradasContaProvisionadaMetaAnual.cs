using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarEntradasContaProvisionadaMetaAnual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "entradas",
                table: "registros_diarios",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE registros_diarios
                SET entradas = CASE
                    WHEN entrada > 0
                    THEN json_build_array(json_build_object('Descricao', 'Entrada', 'Valor', entrada))::jsonb
                    ELSE '[]'::jsonb
                END
                WHERE entradas IS NULL OR entradas = 'null'::jsonb;
            ");

            migrationBuilder.DropColumn(
                name: "entrada",
                table: "registros_diarios");

            migrationBuilder.CreateTable(
                name: "metas_anuais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    meta_receita = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    meta_lucro = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metas_anuais", x => x.id);
                    table.ForeignKey(
                        name: "FK_metas_anuais_usuarios_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_metas_anuais_cliente_id_ano",
                table: "metas_anuais",
                columns: new[] { "cliente_id", "ano" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "entradas",
                table: "registros_diarios");

            migrationBuilder.AddColumn<decimal>(
                name: "entrada",
                table: "registros_diarios",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
