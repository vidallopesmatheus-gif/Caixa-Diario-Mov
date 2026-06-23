using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Auditoria;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class AuditoriaControllerTests
{
    private readonly Mock<IAuditRepository> _repoMock = new();

    private AuditoriaController CriarSut(Guid usuarioId, string perfil)
    {
        var sut = new AuditoriaController(_repoMock.Object);
        var claims = new[] { new Claim("id", usuarioId.ToString()), new Claim("perfil", perfil) };
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return sut;
    }

    [Fact]
    public async Task Listar_Admin_RetornaOkComItensMapeados()
    {
        var clienteId = Guid.NewGuid();
        var log = new AuditLog
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, UsuarioId = Guid.NewGuid(),
            Entidade = "ContaRecorrente", AcaoTipo = "Criacao", EntidadeId = Guid.NewGuid().ToString(),
            DadosAntes = null, DadosDepois = "{}", OcorridoEm = DateTime.UtcNow,
        };
        _repoMock.Setup(r => r.ListarPaginadoAsync(clienteId, null, null, null, null, 1, 50))
            .ReturnsAsync((new List<AuditLog> { log }, 1));

        var result = await CriarSut(Guid.NewGuid(), "admin").Listar(clienteId, null, null, null, null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<AuditLogPaginadoDto>>(ok.Value);
        Assert.Single(body.Dados!.Items);
        Assert.Equal(1, body.Dados.Total);
        Assert.Equal("ContaRecorrente", body.Dados.Items[0].Entidade);
    }

    [Fact]
    public async Task Listar_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.Listar(Guid.NewGuid(), null, null, null, null));
        Assert.Equal(403, ex.StatusCode);
    }
}
