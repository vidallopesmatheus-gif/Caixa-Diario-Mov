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
            new { nome = "Aluguel", tipoCusto = "CustoFixo" },
            new { nome = "Salários/Folha", tipoCusto = "CustoFixo" },
            new { nome = "Energia/Água/Internet", tipoCusto = "CustoFixo" },
            new { nome = "Manutenção", tipoCusto = "CustoFixo" },
            new { nome = "Seguros", tipoCusto = "CustoFixo" },
            new { nome = "Insumos/Mercadoria", tipoCusto = "CustoVariavel" },
            new { nome = "Embalagens", tipoCusto = "CustoVariavel" },
            new { nome = "Comissões", tipoCusto = "CustoVariavel" },
            new { nome = "Marketing", tipoCusto = "CustoVariavel" },
            new { nome = "Outros", tipoCusto = "CustoVariavel" },
        },
    };

    [HttpGet]
    public IActionResult Listar() => Ok(_categorias);
}
