using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

// Money Vision (segurança): o token do cliente teste é perfil "cliente" — nenhuma rota de admin
// pode responder pra ele.
public class AdminMigracaoControllerTests
{
    private readonly Mock<IRecalculoSaldoService> _recalculo = new();

    private AdminMigracaoController Criar(string perfil) => new(
        new Mock<IMigracaoRecorrenciasService>().Object, new Mock<IReclassificacaoCategoriasService>().Object,
        new Mock<ILimpezaTransferenciasService>().Object, _recalculo.Object, new Mock<IVerificacaoSinalTransferenciaService>().Object,
        new Mock<ICorrigirDataParcelasService>().Object, new Mock<ICorrigirFaturasService>().Object, new Mock<IRevisaoTransferenciasService>().Object)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", Guid.NewGuid().ToString()), new Claim("perfil", perfil) }, "Bearer")),
            },
        },
    };

    [Fact]
    public async Task RecalcularSaldos_TokenDeCliente_Lanca403SemTocarNoServico()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => Criar("cliente").RecalcularSaldos(confirmar: true));

        Assert.Equal(403, ex.StatusCode);
        _recalculo.Verify(s => s.RecalcularTodasAsContasAsync(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task RecalcularSaldos_TokenDeAdmin_Executa()
    {
        _recalculo.Setup(s => s.RecalcularTodasAsContasAsync(false)).ReturnsAsync(new CaixaDiario.API.DTOs.Admin.RecalculoSaldoResultDto());

        var resultado = await Criar("admin").RecalcularSaldos(confirmar: false);

        Assert.IsType<OkObjectResult>(resultado);
    }
}
