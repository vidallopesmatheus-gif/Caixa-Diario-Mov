using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDuplicatasDispensadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "duplicatas_dispensadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lancamento_menor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lancamento_maior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_duplicatas_dispensadas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_duplicatas_dispensadas_lancamento_menor_id_lancamento_maior~",
                table: "duplicatas_dispensadas",
                columns: new[] { "lancamento_menor_id", "lancamento_maior_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "duplicatas_dispensadas");
        }
    }
}
