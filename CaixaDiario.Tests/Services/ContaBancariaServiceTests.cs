using CaixaDiario.API.DTOs.ContasBancarias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ContaBancariaServiceTests
{
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IMetaRepository> _metaRepoMock = new();
    private readonly Mock<ITransferenciaRepository> _transferenciaRepoMock = new();
    private readonly Mock<ITransacaoImportadaRepository> _transacaoImportadaRepoMock = new();
    private readonly Mock<IContaRecorrenteRepository> _contaRecorrenteRepoMock = new();
    private readonly Mock<IFaturaRepository> _faturaRepoMock = new();
    private readonly Mock<IPagamentoFaturaRepository> _pagamentoFaturaRepoMock = new();
    private readonly ContaBancariaService _sut;

    public ContaBancariaServiceTests()
    {
        _metaRepoMock.Setup(r => r.ListarPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<MetaAnual>());
        // Default "sem vínculo nenhum" — testes de ExcluirOuInativarAsync sobrescrevem o que precisarem.
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<RegistroDiario>());
        _registroRepoMock.Setup(r => r.ContarTodosPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(0);
        _transferenciaRepoMock.Setup(r => r.ListarPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Transferencia>());
        _transacaoImportadaRepoMock.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<TransacaoImportada>());
        _contaRecorrenteRepoMock.Setup(r => r.ContarPorContaBancariaAsync(It.IsAny<Guid>())).ReturnsAsync(0);
        // Fallback padrão pro item 4 (Money Vision "faturas em aberto") — a maioria dos testes não
        // é de cartão de crédito, só não pode quebrar com NullReferenceException.
        _faturaRepoMock.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Fatura>());
        _pagamentoFaturaRepoMock.Setup(r => r.ListarPorContaCartaoAsync(It.IsAny<Guid>())).ReturnsAsync(new List<PagamentoFatura>());
        _sut = new ContaBancariaService(
            _contaRepoMock.Object, _registroRepoMock.Object, _metaRepoMock.Object,
            _transferenciaRepoMock.Object, _transacaoImportadaRepoMock.Object, _contaRecorrenteRepoMock.Object,
            _faturaRepoMock.Object, _pagamentoFaturaRepoMock.Object);
    }

    private static ContaBancaria CriarConta(Guid id, Guid clienteId, decimal saldoInicial = 1000m) => new()
    {
        Id = id,
        ClienteId = clienteId,
        Nome = "Conta Teste",
        Tipo = "ContaCorrente",
        SaldoInicial = saldoInicial,
        Ativa = true,
        DataCriacao = DateTime.UtcNow,
    };

    [Fact]
    public async Task ObterExtratoAsync_ComEntradasSaidasEBaixas_CalculaSaldoAcumuladoIncluindoBaixas()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(contaId, clienteId);
        var dia1 = new DateOnly(2026, 7, 1);
        var dia2 = new DateOnly(2026, 7, 2);

        var registroDia1 = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            ContaBancariaId = contaId,
            Data = dia1,
            Inicio = 1000m,
            Entradas = new List<ItemFinanceiro> { new() { Descricao = "Venda", Valor = 200m, Categoria = "Vendas" } },
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new(),
            SaldoFinal = 1200m,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
        };
        var registroDia2 = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            ContaBancariaId = contaId,
            Data = dia2,
            Inicio = 1200m,
            Entradas = new(),
            Saidas = new List<ItemFinanceiroSaida> { new() { Descricao = "Aluguel", Valor = 300m, Categoria = "Aluguel" } },
            ContasReceber = new List<ContaProvisionada>
            {
                new() { Descricao = "Cliente X", Valor = 150m, Pago = true, DataBaixa = dia2, ContaBancariaId = contaId },
            },
            ContasPagar = new(),
            SaldoFinal = 1050m,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        // Repositório real retorna em ordem decrescente por data — o serviço deve reordenar.
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId))
            .ReturnsAsync(new List<RegistroDiario> { registroDia2, registroDia1 });

        var extrato = await _sut.ObterExtratoAsync(contaId, clienteId, "cliente", null, null);

        Assert.Equal(3, extrato.Count);
        // Item 2.7: dentro do MESMO dia, a linha do topo é a ÚLTIMA processada (saída), não a
        // primeira (recebimento) — é ela que bate com o SaldoFinal (1050) armazenado pro dia2;
        // o recebimento tem um saldo intermediário (1350) que nunca é "o saldo atual da conta".
        Assert.Equal(-300m, extrato[0].Valor);
        Assert.Equal(1050m, extrato[0].SaldoAcumulado);

        Assert.Equal(150m, extrato[1].Valor);
        Assert.Contains("(recebimento)", extrato[1].Descricao);
        Assert.Equal(1350m, extrato[1].SaldoAcumulado);

        Assert.Equal(200m, extrato[2].Valor);
        Assert.Equal(1200m, extrato[2].SaldoAcumulado);
    }

    // Item 2.7 — critério de aceite: a linha do topo do extrato é sempre o saldo atual de
    // verdade da conta, mesmo quando o dia mais recente tem várias linhas (entradas E saídas).
    [Fact]
    public async Task ObterExtratoAsync_DiaComVariasLinhas_LinhaDoTopoTemOSaldoAtualDaConta()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(contaId, clienteId);
        var dia = new DateOnly(2026, 7, 10);

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dia,
            Inicio = 1000m,
            Entradas = new List<ItemFinanceiro>
            {
                new() { Descricao = "Venda 1", Valor = 200m, Categoria = "Vendas" },
                new() { Descricao = "Venda 2", Valor = 100m, Categoria = "Vendas" },
            },
            Saidas = new List<ItemFinanceiroSaida> { new() { Descricao = "Fornecedor", Valor = 50m, Categoria = "Insumos" } },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 1250m, // 1000 + 200 + 100 - 50
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var extrato = await _sut.ObterExtratoAsync(contaId, clienteId, "cliente", null, null);

        var saldoAtual = ContaBancariaService.ObterSaldoAtual(conta, new List<RegistroDiario> { registro });
        Assert.Equal(1250m, saldoAtual);
        Assert.Equal(saldoAtual, extrato[0].SaldoAcumulado);
        Assert.Equal("Fornecedor", extrato[0].Descricao); // última linha processada no dia
    }

    // Fase 0.2: baixa vinculada a um lançamento já existente (ou que a própria baixa criou, Fase
    // 0.5) não pode sintetizar uma 2ª linha "(pagamento)/(recebimento)" — aquele dinheiro já está
    // contado pelo lançamento real, que já aparece no extrato via Entradas/Saidas.
    [Fact]
    public async Task ObterExtratoAsync_ContaPagaVinculadaALancamentoExistente_NaoSintetizaLinhaDuplicada()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(contaId, clienteId);
        var dia = new DateOnly(2026, 7, 1);
        var lancamentoId = Guid.NewGuid();

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dia,
            Inicio = 1000m, Entradas = new(),
            Saidas = new List<ItemFinanceiroSaida> { new() { Id = lancamentoId, Descricao = "Pix CLARO", Valor = 35.69m, Categoria = "Energia/Água/Internet" } },
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Telefone", Valor = 30m, Pago = true, DataBaixa = dia, ContaBancariaId = contaId, LancamentoVinculadoId = lancamentoId },
            },
            SaldoFinal = 964.31m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var extrato = await _sut.ObterExtratoAsync(contaId, clienteId, "cliente", null, null);

        var linha = Assert.Single(extrato);
        Assert.Equal("Pix CLARO", linha.Descricao);
        Assert.Equal(-35.69m, linha.Valor);
        Assert.Equal(964.31m, linha.SaldoAcumulado);
    }

    // Fase 0.6: SaldoAtual (cartão/card no Banco) tem que ser sempre igual à última linha do
    // extrato — inclusive quando há baixa vinculada (0.2), que não pode fazer os dois divergirem.
    [Fact]
    public async Task ObterPorIdAsync_SaldoAtual_IgualAUltimaLinhaDoExtrato()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(contaId, clienteId);
        var dia = new DateOnly(2026, 7, 1);
        var lancamentoId = Guid.NewGuid();

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dia,
            Inicio = 1000m, Entradas = new(),
            Saidas = new List<ItemFinanceiroSaida> { new() { Id = lancamentoId, Descricao = "Pix CLARO", Valor = 35.69m, Categoria = "Energia/Água/Internet" } },
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Telefone", Valor = 30m, Pago = true, DataBaixa = dia, ContaBancariaId = contaId, LancamentoVinculadoId = lancamentoId },
            },
            SaldoFinal = 964.31m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var contaDto = await _sut.ObterPorIdAsync(contaId, clienteId, "cliente");
        var extrato = await _sut.ObterExtratoAsync(contaId, clienteId, "cliente", null, null);

        Assert.Equal(extrato.OrderByDescending(l => l.Data).First().SaldoAcumulado, contaDto.SaldoAtual);
    }

    [Fact]
    public async Task ObterExtratoAsync_ComEntradaPendenteDeCategorizacao_PropagaFlagNoDto()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(contaId, clienteId);
        var dia = new DateOnly(2026, 7, 1);

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            ContaBancariaId = contaId,
            Data = dia,
            Inicio = 1000m,
            Entradas = new List<ItemFinanceiro>
            {
                new() { Descricao = "Transferência recebida pelo Pix", Valor = 330m, Categoria = null, PendenteCategorizacao = true },
            },
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new(),
            SaldoFinal = 1330m,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var extrato = await _sut.ObterExtratoAsync(contaId, clienteId, "cliente", null, null);

        Assert.True(extrato[0].PendenteCategorizacao);
    }

    // Money Vision — Bloco 3: TipoCusto precisa chegar no DTO pro front saber que é um Ajuste de
    // Saldo (chip cinza/itálico, sem editar categoria nem marcar transferência) — sem isso o front
    // não tem como diferenciar essa linha de uma saída comum categorizada como "Devoluções".
    [Fact]
    public async Task ObterExtratoAsync_ComAjusteDeSaldo_PropagaTipoCustoNoDto()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(contaId, clienteId);
        var dia = new DateOnly(2026, 9, 30);

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            ContaBancariaId = contaId,
            Data = dia,
            Inicio = 10000m,
            Entradas = new(),
            Saidas = new List<ItemFinanceiroSaida>
            {
                new() { Descricao = "saldo", Valor = 3895.76m, Categoria = "Devoluções", TipoCusto = "AjusteSaldo" },
            },
            ContasReceber = new(),
            ContasPagar = new(),
            SaldoFinal = 10000m - 3895.76m,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var extrato = await _sut.ObterExtratoAsync(contaId, clienteId, "cliente", null, null);

        Assert.Equal("AjusteSaldo", extrato[0].TipoCusto);
    }

    [Fact]
    public async Task ObterExtratoAsync_ComFiltroDePeriodo_MantemSaldoAcumuladoDoHistoricoCompleto()
    {
        var contaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(contaId, clienteId);
        var dia1 = new DateOnly(2026, 7, 1);
        var dia2 = new DateOnly(2026, 7, 2);

        var registroDia1 = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dia1, Inicio = 1000m,
            Entradas = new List<ItemFinanceiro> { new() { Descricao = "Venda", Valor = 200m } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 1200m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        var registroDia2 = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = dia2, Inicio = 1200m,
            Entradas = new(),
            Saidas = new List<ItemFinanceiroSaida> { new() { Descricao = "Aluguel", Valor = 300m, Categoria = "Aluguel" } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 900m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(contaId))
            .ReturnsAsync(new List<RegistroDiario> { registroDia2, registroDia1 });

        var extrato = await _sut.ObterExtratoAsync(contaId, clienteId, "cliente", dia2, dia2);

        // Só a linha do dia2 aparece, mas o saldo acumulado continua refletindo o histórico completo (900, não 200-300).
        var linha = Assert.Single(extrato);
        Assert.Equal(-300m, linha.Valor);
        Assert.Equal(900m, linha.SaldoAcumulado);
    }

    [Fact]
    public async Task ObterPendenciasAsync_FiltraApenasPendenciasVinculadasAContaInformada()
    {
        var contaId = Guid.NewGuid();
        var outraContaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(contaId, clienteId);
        var data = new DateOnly(2026, 7, 1);

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            ContaBancariaId = outraContaId, // o registro pode pertencer a outra conta
            Data = data,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new List<ContaProvisionada>
            {
                new() { Descricao = "A receber desta conta", Valor = 500m, Pago = false, ContaBancariaId = contaId },
                new() { Descricao = "A receber de outra conta", Valor = 999m, Pago = false, ContaBancariaId = outraContaId },
                new() { Descricao = "Já recebido", Valor = 111m, Pago = true, ContaBancariaId = contaId },
            },
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "A pagar desta conta", Valor = 300m, Pago = false, ContaBancariaId = contaId },
            },
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var pendencias = await _sut.ObterPendenciasAsync(contaId, clienteId, "cliente");

        var recebivel = Assert.Single(pendencias.Recebiveis);
        Assert.Equal("A receber desta conta", recebivel.Descricao);

        var pagamento = Assert.Single(pendencias.Pagamentos);
        Assert.Equal("A pagar desta conta", pagamento.Descricao);
    }

    [Fact]
    public async Task ListarPorClienteAsync_RetornaContasComSaldoAtualDoUltimoRegistro()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 500m);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id,
            Data = new DateOnly(2026, 7, 1),
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 800m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var contas = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        var dto = Assert.Single(contas);
        Assert.Equal(800m, dto.SaldoAtual);
    }

    // Money Vision: "card mostra R$ 0,00, mas a API e as faturas dizem outra coisa" (item 4) — o
    // ledger pode fechar em saldo positivo (pagamentos cobrindo as compras e mais um pouco) mesmo
    // com uma Fatura real ainda sem ValorPago nenhum (pagamento feito mas nunca vinculado a ela) —
    // FaturasEmAberto tem que refletir a fatura de verdade, nunca o SaldoDevedor do ledger inteiro.
    [Fact]
    public async Task ListarPorClienteAsync_CartaoComFaturaNaoPagaEPagamentoNaoVinculado_FaturasEmAbertoUsaAFaturaReal()
    {
        var clienteId = Guid.NewGuid();
        var contaCartao = new ContaBancaria
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Cartões", Tipo = "CartaoCredito",
            SaldoInicial = 0m, Ativa = true, DataCriacao = DateTime.UtcNow,
        };
        var faturaId = Guid.NewGuid();
        var fatura = new Fatura
        {
            Id = faturaId, ClienteId = clienteId, ContaBancariaId = contaCartao.Id, Banco = "Nubank",
            Competencia = "2026-10", DataVencimento = new DateOnly(2026, 10, 20), CriadoEm = DateTime.UtcNow,
        };
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaCartao.Id, Data = new DateOnly(2026, 10, 5),
            Entradas = new()
            {
                // Pagamento que caiu no ledger (saldo positivo) mas nunca foi vinculado a nenhuma
                // fatura (PagamentoFaturaId null) — não conta no SaldoDevedor da Fatura real.
                new ItemFinanceiro { Descricao = "Pix para Nubank", Valor = 2500m },
            },
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Compra", Valor = 2000m, FaturaId = faturaId } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 500m,
        };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { contaCartao });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _faturaRepoMock.Setup(r => r.ListarPorContaAsync(contaCartao.Id)).ReturnsAsync(new List<Fatura> { fatura });
        _pagamentoFaturaRepoMock.Setup(r => r.ListarPorContaCartaoAsync(contaCartao.Id)).ReturnsAsync(new List<PagamentoFatura>());

        var contas = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        var dto = Assert.Single(contas);
        Assert.Equal(500m, dto.SaldoAtual); // ledger: positivo (crédito a favor)
        Assert.Equal(0m, dto.SaldoDevedor); // SaldoDevedor do ledger: zero (Math.Max(0, -500))
        Assert.Equal(2000m, dto.FaturasEmAberto); // a Fatura real ainda deve os 2000 da compra
    }

    [Fact]
    public async Task ListarPorClienteAsync_ComEntradaESaidaPendentes_ContaAsDuas()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id,
            Data = new DateOnly(2026, 7, 1),
            Entradas = new List<ItemFinanceiro> { new() { Descricao = "Pix recebido", Valor = 100m, PendenteCategorizacao = true } },
            Saidas = new List<ItemFinanceiroSaida> { new() { Descricao = "Compra", Valor = 50m, PendenteCategorizacao = true } },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 1050m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var contas = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        var dto = Assert.Single(contas);
        Assert.Equal(2, dto.PendentesCategorizacao);
    }

    [Fact]
    public async Task ListarPorClienteAsync_ComUsuarioDeOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var outroUsuarioId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ListarPorClienteAsync(clienteId, outroUsuarioId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    [Fact]
    public async Task ObterPorIdAsync_ComContaInexistente_LancaNaoEncontrado()
    {
        var id = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(id)).ReturnsAsync((ContaBancaria?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterPorIdAsync(id, Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.REGISTRO_NAO_ENCONTRADO, ex.Codigo);
    }

    [Fact]
    public async Task ObterPorIdAsync_ComConta_RetornaDto()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());

        var dto = await _sut.ObterPorIdAsync(conta.Id, clienteId, "cliente");

        Assert.Equal(conta.Id, dto.Id);
        Assert.Equal(conta.SaldoInicial, dto.SaldoAtual);
    }

    [Fact]
    public async Task CriarAsync_ComTipoValido_CriaContaAtiva()
    {
        var clienteId = Guid.NewGuid();
        var dto = new CriarContaBancariaDto { ClienteId = clienteId, Nome = "  Conta Nova  ", Tipo = "Caixa", SaldoInicial = 100m };
        _contaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<ContaBancaria>()))
            .ReturnsAsync((ContaBancaria c) => c);

        var criada = await _sut.CriarAsync(dto, clienteId, "cliente");

        Assert.Equal("Conta Nova", criada.Nome);
        Assert.True(criada.Ativa);
        Assert.Equal(100m, criada.SaldoAtual);
        _contaRepoMock.Verify(r => r.AdicionarAsync(It.Is<ContaBancaria>(c => c.Nome == "Conta Nova" && c.Ativa)), Times.Once);
    }

    // Item E1 do bundle D6JWEAJa: saldo inicial negativo é uma situação real (conta que já começa
    // no cheque especial) — nunca houve bloqueio no backend, mas o front mascarava o "-" no input;
    // este teste trava que o backend continua aceitando, caso algum dia uma validação seja adicionada
    // aqui por engano.
    [Fact]
    public async Task CriarAsync_ComSaldoInicialNegativo_CriaContaNormalmente()
    {
        var clienteId = Guid.NewGuid();
        var dto = new CriarContaBancariaDto { ClienteId = clienteId, Nome = "Conta no cheque especial", Tipo = "ContaCorrente", SaldoInicial = -500m };
        _contaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<ContaBancaria>()))
            .ReturnsAsync((ContaBancaria c) => c);

        var criada = await _sut.CriarAsync(dto, clienteId, "cliente");

        Assert.Equal(-500m, criada.SaldoAtual);
        _contaRepoMock.Verify(r => r.AdicionarAsync(It.Is<ContaBancaria>(c => c.SaldoInicial == -500m)), Times.Once);
    }

    [Fact]
    public async Task AtualizarAsync_ComSaldoInicialNegativo_AtualizaNormalmente()
    {
        var clienteId = Guid.NewGuid();
        var conta = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Conta", Tipo = "ContaCorrente", SaldoInicial = 100m, Ativa = true };
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _contaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaBancaria>())).ReturnsAsync((ContaBancaria c) => c);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());
        var dto = new AtualizarContaBancariaDto { Nome = "Conta", Tipo = "ContaCorrente", SaldoInicial = -1200m, Ativa = true };

        var atualizada = await _sut.AtualizarAsync(conta.Id, dto, clienteId, "cliente");

        Assert.Equal(-1200m, atualizada.SaldoAtual);
    }

    // Money Vision: "saldo não fecha com o extrato" — editar o saldo inicial depois que a conta já
    // tem registros nunca resincronizava a cadeia; o primeiro dia ficava com o Inicio antigo pra
    // sempre e todo o extrato arrastava a diferença.
    [Fact]
    public async Task AtualizarAsync_MudaSaldoInicialComRegistroExistente_RecalculaACadeiaInteira()
    {
        var clienteId = Guid.NewGuid();
        var conta = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Conta", Tipo = "ContaCorrente", SaldoInicial = 100m, Ativa = true };
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 10, 1),
            Inicio = 100m, SaldoFinal = 150m, Entradas = new() { new ItemFinanceiro { Descricao = "x", Valor = 50m } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        };
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _contaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaBancaria>())).ReturnsAsync((ContaBancaria c) => c);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        var dto = new AtualizarContaBancariaDto { Nome = "Conta", Tipo = "ContaCorrente", SaldoInicial = 300m, Ativa = true };

        await _sut.AtualizarAsync(conta.Id, dto, clienteId, "cliente");

        // Delta local do dia (SaldoFinal − Inicio = +50) preservado, só a âncora mudou: 300 + 50.
        Assert.Equal(300m, registro.Inicio);
        Assert.Equal(350m, registro.SaldoFinal);
    }

    [Fact]
    public async Task CriarAsync_ComTipoInvalido_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var dto = new CriarContaBancariaDto { ClienteId = clienteId, Nome = "Conta", Tipo = "Poupanca", SaldoInicial = 0m };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task CriarAsync_ComUsuarioDeOutroCliente_LancaAcessoNegado()
    {
        var dto = new CriarContaBancariaDto { ClienteId = Guid.NewGuid(), Nome = "Conta", Tipo = "Caixa" };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task AtualizarAsync_ComDadosValidos_AtualizaCampos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        var dto = new AtualizarContaBancariaDto { Nome = "  Renomeada  ", Tipo = "Investimento", SaldoInicial = 250m, Ativa = false };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _contaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaBancaria>())).ReturnsAsync((ContaBancaria c) => c);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());

        var atualizada = await _sut.AtualizarAsync(conta.Id, dto, clienteId, "cliente");

        Assert.Equal("Renomeada", atualizada.Nome);
        Assert.Equal("Investimento", atualizada.Tipo);
        Assert.Equal(250m, atualizada.SaldoInicial);
        Assert.False(atualizada.Ativa);
    }

    [Fact]
    public async Task AtualizarAsync_ComContaInexistente_LancaNaoEncontrado()
    {
        var id = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(id)).ReturnsAsync((ContaBancaria?)null);
        var dto = new AtualizarContaBancariaDto { Nome = "X", Tipo = "Caixa" };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AtualizarAsync(id, dto, Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task AtualizarAsync_ComTipoInvalido_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        var dto = new AtualizarContaBancariaDto { Nome = "X", Tipo = "Cripto" };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AtualizarAsync(conta.Id, dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task AtualizarAsync_ComUsuarioDeOutroCliente_LancaAcessoNegado()
    {
        var conta = CriarConta(Guid.NewGuid(), Guid.NewGuid());
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        var dto = new AtualizarContaBancariaDto { Nome = "X", Tipo = "Caixa" };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AtualizarAsync(conta.Id, dto, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ExcluirOuInativarAsync_SemNenhumVinculo_ExcluiDeVerdade()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 1m);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var resultado = await _sut.ExcluirOuInativarAsync(conta.Id, clienteId, "cliente");

        Assert.True(resultado.Excluida);
        Assert.Equal(0, resultado.TotalVinculos);
        _contaRepoMock.Verify(r => r.RemoverAsync(conta), Times.Once);
        _contaRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<ContaBancaria>()), Times.Never);
    }

    [Fact]
    public async Task ExcluirOuInativarAsync_ComDiaDeRegistro_ApenasInativaEInformaContagem()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _contaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaBancaria>())).ReturnsAsync((ContaBancaria c) => c);
        // Inclui um dia soft-deletado (Excluido=true) — ContarTodosPorContaAsync não filtra isso,
        // por isso é ele (e não ListarPorContaAsync) que decide se a conta pode ser excluída de verdade.
        _registroRepoMock.Setup(r => r.ContarTodosPorContaAsync(conta.Id)).ReturnsAsync(2);

        var resultado = await _sut.ExcluirOuInativarAsync(conta.Id, clienteId, "cliente");

        Assert.False(resultado.Excluida);
        Assert.Equal(2, resultado.DiasComLancamento);
        Assert.Equal(2, resultado.TotalVinculos);
        _contaRepoMock.Verify(r => r.AtualizarAsync(It.Is<ContaBancaria>(c => !c.Ativa)), Times.Once);
        _contaRepoMock.Verify(r => r.RemoverAsync(It.IsAny<ContaBancaria>()), Times.Never);
    }

    [Fact]
    public async Task ExcluirOuInativarAsync_ComContaProvisionadaEmRegistroDeOutraConta_ContaComoVinculo()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        var outraContaId = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _contaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaBancaria>())).ReturnsAsync((ContaBancaria c) => c);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = outraContaId, Data = new DateOnly(2026, 7, 1),
                Entradas = new(), Saidas = new(),
                ContasReceber = new List<ContaProvisionada> { new() { Descricao = "A receber", Valor = 100m, ContaBancariaId = conta.Id } },
                ContasPagar = new(),
                CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            },
        });

        var resultado = await _sut.ExcluirOuInativarAsync(conta.Id, clienteId, "cliente");

        Assert.False(resultado.Excluida);
        Assert.Equal(1, resultado.ContasProvisionadas);
    }

    [Fact]
    public async Task ExcluirOuInativarAsync_ComTransferenciaComoOrigemOuDestino_ContaComoVinculo()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _contaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaBancaria>())).ReturnsAsync((ContaBancaria c) => c);
        _transferenciaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<Transferencia>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, ContaOrigemId = conta.Id, ContaDestinoId = Guid.NewGuid(), Data = new DateOnly(2026, 7, 1), Valor = 50m },
        });

        var resultado = await _sut.ExcluirOuInativarAsync(conta.Id, clienteId, "cliente");

        Assert.False(resultado.Excluida);
        Assert.Equal(1, resultado.Transferencias);
    }

    [Fact]
    public async Task ExcluirOuInativarAsync_ComMetaVinculada_ContaComoVinculo()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _contaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaBancaria>())).ReturnsAsync((ContaBancaria c) => c);
        _metaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, ContaInvestimentoId = conta.Id, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow },
        });

        var resultado = await _sut.ExcluirOuInativarAsync(conta.Id, clienteId, "cliente");

        Assert.False(resultado.Excluida);
        Assert.Equal(1, resultado.MetasVinculadas);
    }

    [Fact]
    public async Task ExcluirOuInativarAsync_ComContaRecorrenteVinculada_ContaComoVinculo()
    {
        // Fase 0.2: ContaRecorrente.ContaBancariaId é obrigatória agora (FK Restrict) — sem essa
        // contagem, a exclusão física ia bater na constraint do banco em vez de só inativar com
        // uma mensagem clara.
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _contaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaBancaria>())).ReturnsAsync((ContaBancaria c) => c);
        _contaRecorrenteRepoMock.Setup(r => r.ContarPorContaBancariaAsync(conta.Id)).ReturnsAsync(1);

        var resultado = await _sut.ExcluirOuInativarAsync(conta.Id, clienteId, "cliente");

        Assert.False(resultado.Excluida);
        Assert.Equal(1, resultado.ContasRecorrentes);
    }

    [Fact]
    public async Task ExcluirOuInativarAsync_ComContaInexistente_LancaNaoEncontrado()
    {
        var id = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(id)).ReturnsAsync((ContaBancaria?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ExcluirOuInativarAsync(id, Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ExcluirOuInativarAsync_ComUsuarioDeOutroCliente_LancaAcessoNegado()
    {
        var conta = CriarConta(Guid.NewGuid(), Guid.NewGuid());
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ExcluirOuInativarAsync(conta.Id, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ObterExtratoAsync_ComContaInexistente_LancaNaoEncontrado()
    {
        var id = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(id)).ReturnsAsync((ContaBancaria?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterExtratoAsync(id, Guid.NewGuid(), "admin", null, null));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ObterPendenciasAsync_ComContaInexistente_LancaNaoEncontrado()
    {
        var id = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(id)).ReturnsAsync((ContaBancaria?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterPendenciasAsync(id, Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task RegistrarRendimentoAsync_ContaInvestimentoValorPositivo_AdicionaEntradaEAtualizaSaldo()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 1000m);
        conta.Tipo = "Investimento";
        var data = new DateOnly(2026, 8, 1);

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());

        RegistroDiario? adicionado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => adicionado = r).ReturnsAsync((RegistroDiario r) => r);

        var dto = new RegistrarRendimentoDto { Data = data, Valor = 50m };
        await _sut.RegistrarRendimentoAsync(conta.Id, dto, clienteId, "cliente");

        Assert.NotNull(adicionado);
        var entrada = Assert.Single(adicionado!.Entradas);
        Assert.Equal("Rendimento", entrada.TipoCusto);
        Assert.Equal(1050m, adicionado.SaldoFinal);
    }

    // Money Vision: "saldo não fecha com o extrato" — registrar rendimento nunca cascateava; um dia
    // futuro já materializado (ex.: recorrência) ficava com Inicio desatualizado pra sempre.
    [Fact]
    public async Task RegistrarRendimentoAsync_ComDiaFuturoJaMaterializado_RecalculaOInicioDele()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 1000m);
        conta.Tipo = "Investimento";
        var data = new DateOnly(2026, 8, 1);
        var registroFuturo = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = data.AddDays(10),
            Inicio = 1000m, SaldoFinal = 1000m, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, data)).ReturnsAsync((RegistroDiario?)null);
        var registrosDaConta = new List<RegistroDiario> { registroFuturo };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(() => registrosDaConta.ToList());
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(registrosDaConta.Add).ReturnsAsync((RegistroDiario r) => r);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new RegistrarRendimentoDto { Data = data, Valor = 50m };
        await _sut.RegistrarRendimentoAsync(conta.Id, dto, clienteId, "cliente");

        Assert.Equal(1050m, registroFuturo.Inicio);
        Assert.Equal(1050m, registroFuturo.SaldoFinal);
    }

    [Fact]
    public async Task RegistrarRendimentoAsync_ContaNaoInvestimento_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var dto = new RegistrarRendimentoDto { Data = new DateOnly(2026, 8, 1), Valor = 50m };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.RegistrarRendimentoAsync(conta.Id, dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    // ── AjustarSaldoAsync (item 4, Money Vision "saldo não fecha com o extrato") ──────────────────

    [Fact]
    public async Task AjustarSaldoAsync_SaldoRealMaiorQueOCalculado_CriaEntradaDeAjuste()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 1000m);
        var hoje = DataLocalHelper.Hoje();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, hoje)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());

        RegistroDiario? adicionado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => adicionado = r).ReturnsAsync((RegistroDiario r) => r);

        var dto = new AjustarSaldoDto { Data = hoje, SaldoReal = 1132.66m };
        await _sut.AjustarSaldoAsync(conta.Id, dto, clienteId, "cliente");

        Assert.NotNull(adicionado);
        var entrada = Assert.Single(adicionado!.Entradas);
        Assert.Equal(132.66m, entrada.Valor);
        Assert.Equal("Ajuste de saldo", entrada.Categoria);
        Assert.Equal(LancamentoFiltro.TipoAjusteSaldo, entrada.TipoCusto);
        Assert.False(LancamentoFiltro.EhOperacional(entrada.TipoCusto)); // nunca conta no DRE
        Assert.Equal(1132.66m, adicionado.SaldoFinal);
    }

    [Fact]
    public async Task AjustarSaldoAsync_SaldoRealMenorQueOCalculado_CriaSaidaDeAjuste()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 1000m);
        var hoje = DataLocalHelper.Hoje();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, hoje)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());

        RegistroDiario? adicionado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => adicionado = r).ReturnsAsync((RegistroDiario r) => r);

        var dto = new AjustarSaldoDto { Data = hoje, SaldoReal = 850m };
        await _sut.AjustarSaldoAsync(conta.Id, dto, clienteId, "cliente");

        Assert.NotNull(adicionado);
        var saida = Assert.Single(adicionado!.Saidas);
        Assert.Equal(150m, saida.Valor);
        Assert.Equal("Ajuste de saldo", saida.Categoria);
        Assert.Equal(LancamentoFiltro.TipoAjusteSaldo, saida.TipoCusto);
        Assert.Equal(850m, adicionado.SaldoFinal);
    }

    [Fact]
    public async Task AjustarSaldoAsync_SaldoRealIgualAoCalculado_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 1000m);
        var hoje = DataLocalHelper.Hoje();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, hoje)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());

        var dto = new AjustarSaldoDto { Data = hoje, SaldoReal = 1000m };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AjustarSaldoAsync(conta.Id, dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task AjustarSaldoAsync_DataFutura_LancaDataFutura()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 1000m);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var dto = new AjustarSaldoDto { Data = DataLocalHelper.Hoje().AddDays(1), SaldoReal = 1200m };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AjustarSaldoAsync(conta.Id, dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DATA_FUTURA, ex.Codigo);
    }

    // Mesma garantia de cascata das outras mutações de saldo deste service.
    [Fact]
    public async Task AjustarSaldoAsync_ComDiaFuturoJaMaterializado_RecalculaOInicioDele()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 1000m);
        var hoje = DataLocalHelper.Hoje();
        var registroFuturo = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = hoje.AddDays(5),
            Inicio = 1000m, SaldoFinal = 1000m, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        };
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, hoje)).ReturnsAsync((RegistroDiario?)null);
        var registrosDaConta = new List<RegistroDiario> { registroFuturo };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(() => registrosDaConta.ToList());
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(registrosDaConta.Add).ReturnsAsync((RegistroDiario r) => r);
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new AjustarSaldoDto { Data = hoje, SaldoReal = 1132.66m };
        await _sut.AjustarSaldoAsync(conta.Id, dto, clienteId, "cliente");

        Assert.Equal(1132.66m, registroFuturo.Inicio);
        Assert.Equal(1132.66m, registroFuturo.SaldoFinal);
    }

    [Fact]
    public async Task VincularMetaAsync_ContaInvestimentoEMetaDoCliente_SetaContaInvestimentoIdNaMeta()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        conta.Tipo = "Investimento";
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, ValorSonho = 2000m };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _metaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual> { meta });
        _metaRepoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());

        await _sut.VincularMetaAsync(conta.Id, meta.Id, clienteId, "cliente");

        Assert.Equal(conta.Id, meta.ContaInvestimentoId);
        _metaRepoMock.Verify(r => r.SalvarAsync(It.Is<MetaAnual>(m => m.ContaInvestimentoId == conta.Id)), Times.Once);
    }

    [Fact]
    public async Task VincularMetaAsync_ContaNaoInvestimento_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.VincularMetaAsync(conta.Id, Guid.NewGuid(), clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task ListarPorClienteAsync_ContaInvestimentoComTransferenciasERendimento_CalculaAportadoRendimentoEProgresso()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 0m);
        conta.Tipo = "Investimento";
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, ValorSonho = 2000m, ContaInvestimentoId = conta.Id };

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 8, 1),
            Entradas = new()
            {
                new() { Descricao = "Aporte", Valor = 1000m, Categoria = "Transferência", TipoCusto = "Transferencia" },
                new() { Descricao = "Rendimento", Valor = 50m, Categoria = "Rendimento", TipoCusto = "Rendimento" },
            },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 1050m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });
        _metaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual> { meta });

        var contas = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        var dto = Assert.Single(contas);
        Assert.Equal(1000m, dto.TotalAportado);
        Assert.Equal(50m, dto.RendimentoAcumulado);
        Assert.Equal(5.0m, dto.RentabilidadePercentual); // 50 / 1000 * 100
        Assert.Equal(52.5m, dto.ProgressoCombinadoPercentual);
        Assert.Single(dto.MetasVinculadas!);
    }

    [Fact]
    public async Task ListarPorClienteAsync_ContaInvestimentoSemAporte_RentabilidadeNula()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 0m);
        conta.Tipo = "Investimento";

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());

        var contas = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        var dto = Assert.Single(contas);
        Assert.Equal(0m, dto.TotalAportado);
        Assert.Null(dto.RentabilidadePercentual);
    }

    // ── Bloco 5 (Money Vision): resumo do mês, rentabilidade do mês/ano e "ritmo de aporte" ──────

    [Fact]
    public async Task ListarPorClienteAsync_ContaInvestimento_SeparaAporteResgateERendimentoDoMesAtual()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 0m);
        conta.Tipo = "Investimento";
        var hoje = DataLocalHelper.Hoje();
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);

        var regMesPassado = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = inicioMes.AddMonths(-2),
            Entradas = new() { new() { Descricao = "Aporte antigo", Valor = 5000m, Categoria = "Transferência", TipoCusto = "Transferencia" } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 5000m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        var regMesAtual = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = inicioMes,
            Entradas = new() { new() { Descricao = "Aporte", Valor = 1300m, Categoria = "Transferência", TipoCusto = "Transferencia" } },
            Saidas = new() { new() { Descricao = "Resgate", Valor = 300m, Categoria = "Transferência", TipoCusto = "Transferencia" } },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 6000m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { regMesPassado, regMesAtual });
        _metaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual>());

        var contas = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        var dto = Assert.Single(contas);
        Assert.Equal(1300m, dto.AporteMesAtual);
        Assert.Equal(300m, dto.ResgateMesAtual);
        Assert.Equal(1000m, dto.AporteLiquidoMesAtual);
        // O mês de 2 meses atrás não entra no mês atual nem conta como rendimento.
        Assert.Equal(0m, dto.RendimentoMesAtual);
    }

    [Fact]
    public async Task ListarPorClienteAsync_MetaVinculada_ComRitmoDeAportePositivo_PreveDataDeAtingimento()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 0m);
        conta.Tipo = "Investimento";
        var hoje = DataLocalHelper.Hoje();
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        // Meta de 10.000, saldo atual 4.000, faltam 6.000 — aportando 1.000/mês (média dos 3
        // meses anteriores), são necessários 6 meses.
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = hoje.Year, ValorSonho = 10000m, ContaInvestimentoId = conta.Id };

        var registros = Enumerable.Range(1, 3)
            .Select(i => new RegistroDiario
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = inicioMes.AddMonths(-i),
                Entradas = new() { new() { Descricao = "Aporte", Valor = 1000m, Categoria = "Transferência", TipoCusto = "Transferencia" } },
                Saidas = new(), ContasReceber = new(), ContasPagar = new(),
                SaldoFinal = 4000m - (2 - i) * 1000m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            })
            .ToList();
        // Saldo final do registro mais recente (o de -1 mês) é o que conta como saldo atual.
        registros.OrderBy(r => r.Data).Last().SaldoFinal = 4000m;

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);
        _metaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual> { meta });

        var contas = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        var dto = Assert.Single(contas);
        var metaDto = Assert.Single(dto.MetasVinculadas!);
        var esperado = inicioMes.AddMonths(6);
        var mesesAbrev = new[] { "jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez" };
        Assert.Equal($"{mesesAbrev[esperado.Month - 1]}/{esperado.Year}", metaDto.PrevisaoAtingimentoMesAno);
    }

    [Fact]
    public async Task ListarPorClienteAsync_MetaJaAtingida_NaoPreveData()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 5000m);
        conta.Tipo = "Investimento";
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = DataLocalHelper.Hoje().Year, ValorSonho = 1000m, ContaInvestimentoId = conta.Id };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _metaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual> { meta });

        var contas = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        var dto = Assert.Single(contas);
        Assert.Null(Assert.Single(dto.MetasVinculadas!).PrevisaoAtingimentoMesAno);
    }

    [Fact]
    public async Task ListarPorClienteAsync_MetaNaoAtingida_ComRitmoDeAporteZeroOuNegativo_NaoPreveData()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 0m);
        conta.Tipo = "Investimento";
        var hoje = DataLocalHelper.Hoje();
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
        // Meta ainda não atingida (faltam > 0), mas os últimos 3 meses tiveram resgate líquido
        // (ritmo <= 0) — não há base pra projetar uma data de chegada.
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = hoje.Year, ValorSonho = 10000m, ContaInvestimentoId = conta.Id };

        var registros = Enumerable.Range(1, 3)
            .Select(i => new RegistroDiario
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = inicioMes.AddMonths(-i),
                Entradas = new(),
                Saidas = new() { new() { Descricao = "Resgate", Valor = 200m, Categoria = "Transferência", TipoCusto = "Transferencia" } },
                ContasReceber = new(), ContasPagar = new(),
                SaldoFinal = 4000m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
            })
            .ToList();

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);
        _metaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual> { meta });

        var contas = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        var dto = Assert.Single(contas);
        Assert.Null(Assert.Single(dto.MetasVinculadas!).PrevisaoAtingimentoMesAno);
    }

    [Fact]
    public async Task ObterHistoricoSaldoMensalAsync_RetornaSaldoDeFimDeCadaMes()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 100m);
        conta.Tipo = "Investimento";
        var hoje = DataLocalHelper.Hoje();
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);

        var regMesAnterior = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = inicioMes.AddMonths(-1),
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 500m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { regMesAnterior });

        var historico = await _sut.ObterHistoricoSaldoMensalAsync(conta.Id, clienteId, "cliente", meses: 3);

        Assert.Equal(3, historico.Count);
        // Mês atual e o seguinte (não existe, mas "meses" é retroativo então não aparece) — os 3
        // meses retornados são [atual-2, atual-1, atual]. atual-1 já tem o registro (500), os
        // outros dois caem no saldo inicial (100) por não ter nenhum lançamento até ali.
        Assert.Equal(100m, historico[0].Saldo); // atual - 2 meses, antes de qualquer lançamento
        Assert.Equal(500m, historico[1].Saldo); // atual - 1 mês, já tem o registro
        Assert.Equal(500m, historico[2].Saldo); // mês atual, nenhum lançamento novo, mantém o último saldo
        Assert.Equal($"{inicioMes.Year:D4}-{inicioMes.Month:D2}", historico[2].Mes);
    }

    [Fact]
    public async Task ListarPorClienteAsync_ContaComTransferenciaERendimentoNoMes_NaoEntramEmEntradasMesOuSaidasMes()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId, saldoInicial: 0m);
        var hoje = DataLocalHelper.Hoje();

        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = hoje,
            Entradas = new()
            {
                new() { Descricao = "Venda", Valor = 500m, Categoria = "Vendas", TipoCusto = "Receita" },
                new() { Descricao = "Transferência recebida", Valor = 1000m, Categoria = "Transferência", TipoCusto = "Transferencia" },
            },
            Saidas = new()
            {
                new() { Descricao = "Aluguel", Valor = 200m, Categoria = "Aluguel", TipoCusto = "CustoFixo" },
                new() { Descricao = "Transferência enviada", Valor = 300m, Categoria = "Transferência", TipoCusto = "Transferencia" },
            },
            ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 1000m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var contas = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        var dto = Assert.Single(contas);
        Assert.Equal(500m, dto.EntradasMes); // só a venda — a transferência recebida fica de fora
        Assert.Equal(200m, dto.SaidasMes); // só o aluguel — a transferência enviada fica de fora
    }
}
