using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaixaDiario.API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMetaSonhoExtensao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MargemPJ",
                table: "metas_anuais",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconeSonho",
                table: "metas_anuais",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MargemPJ",
                table: "metas_anuais");

            migrationBuilder.DropColumn(
                name: "IconeSonho",
                table: "metas_anuais");
        }
    }
}
