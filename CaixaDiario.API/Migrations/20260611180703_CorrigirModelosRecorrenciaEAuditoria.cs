using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirModelosRecorrenciaEAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_usuarios_UsuarioId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_contas_recorrentes_usuarios_UsuarioId",
                table: "contas_recorrentes");

            migrationBuilder.DropIndex(
                name: "IX_contas_recorrentes_UsuarioId",
                table: "contas_recorrentes");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_UsuarioId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "Ativa",
                table: "contas_recorrentes");

            migrationBuilder.DropColumn(
                name: "DiaVencimento",
                table: "contas_recorrentes");

            migrationBuilder.DropColumn(
                name: "Acao",
                table: "audit_logs");

            migrationBuilder.RenameColumn(
                name: "Valor",
                table: "contas_recorrentes",
                newName: "valor");

            migrationBuilder.RenameColumn(
                name: "Descricao",
                table: "contas_recorrentes",
                newName: "descricao");

            migrationBuilder.RenameColumn(
                name: "Categoria",
                table: "contas_recorrentes",
                newName: "categoria");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "contas_recorrentes",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UsuarioId",
                table: "contas_recorrentes",
                newName: "cliente_id");

            migrationBuilder.RenameColumn(
                name: "CriadaEm",
                table: "contas_recorrentes",
                newName: "criado_em");

            migrationBuilder.RenameColumn(
                name: "Entidade",
                table: "audit_logs",
                newName: "entidade");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "audit_logs",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UsuarioId",
                table: "audit_logs",
                newName: "usuario_id");

            migrationBuilder.RenameColumn(
                name: "EntidadeId",
                table: "audit_logs",
                newName: "entidade_id");

            migrationBuilder.RenameColumn(
                name: "Detalhes",
                table: "audit_logs",
                newName: "dados_depois");

            migrationBuilder.RenameColumn(
                name: "CriadoEm",
                table: "audit_logs",
                newName: "ocorrido_em");

            migrationBuilder.AlterColumn<string>(
                name: "descricao",
                table: "contas_recorrentes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "categoria",
                table: "contas_recorrentes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<bool>(
                name: "ativo",
                table: "contas_recorrentes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "atualizado_em",
                table: "contas_recorrentes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_fim",
                table: "contas_recorrentes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_inicio",
                table: "contas_recorrentes",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "tipo",
                table: "contas_recorrentes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "entidade",
                table: "audit_logs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "entidade_id",
                table: "audit_logs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "acao_tipo",
                table: "audit_logs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "cliente_id",
                table: "audit_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "dados_antes",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_recorrentes_cliente_id_ativo",
                table: "contas_recorrentes",
                columns: new[] { "cliente_id", "ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_cliente_id_ocorrido_em",
                table: "audit_logs",
                columns: new[] { "cliente_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entidade_acao_tipo",
                table: "audit_logs",
                columns: new[] { "entidade", "acao_tipo" });

            migrationBuilder.AddForeignKey(
                name: "FK_contas_recorrentes_usuarios_cliente_id",
                table: "contas_recorrentes",
                column: "cliente_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contas_recorrentes_usuarios_cliente_id",
                table: "contas_recorrentes");

            migrationBuilder.DropIndex(
                name: "IX_contas_recorrentes_cliente_id_ativo",
                table: "contas_recorrentes");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_cliente_id_ocorrido_em",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_entidade_acao_tipo",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ativo",
                table: "contas_recorrentes");

            migrationBuilder.DropColumn(
                name: "atualizado_em",
                table: "contas_recorrentes");

            migrationBuilder.DropColumn(
                name: "data_fim",
                table: "contas_recorrentes");

            migrationBuilder.DropColumn(
                name: "data_inicio",
                table: "contas_recorrentes");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "contas_recorrentes");

            migrationBuilder.DropColumn(
                name: "acao_tipo",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "cliente_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "dados_antes",
                table: "audit_logs");

            migrationBuilder.RenameColumn(
                name: "valor",
                table: "contas_recorrentes",
                newName: "Valor");

            migrationBuilder.RenameColumn(
                name: "descricao",
                table: "contas_recorrentes",
                newName: "Descricao");

            migrationBuilder.RenameColumn(
                name: "categoria",
                table: "contas_recorrentes",
                newName: "Categoria");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "contas_recorrentes",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "criado_em",
                table: "contas_recorrentes",
                newName: "CriadaEm");

            migrationBuilder.RenameColumn(
                name: "cliente_id",
                table: "contas_recorrentes",
                newName: "UsuarioId");

            migrationBuilder.RenameColumn(
                name: "entidade",
                table: "audit_logs",
                newName: "Entidade");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "audit_logs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "usuario_id",
                table: "audit_logs",
                newName: "UsuarioId");

            migrationBuilder.RenameColumn(
                name: "entidade_id",
                table: "audit_logs",
                newName: "EntidadeId");

            migrationBuilder.RenameColumn(
                name: "ocorrido_em",
                table: "audit_logs",
                newName: "CriadoEm");

            migrationBuilder.RenameColumn(
                name: "dados_depois",
                table: "audit_logs",
                newName: "Detalhes");

            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "contas_recorrentes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Categoria",
                table: "contas_recorrentes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Ativa",
                table: "contas_recorrentes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DiaVencimento",
                table: "contas_recorrentes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Entidade",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EntidadeId",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Acao",
                table: "audit_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_contas_recorrentes_UsuarioId",
                table: "contas_recorrentes",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UsuarioId",
                table: "audit_logs",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_usuarios_UsuarioId",
                table: "audit_logs",
                column: "UsuarioId",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_contas_recorrentes_usuarios_UsuarioId",
                table: "contas_recorrentes",
                column: "UsuarioId",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
