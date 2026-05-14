using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class RegistroServiceTests
{
    private readonly Mock<IRegistroRepository> _repoMock = new();
    private readonly RegistroService _sut;

    public RegistroServiceTests()
    {
        _sut = new RegistroService(_repoMock.Object);
    }

    private static CriarRegistroDto CriarDto(DateOnly? data = null)
    {
        return new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = data ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Inicio = 100m,
            Entrada = 500m,
            Saidas = new List<ItemFinanceiroDto> { new() { Descricao = "Aluguel", Valor = 200m } },
            ContasReceber = new(),
            ContasPagar = new(),
            SaldoFinal = 400m
        };
    }

    [Fact]
    public async Task Salvar_DataFutura_LancaDataFutura()
    {
        var dto = CriarDto(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "joao"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DATA_FUTURA, ex.Codigo);
    }

    [Fact]
    public async Task Salvar_RegistroNovo_RetornaCriadoTrue()
    {
        var dto = CriarDto();
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(dto.ClienteId, dto.Data)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, criado) = await _sut.SalvarAsync(dto, "joao");

        Assert.True(criado);
        Assert.Equal(dto.SaldoFinal, resultado.SaldoFinal);
        Assert.Equal(dto.ClienteId, resultado.ClienteId);
    }

    [Fact]
    public async Task Salvar_RegistroExistente_RetornaCriadoFalse()
    {
        var dto = CriarDto();
        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = dto.ClienteId, Data = dto.Data,
            SaldoFinal = 0, Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(dto.ClienteId, dto.Data)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, criado) = await _sut.SalvarAsync(dto, "joao");

        Assert.False(criado);
        Assert.Equal(dto.SaldoFinal, resultado.SaldoFinal);
    }

    [Fact]
    public async Task Excluir_SemMotivo_LancaMotivoObrigatorio()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ExcluirAsync(clienteId, data, "", clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.MOTIVO_OBRIGATORIO, ex.Codigo);
    }

    [Fact]
    public async Task Excluir_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var usuarioLogadoId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ExcluirAsync(clienteId, data, "motivo", usuarioLogadoId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    [Fact]
    public async Task Listar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var usuarioLogadoId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ListarPorClienteAsync(clienteId, usuarioLogadoId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }
}
