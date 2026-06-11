using CaixaDiario.API.Data;
using CaixaDiario.API.Services;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.Tests.Services;

public class AuditServiceTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task LogAsync_GravaRegistro_ComTodosOsCampos()
    {
        var ctx = CriarContexto();
        var sut = new AuditService(ctx);
        var clienteId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        await sut.LogAsync(clienteId, usuarioId, "RegistroDiario", "Criacao", "id-123", null, "{\"saldo\":100}");

        var log = ctx.AuditLogs.Single();
        Assert.Equal(clienteId, log.ClienteId);
        Assert.Equal(usuarioId, log.UsuarioId);
        Assert.Equal("RegistroDiario", log.Entidade);
        Assert.Equal("Criacao", log.AcaoTipo);
        Assert.Equal("id-123", log.EntidadeId);
        Assert.Null(log.DadosAntes);
        Assert.Equal("{\"saldo\":100}", log.DadosDepois);
    }

    [Fact]
    public async Task LogAsync_MultiplasChamadas_GravaMultiplosRegistros()
    {
        var ctx = CriarContexto();
        var sut = new AuditService(ctx);
        var clienteId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        await sut.LogAsync(clienteId, usuarioId, "RegistroDiario", "Criacao", "id-1", null, "{}");
        await sut.LogAsync(clienteId, usuarioId, "RegistroDiario", "Edicao", "id-1", "{}", "{\"saldo\":200}");

        Assert.Equal(2, ctx.AuditLogs.Count());
    }
}
