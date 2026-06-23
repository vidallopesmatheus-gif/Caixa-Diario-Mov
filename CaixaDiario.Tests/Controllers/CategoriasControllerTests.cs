using CaixaDiario.API.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.Tests.Controllers;

public class CategoriasControllerTests
{
    [Fact]
    public void Listar_RetornaOkComCategorias()
    {
        var sut = new CategoriasController();

        var result = sut.Listar();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }
}
