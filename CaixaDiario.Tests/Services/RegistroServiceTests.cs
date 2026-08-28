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
    private readonly Mock<IContaBancariaRepository> _contaBancariaMock = new();
    private readonly RegistroService _sut;

    public RegistroServiceTests()
    {
        _auditMock.Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        _recorrenciaMock.Setup(r => r.MaterializarMesAtualAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _sut = new RegistroService(_repoMock.Object, _auditMock.Object, _recorrenciaMock.Object, _contaBancariaMock.Object);
    }

    private static CriarRegistroDto CriarDto(DateOnly? data = null)
    {
        return new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = data ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Inicio = 100m,
            Entradas = new List<ItemFinanceiroDto> { new() { Descricao = "Caixa", Valor = 500m } },
            Saidas = new List<ItemFinanceiroSaidaDto> { new() { Descricao = "Aluguel", Valor = 200m, Categoria = "Aluguel" } },
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
    public async Task SalvarAsync_ComLancamentoDeTransferencia_PreservaTransferenciaIdNoRoundTrip()
    {
        // Um dia com um lançamento de transferência já salvo (TransferenciaService) precisa
        // sobreviver a um resave normal da tela de Caixa sem perder o vínculo — senão o Estornar
        // não consegue mais achar a ponta certa pra remover (TransferenciaId some silenciosamente).
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var transferenciaId = Guid.NewGuid();

        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new List<ItemFinanceiroDto>
            {
                new() { Descricao = "Transferência recebida", Valor = 500m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId },
            },
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new(),
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        var entrada = Assert.Single(resultado.Entradas);
        Assert.Equal(transferenciaId, entrada.TransferenciaId);
    }

    [Fact]
    public async Task SalvarAsync_ContaDuplicadaNaoAjustaSaldo()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var contaId = Guid.NewGuid();
        var registroExistente = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            ContaBancariaId = contaId,
            Data = hoje.AddDays(-1),
            Entradas = new(),
            Saidas = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Fornecedor X", Valor = 300m, DataVencimento = hoje, Pago = true, DataBaixa = hoje.AddDays(-1), ContaBancariaId = contaId },
            },
            ContasReceber = new(),
            SaldoFinal = 700m,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
        };

        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroExistente });
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Fornecedor X", Valor = 300m, DataVencimento = hoje, Pago = true, ContaBancariaId = contaId },
            },
            SaldoFinal = 1000m,
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.False(resultado.ContasPagar[0].Pago);
        Assert.Equal(1000m, resultado.SaldoFinal);
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
            Saidas = new() { new ItemFinanceiroSaidaDto { Descricao = "Compra", Valor = 50m, Categoria = null! } },
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
            Saidas = new List<ItemFinanceiroSaida> { new() { Descricao = "Saida", Valor = 10m } },
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

    // --- D7: baixa financeira ajusta o saldo automaticamente ---

    private (RegistroDiario existente, CriarRegistroDto dto) SetupBaixa(
        bool pagarPago, bool receberPago, bool existentePagarPago = false, bool existenteReceberPago = false,
        DateOnly? dataBaixaPagarExistente = null, DateOnly? dataBaixaReceberExistente = null)
    {
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(),
            ContasReceber = new List<ContaProvisionada>
            {
                new() { Descricao = "Venda A", Valor = 500m, Pago = existenteReceberPago, DataBaixa = dataBaixaReceberExistente },
            },
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Fornecedor B", Valor = 200m, Pago = existentePagarPago, DataBaixa = dataBaixaPagarExistente },
            },
            SaldoFinal = 1000m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Venda A", Valor = 500m, Pago = receberPago,
                    DataBaixa = receberPago == existenteReceberPago ? dataBaixaReceberExistente : null },
            },
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Fornecedor B", Valor = 200m, Pago = pagarPago,
                    DataBaixa = pagarPago == existentePagarPago ? dataBaixaPagarExistente : null },
            },
            SaldoFinal = 1000m, // frontend reenvia o saldo inalterado
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        return (existente, dto);
    }

    [Fact]
    public async Task SalvarAsync_BaixarContaPagarPendente_ReduzSaldoESetaDataBaixa()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var (_, dto) = SetupBaixa(pagarPago: true, receberPago: false);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(800m, resultado.SaldoFinal); // 1000 - 200
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(hoje, resultado.ContasPagar[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_BaixarContaReceberPendente_AumentaSaldoESetaDataBaixa()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var (_, dto) = SetupBaixa(pagarPago: false, receberPago: true);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(1500m, resultado.SaldoFinal); // 1000 + 500
        Assert.True(resultado.ContasReceber[0].Pago);
        Assert.Equal(hoje, resultado.ContasReceber[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_SalvarNovamenteSemMudarPago_NaoReajustaSaldo()
    {
        var ontem = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        // conta a pagar já estava paga e baixada; reenvia paga (sem transição)
        var (_, dto) = SetupBaixa(pagarPago: true, receberPago: false,
            existentePagarPago: true, dataBaixaPagarExistente: ontem);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(1000m, resultado.SaldoFinal); // inalterado: idempotente
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(ontem, resultado.ContasPagar[0].DataBaixa); // mantém a data original
    }

    [Fact]
    public async Task SalvarAsync_DesfazerBaixaContaPagar_ReverteSaldoELimpaDataBaixa()
    {
        var ontem = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        // conta a pagar estava paga/baixada (saldo já tinha sido reduzido); agora desmarca
        var (_, dto) = SetupBaixa(pagarPago: false, receberPago: false,
            existentePagarPago: true, dataBaixaPagarExistente: ontem);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(1200m, resultado.SaldoFinal); // 1000 + 200 (reverte a saída)
        Assert.False(resultado.ContasPagar[0].Pago);
        Assert.Null(resultado.ContasPagar[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_BaixarContaReceberVinculadaALancamentoExistente_NaoDuplicaOSaldo()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var lancamentoId = Guid.NewGuid();
        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            // O dinheiro já está contado: entrada manual de 500 já bate com a "Venda A".
            Entradas = new() { new() { Id = lancamentoId, Descricao = "Pix recebido", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new List<ContaProvisionada> { new() { Descricao = "Venda A", Valor = 500m, Pago = false } },
            ContasPagar = new(),
            SaldoFinal = 1500m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje,
            Entradas = new List<ItemFinanceiroDto> { new() { Id = lancamentoId, Descricao = "Pix recebido", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Venda A", Valor = 500m, Pago = true, LancamentoVinculadoId = lancamentoId },
            },
            ContasPagar = new(),
            SaldoFinal = 1500m, // frontend reenvia o saldo inalterado — a entrada já estava contada
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(1500m, resultado.SaldoFinal); // sem duplicar: não soma mais 500
        Assert.True(resultado.ContasReceber[0].Pago);
        Assert.Equal(lancamentoId, resultado.ContasReceber[0].LancamentoVinculadoId);
        Assert.Equal(hoje, resultado.ContasReceber[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_DesfazerBaixaVinculada_NaoAlteraSaldo()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var ontem = hoje.AddDays(-1);
        var lancamentoId = Guid.NewGuid();
        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new() { new() { Id = lancamentoId, Descricao = "Pix recebido", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new List<ContaProvisionada>
            {
                new() { Descricao = "Venda A", Valor = 500m, Pago = true, DataBaixa = ontem, LancamentoVinculadoId = lancamentoId },
            },
            ContasPagar = new(),
            SaldoFinal = 1500m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje,
            Entradas = new List<ItemFinanceiroDto> { new() { Id = lancamentoId, Descricao = "Pix recebido", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto> { new() { Descricao = "Venda A", Valor = 500m, Pago = false } },
            ContasPagar = new(),
            SaldoFinal = 1500m,
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(1500m, resultado.SaldoFinal); // nada a reverter: a baixa vinculada nunca somou nada
        Assert.False(resultado.ContasReceber[0].Pago);
        Assert.Null(resultado.ContasReceber[0].DataBaixa);
        Assert.Null(resultado.ContasReceber[0].LancamentoVinculadoId);
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

    // --- C2: cobertura de caminhos de criação/auto-baixa/lista-variável ---

    [Fact]
    public async Task SalvarAsync_CriacaoComContaPagarJaPaga_AjustaSaldoESetaDataBaixa()
    {
        // Gap 1: registro NOVO (existente=null) com ContaPagar já paga deve aplicar baixa (−valor, DataBaixa setada)
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Fornecedor X", Valor = 300m, Pago = true },
            },
            SaldoFinal = 1000m,
        };

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.True(criado);
        Assert.Equal(700m, resultado.SaldoFinal); // 1000 - 300
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(hoje, resultado.ContasPagar[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_CriacaoComContaReceberJaPaga_AumentaSaldoESetaDataBaixa()
    {
        // Gap 1 (opcional): registro NOVO com ContaReceber já paga deve aplicar baixa (+valor, DataBaixa setada)
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Venda Y", Valor = 400m, Pago = true },
            },
            ContasPagar = new(),
            SaldoFinal = 1000m,
        };

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.True(criado);
        Assert.Equal(1400m, resultado.SaldoFinal); // 1000 + 400
        Assert.True(resultado.ContasReceber[0].Pago);
        Assert.Equal(hoje, resultado.ContasReceber[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_ResaveComContaJaPagaNoExistente_NaoReajustaSaldo()
    {
        // Gap 2: após criado com conta paga (existente retorna registro com pago=true) não deve reajustar
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var dataBaixa = hoje.AddDays(-1);

        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Fornecedor X", Valor = 300m, Pago = true, DataBaixa = dataBaixa },
            },
            SaldoFinal = 700m, // já com o ajuste da baixa anterior aplicado
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        // reenvia a mesma conta como paga (sem transição)
        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Fornecedor X", Valor = 300m, Pago = true, DataBaixa = dataBaixa },
            },
            SaldoFinal = 700m, // frontend reenvia saldo atual
        };

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.False(criado);
        Assert.Equal(700m, resultado.SaldoFinal); // idempotente: não reduz de novo
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(dataBaixa, resultado.ContasPagar[0].DataBaixa); // preserva data original
    }

    [Fact]
    public async Task SalvarAsync_AutoBaixaPorVencimento_AjustaSaldoUmaVez()
    {
        // Gap 3 (primeiro save): ContaPagar com DataVencimento == dto.Data e pago=false.
        // AplicarBaixaAutomatica marca pago=true; AplicarBaixaFinanceira deve detectar transição (antes=false) e ajustar uma vez.
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Boleto Z", Valor = 150m, DataVencimento = hoje, Pago = false },
            },
            SaldoFinal = 1000m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Boleto Z", Valor = 150m, DataVencimento = hoje, Pago = false },
            },
            SaldoFinal = 1000m,
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        // AplicarBaixaAutomatica marca pago=true; ajuste financeiro deve ocorrer UMA vez (−150)
        Assert.Equal(850m, resultado.SaldoFinal); // 1000 - 150
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(hoje, resultado.ContasPagar[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_AutoBaixaPorVencimento_SegundoSaveNaoReajusta()
    {
        // Gap 3 (segundo save): existente já tem pago=true; dto reenvia pago=false mas auto-baixa marcará true.
        // Não deve reajustar o saldo porque a transição real já ocorreu no save anterior.
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Boleto Z", Valor = 150m, DataVencimento = hoje, Pago = true, DataBaixa = hoje },
            },
            SaldoFinal = 850m, // já com o ajuste aplicado
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        // frontend reenvia pago=false, mas a auto-baixa marca true novamente
        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Boleto Z", Valor = 150m, DataVencimento = hoje, Pago = false },
            },
            SaldoFinal = 850m,
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        // auto-baixa marca pago=true; existente também era true → sem transição → sem reajuste
        Assert.Equal(850m, resultado.SaldoFinal); // idempotente
        Assert.True(resultado.ContasPagar[0].Pago);
    }

    [Fact]
    public async Task SalvarAsync_ListaTamanhoMaior_ContaPreExistentePagaNaoReajustaNovaNaoAjusta()
    {
        // Gap 4: existente tem 1 conta paga; dto tem 2 contas (primeira igual paga, segunda nova não paga).
        // A conta paga pré-existente NÃO é reajustada; a conta nova não paga não gera ajuste; sem erro de índice.
        var clienteId = Guid.NewGuid();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var dataBaixa = hoje.AddDays(-2);

        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Conta Antiga", Valor = 100m, Pago = true, DataBaixa = dataBaixa },
            },
            SaldoFinal = 900m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Conta Antiga", Valor = 100m, Pago = true, DataBaixa = dataBaixa },
                new() { Descricao = "Conta Nova", Valor = 50m, Pago = false },
            },
            SaldoFinal = 900m,
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(900m, resultado.SaldoFinal); // sem reajuste: conta paga já estava paga; conta nova está não-paga
        Assert.Equal(2, resultado.ContasPagar.Count);
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(dataBaixa, resultado.ContasPagar[0].DataBaixa); // data original preservada
        Assert.False(resultado.ContasPagar[1].Pago);
        Assert.Null(resultado.ContasPagar[1].DataBaixa);
    }

    [Fact]
    public async Task Salvar_ComCategoriaNaSaida_MapeiaCorretamente()
    {
        var dto = new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = DateOnly.FromDateTime(DateTime.UtcNow),
            Inicio = 0m,
            Entradas = new(),
            Saidas = new List<ItemFinanceiroSaidaDto>
            {
                new() { Descricao = "Aluguel", Valor = 1200m, Categoria = "Administrativas", Subcategoria = "Aluguel" }
            },
            ContasReceber = new(),
            ContasPagar = new(),
            SaldoFinal = 0m
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(dto.ClienteId, dto.Data))
                 .ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
                 .ReturnsAsync((RegistroDiario r) => r);

        var (resultado, _) = await _sut.SalvarAsync(dto, "joao");

        Assert.Single(resultado.Saidas);
        Assert.Equal("Administrativas", resultado.Saidas[0].Categoria);
        Assert.Equal("Aluguel", resultado.Saidas[0].Subcategoria);
    }
}
