using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/categorias")]
public class CategoriasController : ControllerBase
{
    private static readonly object _categorias = new
    {
        entradas = new[]
        {
            new { nome = "Vendas", tipoCusto = "Receita" },
            new { nome = "Serviços Prestados", tipoCusto = "Receita" },
            new { nome = "Outras Receitas", tipoCusto = "Receita" },
        },
        saidas = new[]
        {
            // Custos Diretos
            new { grupo = "Custos Diretos", nome = "Insumos/Mercadoria", tipoCusto = "CustoVariavel" },
            new { grupo = "Custos Diretos", nome = "Embalagens", tipoCusto = "CustoVariavel" },
            new { grupo = "Custos Diretos", nome = "Comissões", tipoCusto = "CustoVariavel" },
            // Pessoas
            new { grupo = "Pessoas", nome = "Salários/Folha", tipoCusto = "CustoFixo" },
            new { grupo = "Pessoas", nome = "Encargos", tipoCusto = "CustoFixo" },
            new { grupo = "Pessoas", nome = "Benefícios", tipoCusto = "CustoFixo" },
            new { grupo = "Pessoas", nome = "Pró-labore", tipoCusto = "CustoFixo" },
            // Despesas Administrativas
            new { grupo = "Despesas Administrativas", nome = "Aluguel", tipoCusto = "CustoFixo" },
            new { grupo = "Despesas Administrativas", nome = "Energia/Água/Internet", tipoCusto = "CustoFixo" },
            new { grupo = "Despesas Administrativas", nome = "Seguros", tipoCusto = "CustoFixo" },
            new { grupo = "Despesas Administrativas", nome = "Manutenção", tipoCusto = "CustoFixo" },
            new { grupo = "Despesas Administrativas", nome = "Material de Escritório", tipoCusto = "CustoFixo" },
            // Marketing
            new { grupo = "Marketing", nome = "Publicidade", tipoCusto = "CustoVariavel" },
            new { grupo = "Marketing", nome = "Mídia paga", tipoCusto = "CustoVariavel" },
            new { grupo = "Marketing", nome = "Material gráfico", tipoCusto = "CustoVariavel" },
            // Impostos
            new { grupo = "Impostos", nome = "Simples/DAS", tipoCusto = "CustoFixo" },
            new { grupo = "Impostos", nome = "ISS", tipoCusto = "CustoFixo" },
            new { grupo = "Impostos", nome = "Outros tributos", tipoCusto = "CustoFixo" },
            // Financeiras
            new { grupo = "Financeiras", nome = "Tarifas bancárias", tipoCusto = "CustoFixo" },
            new { grupo = "Financeiras", nome = "Juros", tipoCusto = "CustoFixo" },
            new { grupo = "Financeiras", nome = "IOF", tipoCusto = "CustoFixo" },
            // Investimentos
            new { grupo = "Investimentos", nome = "Equipamentos", tipoCusto = "CustoFixo" },
            new { grupo = "Investimentos", nome = "Reformas", tipoCusto = "CustoFixo" },
            new { grupo = "Investimentos", nome = "Software", tipoCusto = "CustoFixo" },
        },
    };

    [HttpGet]
    public IActionResult Listar() => Ok(_categorias);
}
