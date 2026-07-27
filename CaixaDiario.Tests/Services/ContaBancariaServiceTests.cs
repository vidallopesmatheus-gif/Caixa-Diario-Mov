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
    private readonly ContaBancariaService _sut;

    public ContaBancariaServiceTests()
    {
        _sut = new ContaBancariaService(_contaRepoMock.Object, _registroRepoMock.Object);
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
        // Mais recente primeiro: recebimento (dia2), saída (dia2), entrada (dia1)
        Assert.Equal(150m, extrato[0].Valor);
        Assert.Contains("(recebimento)", extrato[0].Descricao);
        Assert.Equal(1350m, extrato[0].SaldoAcumulado);

        Assert.Equal(-300m, extrato[1].Valor);
        Assert.Equal(1050m, extrato[1].SaldoAcumulado);

        Assert.Equal(200m, extrato[2].Valor);
        Assert.Equal(1200m, extrato[2].SaldoAcumulado);
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
    public async Task InativarAsync_ComConta_MarcaComoInativaEPersiste()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid(), clienteId);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);
        _contaRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaBancaria>())).ReturnsAsync((ContaBancaria c) => c);

        await _sut.InativarAsync(conta.Id, clienteId, "cliente");

        _contaRepoMock.Verify(r => r.AtualizarAsync(It.Is<ContaBancaria>(c => !c.Ativa)), Times.Once);
    }

    [Fact]
    public async Task InativarAsync_ComContaInexistente_LancaNaoEncontrado()
    {
        var id = Guid.NewGuid();
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(id)).ReturnsAsync((ContaBancaria?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.InativarAsync(id, Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task InativarAsync_ComUsuarioDeOutroCliente_LancaAcessoNegado()
    {
        var conta = CriarConta(Guid.NewGuid(), Guid.NewGuid());
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.InativarAsync(conta.Id, Guid.NewGuid(), "cliente"));

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
}
