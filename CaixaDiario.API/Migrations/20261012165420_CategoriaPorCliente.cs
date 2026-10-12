using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class CategoriaPorCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_categorias_nome",
                table: "categorias");

            migrationBuilder.AddColumn<Guid>(
                name: "cliente_id",
                table: "categorias",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "criado_por",
                table: "categorias",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origem",
                table: "categorias",
                type: "text",
                nullable: false,
                defaultValue: "Padrao");

            migrationBuilder.CreateIndex(
                name: "IX_categorias_cliente_id_nome",
                table: "categorias",
                columns: new[] { "cliente_id", "nome" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_categorias_cliente_id_nome",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "cliente_id",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "criado_por",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "origem",
                table: "categorias");

            migrationBuilder.CreateIndex(
                name: "IX_categorias_nome",
                table: "categorias",
                column: "nome",
                unique: true);
        }
    }
}
