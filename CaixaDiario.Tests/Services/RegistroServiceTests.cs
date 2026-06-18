using System.Text.Json;
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
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly Mock<IRecorrenciaService> _recorrenciaMock = new();
    private readonly RegistroService _sut;

    public RegistroServiceTests()
    {
        _auditMock.Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        _recorrenciaMock.Setup(r => r.MaterializarMesAtualAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _sut = new RegistroService(_repoMock.Object, _auditMock.Object, _recorrenciaMock.Object);
    }

    private static CriarRegistroDto CriarDto(DateOnly? data = null)
    {
        return new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = data ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Inicio = 100m,
            Entradas = new List<ItemFinanceiroDto> { new() { Descricao = "Caixa", Valor = 500m } },
            Saidas = new List<ItemFinanceiroDto> { new() { Descricao = "Aluguel", Valor = 200m, Categoria = "Aluguel" } },
            ContasReceber = new(),
            ContasPagar = new(),
            SaldoFinal = 400m
        };
    }

    private static RegistroDiario CriarRegistroComContas(Guid clienteId, DateOnly data) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = clienteId,
        Data = data,
        ContasReceber = new List<ContaProvisionada>
        {
            new() { Descricao = "Venda A", Valor = 500m, DataVencimento = data, Pago = false },
        },
        ContasPagar = new List<ContaProvisionada>
        {
            new() { Descricao = "Fornecedor B", Valor = 200m, DataVencimento = data, Pago = false },
        },
        Entradas = new(),
        Saidas = new(),
        CriadoEm = DateTime.UtcNow,
        SalvoEm = DateTime.UtcNow
    };

    [Fact]
    public async Task SalvarAsync_DataFutura_LancaExcecao()
    {
        var dto = new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "admin"));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DATA_FUTURA, ex.Codigo);
    }

    [Fact]
    public async Task SalvarAsync_ContasComVencimentoNoDia_MarcaComoPagas()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registroExistente = CriarRegistroComContas(clienteId, hoje);

        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje))
            .ReturnsAsync(registroExistente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            ContasReceber = registroExistente.ContasReceber
                .Select(c => new ContaProvisionadaDto { Descricao = c.Descricao, Valor = c.Valor, DataVencimento = c.DataVencimento, Pago = c.Pago })
                .ToList(),
            ContasPagar = registroExistente.ContasPagar
                .Select(c => new ContaProvisionadaDto { Descricao = c.Descricao, Valor = c.Valor, DataVencimento = c.DataVencimento, Pago = c.Pago })
                .ToList(),
            Entradas = new(),
            Saidas = new(),
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.True(resultado.ContasReceber[0].Pago);
        Assert.True(resultado.ContasPagar[0].Pago);
    }

    [Fact]
    public async Task SalvarAsync_Novo_ChamaAuditCriacao()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto { ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new() };
        await _sut.SalvarAsync(dto, "admin");

        _auditMock.Verify(a => a.LogAsync(
            clienteId, It.IsAny<Guid>(), "RegistroDiario", "Criacao",
            It.IsAny<string>(), null, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ListarPorClienteAsync_ChamaRecorrencia()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());

        await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        _recorrenciaMock.Verify(r => r.MaterializarMesAtualAsync(clienteId), Times.Once);
    }

    [Fact]
    public async Task SalvarAsync_SaidaSemCategoria_LancaExcecao()
    {
        var dto = new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = DateOnly.FromDateTime(DateTime.UtcNow),
            Entradas = new(),
            Saidas = new() { new ItemFinanceiroDto { Descricao = "Compra", Valor = 50m, Categoria = null } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
        };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "tester"));
        Assert.Equal(400, ex.StatusCode);
    }

    // --- existing tests preserved below ---

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
            SaldoFinal = 0, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
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

    [Fact]
    public async Task Listar_Admin_RetornaLista()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId,
                Data = DateOnly.FromDateTime(DateTime.UtcNow),
                Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
                CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
            }
        };
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);

        var resultado = await _sut.ListarPorClienteAsync(clienteId, Guid.NewGuid(), "admin");

        Assert.Single(resultado);
    }

    [Fact]
    public async Task Listar_ClienteAcessandoProprioId_RetornaLista()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId,
                Data = DateOnly.FromDateTime(DateTime.UtcNow),
                Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
                CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
            }
        };
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);

        var resultado = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        Assert.Single(resultado);
    }

    [Fact]
    public async Task ObterPorData_Admin_RetornaRegistro()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = data,
            Entradas = new List<ItemFinanceiro> { new() { Descricao = "Caixa", Valor = 10m } },
            Saidas = new List<ItemFinanceiro> { new() { Descricao = "Saida", Valor = 10m } },
            ContasReceber = new List<ContaProvisionada> { new() { Descricao = "CR", Valor = 5m } },
            ContasPagar = new List<ContaProvisionada> { new() { Descricao = "CP", Valor = 3m } },
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, data)).ReturnsAsync(registro);

        var resultado = await _sut.ObterPorDataAsync(clienteId, data, Guid.NewGuid(), "admin");

        Assert.Equal(clienteId, resultado.ClienteId);
    }

    [Fact]
    public async Task ObterPorData_ClienteAcessandoProprioId_RetornaRegistro()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = data,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, data)).ReturnsAsync(registro);

        var resultado = await _sut.ObterPorDataAsync(clienteId, data, clienteId, "cliente");

        Assert.Equal(clienteId, resultado.ClienteId);
    }

    [Fact]
    public async Task ObterPorData_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var usuarioLogadoId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterPorDataAsync(clienteId, data, usuarioLogadoId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    [Fact]
    public async Task ObterPorData_RegistroNaoEncontrado_LancaRegistroNaoEncontrado()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, data)).ReturnsAsync((RegistroDiario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterPorDataAsync(clienteId, data, Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.REGISTRO_NAO_ENCONTRADO, ex.Codigo);
    }

    [Fact]
    public async Task Excluir_RegistroNaoEncontrado_LancaRegistroNaoEncontrado()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, data)).ReturnsAsync((RegistroDiario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ExcluirAsync(clienteId, data, "motivo", Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.REGISTRO_NAO_ENCONTRADO, ex.Codigo);
    }

    [Fact]
    public async Task Excluir_Admin_MarcaComoExcluido()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = data,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, data)).ReturnsAsync(registro);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        await _sut.ExcluirAsync(clienteId, data, "motivo teste", Guid.NewGuid(), "admin");

        _repoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(x => x.Excluido && x.MotivoExclusao == "motivo teste")), Times.Once);
    }

    [Fact]
    public async Task Salvar_NovoRegistroComContasReceberEPagar_MapeiaCorretamente()
    {
        var dto = new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = DateOnly.FromDateTime(DateTime.UtcNow),
            Inicio = 100m,
            Entradas = new List<ItemFinanceiroDto> { new() { Descricao = "Caixa", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto> { new() { Descricao = "CR", Valor = 50m, DataVencimento = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), Pago = false } },
            ContasPagar = new List<ContaProvisionadaDto> { new() { Descricao = "CP", Valor = 30m, Pago = true } },
            SaldoFinal = 520m
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(dto.ClienteId, dto.Data)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.True(criado);
        Assert.Single(resultado.ContasReceber);
        Assert.Single(resultado.ContasPagar);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), resultado.ContasReceber[0].DataVencimento);
        Assert.True(resultado.ContasPagar[0].Pago);
    }
}
