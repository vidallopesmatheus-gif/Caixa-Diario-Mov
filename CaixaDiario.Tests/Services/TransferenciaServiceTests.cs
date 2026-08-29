using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class TransferenciaServiceTests
{
    private readonly Mock<ITransferenciaRepository> _transferenciaRepoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly TransferenciaService _sut;

    public TransferenciaServiceTests()
    {
        _sut = new TransferenciaService(_transferenciaRepoMock.Object, _contaRepoMock.Object, _registroRepoMock.Object, _auditMock.Object);
    }

    private static ContaBancaria CriarConta(Guid clienteId, string nome, string tipo = "Caixa", decimal saldoInicial = 0m, bool ativa = true) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Nome = nome, Tipo = tipo, SaldoInicial = saldoInicial, Ativa = ativa, DataCriacao = DateTime.UtcNow,
    };

    [Fact]
    public async Task CriarAsync_ContasValidasSemRegistroNoDia_CriaParDeLancamentosERecalculaSaldos()
    {
        var clienteId = Guid.NewGuid();
        var origem = CriarConta(clienteId, "Caixa", saldoInicial: 1000m);
        var destino = CriarConta(clienteId, "CDI Nubank", tipo: "Investimento", saldoInicial: 0m);
        var data = new DateOnly(2026, 8, 1);

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(origem.Id)).ReturnsAsync(origem);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(destino.Id)).ReturnsAsync(destino);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(It.IsAny<Guid>(), data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<RegistroDiario>());

        RegistroDiario? registroOrigemAdicionado = null;
        RegistroDiario? registroDestinoAdicionado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.Is<RegistroDiario>(x => x.ContaBancariaId == origem.Id)))
            .Callback<RegistroDiario>(r => registroOrigemAdicionado = r).ReturnsAsync((RegistroDiario r) => r);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.Is<RegistroDiario>(x => x.ContaBancariaId == destino.Id)))
            .Callback<RegistroDiario>(r => registroDestinoAdicionado = r).ReturnsAsync((RegistroDiario r) => r);
        _transferenciaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Transferencia>())).ReturnsAsync((Transferencia t) => t);

        var dto = new CriarTransferenciaDto { ClienteId = clienteId, ContaOrigemId = origem.Id, ContaDestinoId = destino.Id, Data = data, Valor = 300m };
        var resultado = await _sut.CriarAsync(dto, clienteId, "cliente");

        Assert.Equal(300m, resultado.Valor);
        Assert.NotNull(registroOrigemAdicionado);
        Assert.NotNull(registroDestinoAdicionado);

        var saidaOrigem = Assert.Single(registroOrigemAdicionado!.Saidas);
        Assert.Equal("Transferencia", saidaOrigem.TipoCusto);
        Assert.Equal(300m, saidaOrigem.Valor);
        Assert.Equal(700m, registroOrigemAdicionado.SaldoFinal); // 1000 (Inicio = SaldoInicial) - 300

        var entradaDestino = Assert.Single(registroDestinoAdicionado!.Entradas);
        Assert.Equal("Transferencia", entradaDestino.TipoCusto);
        Assert.Equal(300m, entradaDestino.Valor);
        Assert.Equal(300m, registroDestinoAdicionado.SaldoFinal); // 0 + 300
        Assert.Equal(saidaOrigem.TransferenciaId, entradaDestino.TransferenciaId);

        _transferenciaRepoMock.Verify(r => r.AdicionarAsync(It.Is<Transferencia>(t =>
            t.ContaOrigemId == origem.Id && t.ContaDestinoId == destino.Id && t.Valor == 300m)), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_ContaOrigemIgualContaDestino_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var dto = new CriarTransferenciaDto { ClienteId = clienteId, ContaOrigemId = contaId, ContaDestinoId = contaId, Data = new DateOnly(2026, 8, 1), Valor = 10m };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task CriarAsync_ContaInativa_LancaContaInativa()
    {
        var clienteId = Guid.NewGuid();
        var origem = CriarConta(clienteId, "Caixa", ativa: false);
        var destino = CriarConta(clienteId, "Investimentos", tipo: "Investimento");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(origem.Id)).ReturnsAsync(origem);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(destino.Id)).ReturnsAsync(destino);

        var dto = new CriarTransferenciaDto { ClienteId = clienteId, ContaOrigemId = origem.Id, ContaDestinoId = destino.Id, Data = new DateOnly(2026, 8, 1), Valor = 10m };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CONTA_INATIVA, ex.Codigo);
    }

    [Fact]
    public async Task CriarAsync_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var dto = new CriarTransferenciaDto { ClienteId = Guid.NewGuid(), ContaOrigemId = Guid.NewGuid(), ContaDestinoId = Guid.NewGuid(), Data = new DateOnly(2026, 8, 1), Valor = 10m };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task EstornarAsync_RemovePartesDosDoisRegistrosERevertaSaldo()
    {
        var clienteId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        var origemId = Guid.NewGuid();
        var destinoId = Guid.NewGuid();
        var data = new DateOnly(2026, 8, 1);

        var transferencia = new Transferencia
        {
            Id = transferenciaId, ClienteId = clienteId, ContaOrigemId = origemId, ContaDestinoId = destinoId,
            Data = data, Valor = 300m, CriadoEm = DateTime.UtcNow,
        };

        var regOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = origemId, Data = data, Inicio = 1000m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 700m,
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "x", Valor = 300m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId } },
        };
        var regDestino = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = destinoId, Data = data, Inicio = 0m,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 300m,
            Entradas = new() { new ItemFinanceiro { Descricao = "x", Valor = 300m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId } },
        };

        _transferenciaRepoMock.Setup(r => r.ObterPorIdAsync(transferenciaId)).ReturnsAsync(transferencia);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(origemId, data)).ReturnsAsync(regOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(destinoId, data)).ReturnsAsync(regDestino);

        await _sut.EstornarAsync(transferenciaId, clienteId, "cliente");

        Assert.Empty(regOrigem.Saidas);
        Assert.Equal(1000m, regOrigem.SaldoFinal);
        Assert.Empty(regDestino.Entradas);
        Assert.Equal(0m, regDestino.SaldoFinal);
        _transferenciaRepoMock.Verify(r => r.RemoverAsync(transferencia), Times.Once);
    }

    [Fact]
    public async Task EstornarAsync_TransferenciaInexistente_LancaNaoEncontrada()
    {
        _transferenciaRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Transferencia?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.EstornarAsync(Guid.NewGuid(), Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.TRANSFERENCIA_NAO_ENCONTRADA, ex.Codigo);
    }

    // ── ConverterLancamentoAsync: reclassifica um lançamento já real como Transferência ──────────

    [Fact]
    public async Task ConverterLancamentoAsync_SaidaExistente_RelabelaOrigemECriaEntradaNaContrapartida()
    {
        var clienteId = Guid.NewGuid();
        var contaCorrente = CriarConta(clienteId, "Conta Corrente", tipo: "ContaCorrente", saldoInicial: 1000m);
        var contaInvestimento = CriarConta(clienteId, "CDI Nubank", tipo: "Investimento", saldoInicial: 0m);
        var data = new DateOnly(2026, 8, 20);
        var lancamentoId = Guid.NewGuid();

        var registroOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCorrente.Id, Data = data, Inicio = 1500m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 1000m,
            Saidas = new() { new ItemFinanceiroSaida { Id = lancamentoId, Descricao = "Aplicação RDB", Valor = 500m, Categoria = "", PendenteCategorizacao = true } },
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorrente.Id)).ReturnsAsync(contaCorrente);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaInvestimento.Id)).ReturnsAsync(contaInvestimento);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCorrente.Id, data)).ReturnsAsync(registroOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaInvestimento.Id, data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaInvestimento.Id)).ReturnsAsync(new List<RegistroDiario>());

        RegistroDiario? registroDestinoCriado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(registroOrigem)).ReturnsAsync(registroOrigem);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => registroDestinoCriado = r).ReturnsAsync((RegistroDiario r) => r);
        _transferenciaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Transferencia>())).ReturnsAsync((Transferencia t) => t);

        var dto = new ConverterLancamentoEmTransferenciaDto
        {
            ContaId = contaCorrente.Id, LancamentoId = lancamentoId, Data = data, Tipo = "Saida",
            ContaContrapartidaId = contaInvestimento.Id,
        };

        var resultado = await _sut.ConverterLancamentoAsync(dto, clienteId, "cliente");

        // A ponta original só é relabelada — valor e efeito no saldo (já aplicado antes) não mudam.
        var saidaOriginal = Assert.Single(registroOrigem.Saidas);
        Assert.Equal("Transferencia", saidaOriginal.TipoCusto);
        Assert.Equal("Transferência", saidaOriginal.Categoria);
        Assert.False(saidaOriginal.PendenteCategorizacao);
        Assert.Equal(500m, saidaOriginal.Valor);
        Assert.Equal(1000m, registroOrigem.SaldoFinal); // inalterado

        Assert.NotNull(registroDestinoCriado);
        var entradaDestino = Assert.Single(registroDestinoCriado!.Entradas);
        Assert.Equal("Transferencia", entradaDestino.TipoCusto);
        Assert.Equal(500m, entradaDestino.Valor);
        Assert.Equal(500m, registroDestinoCriado.SaldoFinal); // 0 + 500
        Assert.Equal(saidaOriginal.TransferenciaId, entradaDestino.TransferenciaId);

        Assert.Equal(contaCorrente.Id, resultado.ContaOrigemId);
        Assert.Equal(contaInvestimento.Id, resultado.ContaDestinoId);
        Assert.Equal(500m, resultado.Valor);
    }

    [Fact]
    public async Task ConverterLancamentoAsync_EntradaExistente_RelabelaOrigemECriaSaidaNaContrapartida()
    {
        var clienteId = Guid.NewGuid();
        var contaCorrente = CriarConta(clienteId, "Conta Corrente", tipo: "ContaCorrente", saldoInicial: 500m);
        var contaInvestimento = CriarConta(clienteId, "CDI Nubank", tipo: "Investimento", saldoInicial: 1000m);
        var data = new DateOnly(2026, 8, 20);
        var lancamentoId = Guid.NewGuid();

        var registroOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCorrente.Id, Data = data, Inicio = 300m,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 800m,
            Entradas = new() { new ItemFinanceiro { Id = lancamentoId, Descricao = "Resgate RDB", Valor = 500m } },
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorrente.Id)).ReturnsAsync(contaCorrente);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaInvestimento.Id)).ReturnsAsync(contaInvestimento);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCorrente.Id, data)).ReturnsAsync(registroOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaInvestimento.Id, data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaInvestimento.Id)).ReturnsAsync(new List<RegistroDiario>());

        RegistroDiario? registroDestinoCriado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(registroOrigem)).ReturnsAsync(registroOrigem);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => registroDestinoCriado = r).ReturnsAsync((RegistroDiario r) => r);
        _transferenciaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Transferencia>())).ReturnsAsync((Transferencia t) => t);

        var dto = new ConverterLancamentoEmTransferenciaDto
        {
            ContaId = contaCorrente.Id, LancamentoId = lancamentoId, Data = data, Tipo = "Entrada",
            ContaContrapartidaId = contaInvestimento.Id,
        };

        var resultado = await _sut.ConverterLancamentoAsync(dto, clienteId, "cliente");

        var entradaOriginal = Assert.Single(registroOrigem.Entradas);
        Assert.Equal("Transferencia", entradaOriginal.TipoCusto);
        Assert.Equal(800m, registroOrigem.SaldoFinal); // inalterado

        Assert.NotNull(registroDestinoCriado);
        var saidaDestino = Assert.Single(registroDestinoCriado!.Saidas);
        Assert.Equal("Transferencia", saidaDestino.TipoCusto);
        Assert.Equal(500m, saidaDestino.Valor);
        Assert.Equal(500m, registroDestinoCriado.SaldoFinal); // 1000 - 500

        Assert.Equal(contaInvestimento.Id, resultado.ContaOrigemId);
        Assert.Equal(contaCorrente.Id, resultado.ContaDestinoId);
    }

    [Fact]
    public async Task ConverterLancamentoAsync_ComLancamentoContrapartidaExistente_VinculaSemCriarNovo()
    {
        // Cenário do risco de duplicação: o extrato da conta de investimento já foi importado e já
        // trouxe a entrada correspondente — vincular às duas pontas já existentes, sem criar nada novo.
        var clienteId = Guid.NewGuid();
        var contaCorrente = CriarConta(clienteId, "Conta Corrente", tipo: "ContaCorrente", saldoInicial: 1000m);
        var contaInvestimento = CriarConta(clienteId, "CDI Nubank", tipo: "Investimento", saldoInicial: 0m);
        var data = new DateOnly(2026, 8, 20);
        var lancamentoId = Guid.NewGuid();
        var lancamentoContrapartidaId = Guid.NewGuid();

        var registroOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCorrente.Id, Data = data, Inicio = 1500m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 1000m,
            Saidas = new() { new ItemFinanceiroSaida { Id = lancamentoId, Descricao = "Aplicação RDB", Valor = 500m, Categoria = "", PendenteCategorizacao = true } },
        };
        var registroContrapartida = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaInvestimento.Id, Data = data, Inicio = 0m,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 500m,
            Entradas = new() { new ItemFinanceiro { Id = lancamentoContrapartidaId, Descricao = "Aplicação recebida", Valor = 500m } },
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaCorrente.Id)).ReturnsAsync(contaCorrente);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaInvestimento.Id)).ReturnsAsync(contaInvestimento);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaCorrente.Id, data)).ReturnsAsync(registroOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(contaInvestimento.Id, data)).ReturnsAsync(registroContrapartida);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _transferenciaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Transferencia>())).ReturnsAsync((Transferencia t) => t);

        var dto = new ConverterLancamentoEmTransferenciaDto
        {
            ContaId = contaCorrente.Id, LancamentoId = lancamentoId, Data = data, Tipo = "Saida",
            ContaContrapartidaId = contaInvestimento.Id, LancamentoContrapartidaId = lancamentoContrapartidaId, DataContrapartida = data,
        };

        var resultado = await _sut.ConverterLancamentoAsync(dto, clienteId, "cliente");

        // Saldo da contrapartida inalterado — a entrada já estava lá, não foi criada de novo.
        Assert.Equal(500m, registroContrapartida.SaldoFinal);
        var entradaContrapartida = Assert.Single(registroContrapartida.Entradas);
        Assert.Equal("Transferencia", entradaContrapartida.TipoCusto);
        Assert.Equal(1000m, registroOrigem.SaldoFinal); // também inalterado

        var saidaOriginal = Assert.Single(registroOrigem.Saidas);
        Assert.Equal(saidaOriginal.TransferenciaId, entradaContrapartida.TransferenciaId);
        Assert.Equal(resultado.Id, saidaOriginal.TransferenciaId);

        _registroRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task ConverterLancamentoAsync_ContaContrapartidaIgualAOriginal_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Conta Corrente");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var dto = new ConverterLancamentoEmTransferenciaDto
        {
            ContaId = conta.Id, LancamentoId = Guid.NewGuid(), Data = new DateOnly(2026, 8, 20), Tipo = "Saida",
            ContaContrapartidaId = conta.Id,
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConverterLancamentoAsync(dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task ConverterLancamentoAsync_LancamentoInexistente_LancaNaoEncontrado()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Conta Corrente");
        var contrapartida = CriarConta(clienteId, "CDI Nubank", tipo: "Investimento");
        var data = new DateOnly(2026, 8, 20);

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contrapartida.Id)).ReturnsAsync(contrapartida);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, data)).ReturnsAsync(new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = data,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
        });

        var dto = new ConverterLancamentoEmTransferenciaDto
        {
            ContaId = conta.Id, LancamentoId = Guid.NewGuid(), Data = data, Tipo = "Saida",
            ContaContrapartidaId = contrapartida.Id,
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConverterLancamentoAsync(dto, clienteId, "cliente"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ConverterLancamentoAsync_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Conta Corrente");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var dto = new ConverterLancamentoEmTransferenciaDto
        {
            ContaId = conta.Id, LancamentoId = Guid.NewGuid(), Data = new DateOnly(2026, 8, 20), Tipo = "Saida",
            ContaContrapartidaId = Guid.NewGuid(),
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConverterLancamentoAsync(dto, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }
}
