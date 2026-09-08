using CaixaDiario.API.DTOs.ContasRecorrentes;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ContaRecorrenteServiceTests
{
    private readonly Mock<IContaRecorrenteRepository> _repoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private ContaRecorrenteService CriarSut() => new(_repoMock.Object, _registroRepoMock.Object, _auditMock.Object);

    public ContaRecorrenteServiceTests()
    {
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaRecorrente>())).ReturnsAsync((ContaRecorrente c) => c);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<RegistroDiario>());
        _auditMock.Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
    }

    private static ContaRecorrente CriarConta(Guid clienteId) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Descricao = "Aluguel",
        Valor = 1000m, Tipo = "Pagar", DataInicio = new DateOnly(2026, 1, 1),
        Ativo = true, CriadoEm = DateTime.UtcNow,
    };

    private static RegistroDiario CriarRegistro(Guid clienteId, DateOnly data) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Data = data,
        Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
    };

    [Fact]
    public async Task Listar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().ListarPorClienteAsync(Guid.NewGuid(), Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Listar_Admin_RetornaLista()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { CriarConta(clienteId) });
        var resultado = await CriarSut().ListarPorClienteAsync(clienteId, Guid.NewGuid(), "admin");
        Assert.Single(resultado);
    }

    [Fact]
    public async Task Criar_TipoInvalido_LancaDadosInvalidos()
    {
        var dto = new CriarContaRecorrenteDto
        {
            ClienteId = Guid.NewGuid(), Descricao = "Teste", Valor = 100m,
            Tipo = "Invalido", DataInicio = new DateOnly(2026, 1, 1),
        };
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().CriarAsync(dto, dto.ClienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Criar_QuantidadeParcelasAcimaDoTeto_LancaDadosInvalidos()
    {
        var dto = new CriarContaRecorrenteDto
        {
            ClienteId = Guid.NewGuid(), Descricao = "Teste", Valor = 600m,
            Tipo = "Receber", DataInicio = new DateOnly(2026, 1, 1),
            QuantidadeParcelas = 600,
        };
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().CriarAsync(dto, dto.ClienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Criar_Valido_RetornaDto()
    {
        var clienteId = Guid.NewGuid();
        var contaBancariaId = Guid.NewGuid();
        var dto = new CriarContaRecorrenteDto
        {
            ClienteId = clienteId, Descricao = "Aluguel", Valor = 1000m,
            Tipo = "Pagar", DataInicio = new DateOnly(2026, 1, 1), ContaBancariaId = contaBancariaId,
        };
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<ContaRecorrente>()))
            .ReturnsAsync((ContaRecorrente c) => c);

        var resultado = await CriarSut().CriarAsync(dto, clienteId, "cliente");

        Assert.Equal("Aluguel", resultado.Descricao);
        Assert.True(resultado.Ativo);
        Assert.Equal(contaBancariaId, resultado.ContaBancariaId);
    }

    [Fact]
    public async Task Desativar_NaoEncontrada_LancaNaoEncontrada()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, It.IsAny<Guid>()))
            .ReturnsAsync((ContaRecorrente?)null);
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().DesativarAsync(clienteId, Guid.NewGuid(), false, clienteId, "cliente"));
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CONTA_RECORRENTE_NAO_ENCONTRADA, ex.Codigo);
    }

    [Fact]
    public async Task Desativar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().DesativarAsync(Guid.NewGuid(), Guid.NewGuid(), false, Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Desativar_Valido_MarcaInativaERegistraAuditoria()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);

        await CriarSut().DesativarAsync(clienteId, conta.Id, false, clienteId, "cliente");

        Assert.False(conta.Ativo);
        _repoMock.Verify(r => r.AtualizarAsync(It.Is<ContaRecorrente>(c => !c.Ativo)), Times.Once);
        _auditMock.Verify(a => a.LogAsync(clienteId, clienteId, "ContaRecorrente", "Exclusao",
            conta.Id.ToString(), It.IsAny<string?>(), null), Times.Once);
    }

    [Fact]
    public async Task Desativar_SemRemoverPendentes_NaoTocaRegistros()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);

        await CriarSut().DesativarAsync(clienteId, conta.Id, false, clienteId, "cliente");

        _registroRepoMock.Verify(r => r.ListarPorClienteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Desativar_ComRemoverPendentes_RemoveSoAsOcorrenciasNaoPagasDestaRecorrencia()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);

        var registro = CriarRegistro(clienteId, new DateOnly(2026, 9, 10));
        registro.ContasPagar.Add(new ContaProvisionada { Descricao = "Ocorrência pendente", Valor = 100m, RecorrenciaId = conta.Id, Pago = false });
        registro.ContasPagar.Add(new ContaProvisionada { Descricao = "Ocorrência paga", Valor = 100m, RecorrenciaId = conta.Id, Pago = true });
        registro.ContasPagar.Add(new ContaProvisionada { Descricao = "Conta avulsa, sem recorrência", Valor = 50m, Pago = false });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        await CriarSut().DesativarAsync(clienteId, conta.Id, true, clienteId, "cliente");

        _registroRepoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(reg =>
            reg.ContasPagar.Count == 2
            && reg.ContasPagar.Any(c => c.Descricao == "Ocorrência paga")
            && reg.ContasPagar.Any(c => c.Descricao == "Conta avulsa, sem recorrência"))), Times.Once);
    }

    [Fact]
    public async Task Atualizar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().AtualizarAsync(Guid.NewGuid(), Guid.NewGuid(),
                new AtualizarContaRecorrenteDto(), Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Atualizar_NaoEncontrada_LancaNaoEncontrada()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, It.IsAny<Guid>()))
            .ReturnsAsync((ContaRecorrente?)null);
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().AtualizarAsync(clienteId, Guid.NewGuid(),
                new AtualizarContaRecorrenteDto(), clienteId, "cliente"));
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CONTA_RECORRENTE_NAO_ENCONTRADA, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_PeriodicidadeInvalida_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().AtualizarAsync(clienteId, conta.Id, new AtualizarContaRecorrenteDto { Periodicidade = "Diaria" }, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Atualizar_TodosOsCampos_AplicaAlteracoesERegistraAuditoria()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        var novaContaBancariaId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);

        var dto = new AtualizarContaRecorrenteDto
        {
            Descricao = "Aluguel novo",
            Valor = 1500m,
            Categoria = "Despesas Administrativas",
            DataInicio = new DateOnly(2026, 2, 1),
            DataFim = new DateOnly(2026, 12, 31),
            Periodicidade = "Trimestral",
            ContaBancariaId = novaContaBancariaId,
        };

        var resultado = await CriarSut().AtualizarAsync(clienteId, conta.Id, dto, clienteId, "cliente");

        Assert.Equal("Aluguel novo", resultado.Descricao);
        Assert.Equal(1500m, resultado.Valor);
        Assert.Equal("Despesas Administrativas", resultado.Categoria);
        Assert.Equal(new DateOnly(2026, 2, 1), resultado.DataInicio);
        Assert.Equal(new DateOnly(2026, 12, 31), resultado.DataFim);
        Assert.Equal("Trimestral", resultado.Periodicidade);
        Assert.Equal(novaContaBancariaId, resultado.ContaBancariaId);
        _auditMock.Verify(a => a.LogAsync(clienteId, clienteId, "ContaRecorrente", "Edicao",
            conta.Id.ToString(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Atualizar_SemCampos_MantemValoresEAtualiza()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);

        var resultado = await CriarSut().AtualizarAsync(clienteId, conta.Id,
            new AtualizarContaRecorrenteDto(), clienteId, "admin");

        Assert.Equal("Aluguel", resultado.Descricao);
        Assert.Equal(1000m, resultado.Valor);
        _repoMock.Verify(r => r.AtualizarAsync(It.IsAny<ContaRecorrente>()), Times.Once);
    }

    [Fact]
    public async Task Atualizar_SemAplicarAsPendentes_NaoTocaRegistros()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);

        await CriarSut().AtualizarAsync(clienteId, conta.Id, new AtualizarContaRecorrenteDto { Valor = 1200m }, clienteId, "cliente");

        _registroRepoMock.Verify(r => r.ListarPorClienteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Atualizar_ComAplicarAsPendentes_AtualizaSoOcorrenciasNaoPagasDestaRecorrencia()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        var novaContaBancariaId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);

        var registro = CriarRegistro(clienteId, new DateOnly(2026, 9, 10));
        var pendente = new ContaProvisionada { Descricao = "Aluguel", Valor = 1000m, DataVencimento = new DateOnly(2026, 9, 10), RecorrenciaId = conta.Id, Pago = false };
        var paga = new ContaProvisionada { Descricao = "Aluguel", Valor = 1000m, DataVencimento = new DateOnly(2026, 8, 10), RecorrenciaId = conta.Id, Pago = true, DataBaixa = new DateOnly(2026, 8, 10) };
        registro.ContasPagar.Add(pendente);
        registro.ContasPagar.Add(paga);
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registro });

        var dto = new AtualizarContaRecorrenteDto { Valor = 1300m, Descricao = "Aluguel novo", ContaBancariaId = novaContaBancariaId, AplicarAsPendentes = true };
        await CriarSut().AtualizarAsync(clienteId, conta.Id, dto, clienteId, "cliente");

        _registroRepoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(reg =>
            reg.ContasPagar.Single(c => !c.Pago).Valor == 1300m
            && reg.ContasPagar.Single(c => !c.Pago).Descricao == "Aluguel novo"
            && reg.ContasPagar.Single(c => !c.Pago).ContaBancariaId == novaContaBancariaId
            // A paga nunca muda — nem valor, nem descrição, nem data de vencimento.
            && reg.ContasPagar.Single(c => c.Pago).Valor == 1000m
            && reg.ContasPagar.Single(c => c.Pago).DataVencimento == new DateOnly(2026, 8, 10))), Times.Once);
    }
}
