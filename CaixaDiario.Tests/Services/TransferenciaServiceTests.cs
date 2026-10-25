using CaixaDiario.API.Data;
using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CaixaDiario.Tests.Services;

public class TransferenciaServiceTests
{
    private readonly Mock<ITransferenciaRepository> _transferenciaRepoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly TransferenciaService _sut;

    // InMemory não é relacional — o serviço detecta isso (Database.IsRelational()) e não tenta abrir
    // transação, então os testes continuam simulando só via mocks dos repositórios.
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public TransferenciaServiceTests()
    {
        _sut = new TransferenciaService(_transferenciaRepoMock.Object, _contaRepoMock.Object, _registroRepoMock.Object, _auditMock.Object, CriarContexto());
    }

    private static ContaBancaria CriarConta(Guid clienteId, string nome, string tipo = "Caixa", decimal saldoInicial = 0m, bool ativa = true) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Nome = nome, Tipo = tipo, SaldoInicial = saldoInicial, Ativa = ativa, DataCriacao = DateTime.UtcNow,
    };

    [Fact]
    public async Task DesfazerClassificacaoAsync_ReverteOsDoisLadosSemApagarNemMudarSaldo()
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
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(origemId)).ReturnsAsync(new List<RegistroDiario> { regOrigem });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(destinoId)).ReturnsAsync(new List<RegistroDiario> { regDestino });

        await _sut.DesfazerClassificacaoAsync(transferenciaId, clienteId, "cliente");

        // Os lançamentos continuam existindo — só voltam a ficar sem classificação, pendentes.
        var saidaRevertida = Assert.Single(regOrigem.Saidas);
        Assert.Equal(300m, saidaRevertida.Valor);
        Assert.Equal(string.Empty, saidaRevertida.Categoria);
        Assert.Null(saidaRevertida.TipoCusto);
        Assert.Null(saidaRevertida.TransferenciaId);
        Assert.True(saidaRevertida.PendenteCategorizacao);
        Assert.Equal(700m, regOrigem.SaldoFinal); // inalterado — o valor do lançamento não mudou

        var entradaRevertida = Assert.Single(regDestino.Entradas);
        Assert.Equal(300m, entradaRevertida.Valor);
        Assert.Null(entradaRevertida.Categoria);
        Assert.Null(entradaRevertida.TipoCusto);
        Assert.Null(entradaRevertida.TransferenciaId);
        Assert.True(entradaRevertida.PendenteCategorizacao);
        Assert.Equal(300m, regDestino.SaldoFinal); // inalterado

        _transferenciaRepoMock.Verify(r => r.RemoverAsync(transferencia), Times.Once);
    }

    [Fact]
    public async Task DesfazerClassificacaoAsync_ContrapartidaProvisoria_ApagaEAjustaSaldoDasDuasContas()
    {
        // Caso real reportado: "Pix recebido" (R$520) virou transferência pra Investimento Nubank
        // sem contrapartida pré-existente — o app criou a saída −520 do zero (Provisoria=true) lá.
        // Desfazer tem que apagar essa saída fantasma, não só tirar o rótulo.
        var clienteId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        var origemId = Guid.NewGuid(); // Investimento Nubank — de onde "saiu" o dinheiro provisório
        var destinoId = Guid.NewGuid(); // conta corrente — onde o Pix realmente foi recebido
        var data = new DateOnly(2026, 10, 15);

        var transferencia = new Transferencia
        {
            Id = transferenciaId, ClienteId = clienteId, ContaOrigemId = origemId, ContaDestinoId = destinoId,
            Data = data, Valor = 520m, CriadoEm = DateTime.UtcNow,
        };

        var regOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = origemId, Data = data, Inicio = 1335.53m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 815.53m,
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Pix recebido", Valor = 520m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId, Provisoria = true } },
        };
        // Lançamento real que já estava no extrato da conta de destino — esse não é provisório.
        var regDestino = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = destinoId, Data = data, Inicio = 0m,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 520m,
            Entradas = new() { new ItemFinanceiro { Descricao = "Pix recebido", Valor = 520m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId, Provisoria = false } },
        };

        _transferenciaRepoMock.Setup(r => r.ObterPorIdAsync(transferenciaId)).ReturnsAsync(transferencia);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(origemId, data)).ReturnsAsync(regOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(destinoId, data)).ReturnsAsync(regDestino);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(origemId)).ReturnsAsync(new List<RegistroDiario> { regOrigem });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(destinoId)).ReturnsAsync(new List<RegistroDiario> { regDestino });

        await _sut.DesfazerClassificacaoAsync(transferenciaId, clienteId, "cliente");

        // A contrapartida provisória (fantasma) some de vez — não vira "Sem categoria" órfã.
        Assert.Empty(regOrigem.Saidas);
        Assert.Equal(1335.53m, regOrigem.SaldoFinal); // 815.53 + 520 — volta ao estado anterior

        // A ponta real (não provisória) só é desvinculada, exatamente como antes.
        var entradaRevertida = Assert.Single(regDestino.Entradas);
        Assert.Null(entradaRevertida.TransferenciaId);
        Assert.True(entradaRevertida.PendenteCategorizacao);
        Assert.Equal(520m, regDestino.SaldoFinal); // inalterado — era um lançamento real

        _transferenciaRepoMock.Verify(r => r.RemoverAsync(transferencia), Times.Once);
    }

    [Fact]
    public async Task DesfazerClassificacaoAsync_RecalculaCadeiaDeSaldoDosDiasSeguintes()
    {
        var clienteId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        var origemId = Guid.NewGuid();
        var destinoId = Guid.NewGuid();
        var data = new DateOnly(2026, 10, 15);
        var diaSeguinte = data.AddDays(1);

        var transferencia = new Transferencia
        {
            Id = transferenciaId, ClienteId = clienteId, ContaOrigemId = origemId, ContaDestinoId = destinoId,
            Data = data, Valor = 520m, CriadoEm = DateTime.UtcNow,
        };
        var regOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = origemId, Data = data, Inicio = 1335.53m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 815.53m,
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Pix recebido", Valor = 520m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId, Provisoria = true } },
        };
        var regDestino = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = destinoId, Data = data, Inicio = 0m,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 520m,
            Entradas = new() { new ItemFinanceiro { Descricao = "Pix recebido", Valor = 520m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId, Provisoria = false } },
        };
        // Dia seguinte da conta de origem já existia com um saldo que ainda assume os −520
        // fantasmas (Inicio = 815.53, o SaldoFinal de regOrigem ANTES de desfazer).
        var regOrigemDiaSeguinte = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = origemId, Data = diaSeguinte,
            Inicio = 815.53m, SaldoFinal = 815.53m, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        };

        _transferenciaRepoMock.Setup(r => r.ObterPorIdAsync(transferenciaId)).ReturnsAsync(transferencia);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(origemId, data)).ReturnsAsync(regOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(destinoId, data)).ReturnsAsync(regDestino);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(origemId)).ReturnsAsync(new List<RegistroDiario> { regOrigem, regOrigemDiaSeguinte });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(destinoId)).ReturnsAsync(new List<RegistroDiario> { regDestino });

        await _sut.DesfazerClassificacaoAsync(transferenciaId, clienteId, "cliente");

        // A cadeia propagou o delta (+520, de 815.53 pra 1335.53) pro dia seguinte da mesma conta.
        Assert.Equal(1335.53m, regOrigemDiaSeguinte.Inicio);
        Assert.Equal(1335.53m, regOrigemDiaSeguinte.SaldoFinal);
    }

    [Fact]
    public async Task DesfazerClassificacaoAsync_TransferenciaInexistente_LancaNaoEncontrada()
    {
        _transferenciaRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Transferencia?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.DesfazerClassificacaoAsync(Guid.NewGuid(), Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.TRANSFERENCIA_NAO_ENCONTRADA, ex.Codigo);
    }

    // ── ExcluirAsync: exclui de verdade as duas pontas (ação "Excluir" num item do extrato) ──────

    [Fact]
    public async Task ExcluirAsync_RemoveAsDuasPontasEAjustaOsSaldos()
    {
        var clienteId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        var origemId = Guid.NewGuid();
        var destinoId = Guid.NewGuid();
        var data = new DateOnly(2026, 10, 15);

        var transferencia = new Transferencia
        {
            Id = transferenciaId, ClienteId = clienteId, ContaOrigemId = origemId, ContaDestinoId = destinoId,
            Data = data, Valor = 520m, CriadoEm = DateTime.UtcNow,
        };
        var regOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = origemId, Data = data, Inicio = 1335.53m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 815.53m,
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Pix recebido", Valor = 520m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId } },
        };
        var regDestino = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = destinoId, Data = data, Inicio = 0m,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 520m,
            Entradas = new() { new ItemFinanceiro { Descricao = "Pix recebido", Valor = 520m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId } },
        };

        _transferenciaRepoMock.Setup(r => r.ObterPorIdAsync(transferenciaId)).ReturnsAsync(transferencia);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(origemId, data)).ReturnsAsync(regOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(destinoId, data)).ReturnsAsync(regDestino);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(origemId)).ReturnsAsync(new List<RegistroDiario> { regOrigem });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(destinoId)).ReturnsAsync(new List<RegistroDiario> { regDestino });

        await _sut.ExcluirAsync(transferenciaId, clienteId, "cliente");

        Assert.Empty(regOrigem.Saidas);
        Assert.Equal(1335.53m, regOrigem.SaldoFinal);
        Assert.Empty(regDestino.Entradas);
        Assert.Equal(0m, regDestino.SaldoFinal);
        _transferenciaRepoMock.Verify(r => r.RemoverAsync(transferencia), Times.Once);
    }

    [Fact]
    public async Task ExcluirAsync_RecalculaCadeiaDeSaldoDosDiasSeguintes()
    {
        var clienteId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        var origemId = Guid.NewGuid();
        var destinoId = Guid.NewGuid();
        var data = new DateOnly(2026, 10, 15);
        var diaSeguinte = data.AddDays(1);

        var transferencia = new Transferencia
        {
            Id = transferenciaId, ClienteId = clienteId, ContaOrigemId = origemId, ContaDestinoId = destinoId,
            Data = data, Valor = 520m, CriadoEm = DateTime.UtcNow,
        };
        var regOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = origemId, Data = data, Inicio = 1335.53m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 815.53m,
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Pix recebido", Valor = 520m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId } },
        };
        var regDestino = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = destinoId, Data = data, Inicio = 0m,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 520m,
            Entradas = new() { new ItemFinanceiro { Descricao = "Pix recebido", Valor = 520m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId } },
        };
        var regOrigemDiaSeguinte = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = origemId, Data = diaSeguinte,
            Inicio = 815.53m, SaldoFinal = 815.53m, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        };

        _transferenciaRepoMock.Setup(r => r.ObterPorIdAsync(transferenciaId)).ReturnsAsync(transferencia);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(origemId, data)).ReturnsAsync(regOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(destinoId, data)).ReturnsAsync(regDestino);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(origemId)).ReturnsAsync(new List<RegistroDiario> { regOrigem, regOrigemDiaSeguinte });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(destinoId)).ReturnsAsync(new List<RegistroDiario> { regDestino });

        await _sut.ExcluirAsync(transferenciaId, clienteId, "cliente");

        Assert.Equal(1335.53m, regOrigemDiaSeguinte.Inicio);
        Assert.Equal(1335.53m, regOrigemDiaSeguinte.SaldoFinal);
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
        // Nunca pode existir uma contrapartida nova sem TransferenciaId — seria uma cópia solta,
        // indistinguível de um lançamento duplicado de verdade.
        Assert.NotNull(entradaDestino.TransferenciaId);
        Assert.Equal(saidaOriginal.TransferenciaId, entradaDestino.TransferenciaId);
        // Provisória: ainda não confirmada pelo extrato real da conta de destino (ver Bloco 3C —
        // é o que permite a importação casar com ela em vez de duplicar).
        Assert.True(entradaDestino.Provisoria);
        Assert.Equal("Aplicação RDB", entradaDestino.Descricao); // mesma descrição do lançamento original

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
            Entradas = new() { new ItemFinanceiro { Id = lancamentoId, Descricao = "Resgate RDB", Valor = 500m, PendenteCategorizacao = true } },
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
        Assert.False(entradaOriginal.PendenteCategorizacao);
        Assert.Equal(800m, registroOrigem.SaldoFinal); // inalterado

        Assert.NotNull(registroDestinoCriado);
        var saidaDestino = Assert.Single(registroDestinoCriado!.Saidas);
        Assert.Equal("Transferencia", saidaDestino.TipoCusto);
        Assert.Equal(500m, saidaDestino.Valor);
        Assert.Equal(500m, registroDestinoCriado.SaldoFinal); // 1000 - 500
        // Nunca pode existir uma contrapartida nova sem TransferenciaId — seria uma cópia solta,
        // indistinguível de um lançamento duplicado de verdade.
        Assert.NotNull(saidaDestino.TransferenciaId);
        Assert.Equal(entradaOriginal.TransferenciaId, saidaDestino.TransferenciaId);
        Assert.True(saidaDestino.Provisoria);
        Assert.Equal("Resgate RDB", saidaDestino.Descricao); // mesma descrição do lançamento original

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
        // Pareado com lançamento real já existente — não é provisória (já foi confirmado pelo banco).
        Assert.False(entradaContrapartida.Provisoria);

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

    // ── CriarAsync (Fase 5.3 — resgate de investimento do zero) ─────────────────────────────────

    [Fact]
    public async Task CriarAsync_CriaAsDuasPontasDoZeroComoProvisoriasEAjustaOsSaldos()
    {
        var clienteId = Guid.NewGuid();
        var contaInvestimento = CriarConta(clienteId, "Investimento", "Investimento", saldoInicial: 10000m);
        var contaPagamento = CriarConta(clienteId, "Conta Corrente", "ContaCorrente", saldoInicial: 500m);
        var data = new DateOnly(2026, 11, 10);

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaInvestimento.Id)).ReturnsAsync(contaInvestimento);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaPagamento.Id)).ReturnsAsync(contaPagamento);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(It.IsAny<Guid>(), data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<RegistroDiario>());
        var criados = new List<RegistroDiario>();
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(criados.Add).ReturnsAsync((RegistroDiario r) => r);
        _transferenciaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Transferencia>())).ReturnsAsync((Transferencia t) => t);

        var resultado = await _sut.CriarAsync(
            clienteId, contaInvestimento.Id, contaPagamento.Id, data, 4000m, "Resgate para 🏆 Macbook",
            clienteId, "cliente");

        var regOrigem = criados.Single(r => r.ContaBancariaId == contaInvestimento.Id);
        var regDestino = criados.Single(r => r.ContaBancariaId == contaPagamento.Id);
        var saida = Assert.Single(regOrigem.Saidas);
        var entrada = Assert.Single(regDestino.Entradas);

        Assert.Equal(4000m, saida.Valor);
        Assert.True(saida.Provisoria);
        Assert.Equal(resultado.Id, saida.TransferenciaId);
        Assert.Equal(6000m, regOrigem.SaldoFinal); // 10000 - 4000

        Assert.Equal(4000m, entrada.Valor);
        Assert.True(entrada.Provisoria);
        Assert.Equal(resultado.Id, entrada.TransferenciaId);
        Assert.Equal(4500m, regDestino.SaldoFinal); // 500 + 4000
    }

    [Fact]
    public async Task CriarAsync_MesmaContaDeOrigemEDestino_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(clienteId, contaId, contaId, new DateOnly(2026, 11, 10), 100m, "x", clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task CriarAsync_ContaInativa_LancaContaInativa()
    {
        var clienteId = Guid.NewGuid();
        var contaOrigem = CriarConta(clienteId, "Investimento", "Investimento", ativa: false);
        var contaDestino = CriarConta(clienteId, "Conta Corrente");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaOrigem.Id)).ReturnsAsync(contaOrigem);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaDestino.Id)).ReturnsAsync(contaDestino);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(clienteId, contaOrigem.Id, contaDestino.Id, new DateOnly(2026, 11, 10), 100m, "x", clienteId, "cliente"));

        Assert.Equal(CodigoRetorno.CONTA_INATIVA, ex.Codigo);
    }

    // ── Bloco 4, item 4: "Transferências sem par" ──────────────────────────────────────────────

    [Fact]
    public async Task ListarSugestoesSemParAsync_PegaSoLancamentosSugeridosComoTransferenciaAindaPendentes()
    {
        var clienteId = Guid.NewGuid();
        var contaCorrente = CriarConta(clienteId, "Nubank", "ContaCorrente");
        var data = new DateOnly(2026, 10, 8);

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCorrente.Id, Data = data, Inicio = 1000m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 700m,
            Saidas = new()
            {
                // Sugerida pelo dicionário de palavras-chave, ainda pendente — é a "sem par" de verdade.
                new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Aplicação RDB", Valor = 300m, Categoria = "Transferência", CategoriaSugerida = true, PendenteCategorizacao = true },
                // Já confirmada (não pendente) — não deve aparecer.
                new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Transf confirmada", Valor = 50m, Categoria = "Transferência", CategoriaSugerida = false, PendenteCategorizacao = false },
                // Categoria normal sugerida, não é transferência — não deve aparecer.
                new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Uber", Valor = 20m, Categoria = "Frete e entrega", CategoriaSugerida = true, PendenteCategorizacao = true },
            },
        };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { contaCorrente });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var sugestoes = await _sut.ListarSugestoesSemParAsync(clienteId, clienteId, "cliente");

        var unica = Assert.Single(sugestoes);
        Assert.Equal("Aplicação RDB", unica.Descricao);
        Assert.Equal(-300m, unica.Valor);
        Assert.Equal("Saida", unica.Tipo);
        Assert.Equal(contaCorrente.Id, unica.ContaBancariaId);
    }

    [Fact]
    public async Task ListarSugestoesSemParAsync_ComUmaUnicaContaDeInvestimentoAtiva_SugereElaComoDestino()
    {
        var clienteId = Guid.NewGuid();
        var contaCorrente = CriarConta(clienteId, "Nubank", "ContaCorrente");
        var contaInvestimento = CriarConta(clienteId, "Nubank Investimento", "Investimento");
        var data = new DateOnly(2026, 10, 8);

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCorrente.Id, Data = data, Inicio = 1000m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 700m,
            Saidas = new() { new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Aplicação RDB", Valor = 300m, Categoria = "Transferência", CategoriaSugerida = true, PendenteCategorizacao = true } },
        };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { contaCorrente, contaInvestimento });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var sugestoes = await _sut.ListarSugestoesSemParAsync(clienteId, clienteId, "cliente");

        var unica = Assert.Single(sugestoes);
        Assert.Equal(contaInvestimento.Id, unica.ContaDestinoSugeridaId);
        Assert.Equal("Nubank Investimento", unica.ContaDestinoSugeridaNome);
    }

    [Fact]
    public async Task ListarSugestoesSemParAsync_ComDuasContasDeInvestimento_NaoArriscaUmaSugestao()
    {
        var clienteId = Guid.NewGuid();
        var contaCorrente = CriarConta(clienteId, "Nubank", "ContaCorrente");
        var investimentoA = CriarConta(clienteId, "Investimento A", "Investimento");
        var investimentoB = CriarConta(clienteId, "Investimento B", "Investimento");
        var data = new DateOnly(2026, 10, 8);

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCorrente.Id, Data = data, Inicio = 1000m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 700m,
            Saidas = new() { new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Aplicação RDB", Valor = 300m, Categoria = "Transferência", CategoriaSugerida = true, PendenteCategorizacao = true } },
        };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { contaCorrente, investimentoA, investimentoB });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var sugestoes = await _sut.ListarSugestoesSemParAsync(clienteId, clienteId, "cliente");

        Assert.Null(Assert.Single(sugestoes).ContaDestinoSugeridaId);
    }

    [Fact]
    public async Task DescartarSugestaoAsync_LimpaCategoriaMasMantemPendenteECategoriaSugeridaFalsa()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId, "Nubank", "ContaCorrente");
        var data = new DateOnly(2026, 10, 8);
        var itemId = Guid.NewGuid();

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = data, Inicio = 1000m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 700m,
            Saidas = new() { new ItemFinanceiroSaida { Id = itemId, Descricao = "Aplicação RDB", Valor = 300m, Categoria = "Transferência", CategoriaSugerida = true, PendenteCategorizacao = true } },
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, data)).ReturnsAsync(registro);
        RegistroDiario? atualizado = null;
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => atualizado = r).ReturnsAsync((RegistroDiario r) => r);

        await _sut.DescartarSugestaoAsync(new DescartarSugestaoTransferenciaDto { ContaId = conta.Id, LancamentoId = itemId, Data = data }, clienteId, "cliente");

        var saida = Assert.Single(atualizado!.Saidas);
        Assert.Equal(string.Empty, saida.Categoria);
        Assert.False(saida.CategoriaSugerida);
        Assert.True(saida.PendenteCategorizacao); // continua pendente — só deixou de sugerir Transferência
    }
}
