using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirEntradasJsonInvalido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Registros com entrada vazia/inválida (defaultValue:"" da migration anterior virou string JSON)
            migrationBuilder.Sql(@"
                UPDATE registros_diarios
                SET entradas = '[]'::jsonb
                WHERE jsonb_typeof(entradas) <> 'array';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
