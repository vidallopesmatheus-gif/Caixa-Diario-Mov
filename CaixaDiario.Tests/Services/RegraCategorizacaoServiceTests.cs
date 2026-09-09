using CaixaDiario.API.DTOs.Regras;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class RegraCategorizacaoServiceTests
{
    private readonly Mock<IRegraCategorizacaoRepository> _regraRepoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<ICategoriaRepository> _categoriaRepoMock = new();
    private readonly Mock<ITransferenciaService> _transferenciaServiceMock = new();
    private readonly RegraCategorizacaoService _sut;

    public RegraCategorizacaoServiceTests()
    {
        _categoriaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Aplicações Financeiras", Tipo = "Investimento" },
        });
        _sut = new RegraCategorizacaoService(
            _regraRepoMock.Object, _contaRepoMock.Object, _registroRepoMock.Object, _categoriaRepoMock.Object, _transferenciaServiceMock.Object);
    }

    private static ContaBancaria CriarConta(Guid clienteId, bool ativa = true) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Conta Teste", Tipo = "ContaCorrente",
        Ativa = ativa, DataCriacao = DateTime.UtcNow,
    };

    private void ConfigurarContaAsync(ContaBancaria conta) =>
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(conta.Id)).ReturnsAsync(conta);

    [Fact]
    public async Task CriarAsync_DescricaoComCnpj_UsaDocumentoComoCriterio()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        ConfigurarContaAsync(conta);
        _regraRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegraCategorizacao>());
        RegraCategorizacao? salva = null;
        _regraRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegraCategorizacao>()))
            .Callback<RegraCategorizacao>(r => salva = r)
            .ReturnsAsync((RegraCategorizacao r) => r);

        var dto = new CriarRegraDto
        {
            ContaBancariaId = conta.Id, Tipo = "Saida", AcaoTipo = "Categoria", Categoria = "Serviços",
            DescricaoReferencia = "Transferência enviada pelo Pix - AUTOPASS S.A. - 07.140.538/0001-40 - BCO BRADESCO (0237)",
        };

        await _sut.CriarAsync(clienteId, dto, clienteId, "cliente");

        Assert.NotNull(salva);
        Assert.Equal("Documento", salva!.CriterioTipo);
        Assert.Equal("DOC:07140538000140", salva.CriterioValor);
        Assert.Equal(0, salva.Ordem);
    }

    [Fact]
    public async Task CriarAsync_DescricaoSemCnpj_UsaDescricaoExata()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        ConfigurarContaAsync(conta);
        _regraRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<RegraCategorizacao> { new() { Ordem = 0 }, new() { Ordem = 3 } });
        RegraCategorizacao? salva = null;
        _regraRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegraCategorizacao>()))
            .Callback<RegraCategorizacao>(r => salva = r)
            .ReturnsAsync((RegraCategorizacao r) => r);

        var dto = new CriarRegraDto { ContaBancariaId = conta.Id, Tipo = "Saida", AcaoTipo = "Categoria", Categoria = "Investimentos", DescricaoReferencia = "aplicação rdb" };
        await _sut.CriarAsync(clienteId, dto, clienteId, "cliente");

        Assert.Equal("DescricaoExata", salva!.CriterioTipo);
        Assert.Equal("APLICAÇÃO RDB", salva.CriterioValor);
        // Nova regra entra no fim da lista (maior ordem existente + 1) — precedência manual.
        Assert.Equal(4, salva.Ordem);
    }

    [Fact]
    public async Task CriarAsync_AcaoCategoriaSemCategoriaInformada_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        ConfigurarContaAsync(conta);

        var dto = new CriarRegraDto { ContaBancariaId = conta.Id, Tipo = "Saida", AcaoTipo = "Categoria", DescricaoReferencia = "x" };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(clienteId, dto, clienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CriarAsync_AcaoTransferenciaSemContrapartida_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        ConfigurarContaAsync(conta);

        var dto = new CriarRegraDto { ContaBancariaId = conta.Id, Tipo = "Saida", AcaoTipo = "Transferencia", DescricaoReferencia = "Aplicação RDB" };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(clienteId, dto, clienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CriarAsync_ContrapartidaIgualContaDaRegra_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        ConfigurarContaAsync(conta);

        var dto = new CriarRegraDto
        {
            ContaBancariaId = conta.Id, Tipo = "Saida", AcaoTipo = "Transferencia",
            ContaContrapartidaId = conta.Id, DescricaoReferencia = "Aplicação RDB",
        };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(clienteId, dto, clienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CriarAsync_ClienteAcessandoContaDeOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(Guid.NewGuid()); // conta de outro cliente
        ConfigurarContaAsync(conta);

        var dto = new CriarRegraDto { ContaBancariaId = conta.Id, Tipo = "Saida", AcaoTipo = "Categoria", Categoria = "X", DescricaoReferencia = "y" };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(clienteId, dto, clienteId, "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ListarAsync_ContaQuantidadeAplicadaPelosItensComRegraCategorizacaoId()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        var regra = new RegraCategorizacao
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Tipo = "Saida",
            CriterioTipo = "DescricaoExata", CriterioValor = "APLICAÇÃO RDB", DescricaoReferencia = "Aplicação RDB",
            AcaoTipo = "Categoria", Categoria = "Investimentos", ContaBancaria = conta,
        };
        _regraRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegraCategorizacao> { regra });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 1, 1),
                Entradas = new(), ContasReceber = new(), ContasPagar = new(),
                Saidas = new()
                {
                    new() { Id = Guid.NewGuid(), Descricao = "Aplicação RDB", Valor = 100, RegraCategorizacaoId = regra.Id },
                    new() { Id = Guid.NewGuid(), Descricao = "Aplicação RDB", Valor = 200, RegraCategorizacaoId = regra.Id },
                    new() { Id = Guid.NewGuid(), Descricao = "Outra coisa", Valor = 50 },
                },
            },
        });

        var resultado = await _sut.ListarAsync(clienteId, clienteId, "cliente");

        Assert.Equal(2, Assert.Single(resultado).QuantidadeAplicada);
    }

    [Fact]
    public async Task ContarCorrespondenciasAsync_SoContaItensPendentesQueCasamComOCriterio()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        ConfigurarContaAsync(conta);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 1, 1),
                Entradas = new(), ContasReceber = new(), ContasPagar = new(),
                Saidas = new()
                {
                    new() { Id = Guid.NewGuid(), Descricao = "Aplicação RDB", Valor = 100, PendenteCategorizacao = true },
                    new() { Id = Guid.NewGuid(), Descricao = "Aplicação RDB", Valor = 200, PendenteCategorizacao = false }, // já categorizado — não conta
                    new() { Id = Guid.NewGuid(), Descricao = "Resgate RDB", Valor = 50, PendenteCategorizacao = true }, // não casa
                },
            },
        });

        var quantidade = await _sut.ContarCorrespondenciasAsync(conta.Id, "Saida", "Aplicação RDB", clienteId, "cliente");

        Assert.Equal(1, quantidade);
    }

    [Fact]
    public async Task AplicarRetroativamenteAsync_AcaoCategoria_CategorizaSoPendentesQueCasamENuncaOsJaCategorizados()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        var regra = new RegraCategorizacao
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Tipo = "Saida",
            CriterioTipo = "DescricaoExata", CriterioValor = "APLICAÇÃO RDB", DescricaoReferencia = "Aplicação RDB",
            AcaoTipo = "Categoria", Categoria = "Aplicações Financeiras", Ativa = true, ContaBancaria = conta,
        };
        _regraRepoMock.Setup(r => r.ObterPorIdAsync(regra.Id)).ReturnsAsync(regra);

        var itemPendenteQueCasa = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Aplicação RDB", Valor = 100, PendenteCategorizacao = true };
        var itemJaCategorizado = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Aplicação RDB", Valor = 200, Categoria = "Outra", PendenteCategorizacao = false };
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 1, 1),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { itemPendenteQueCasa, itemJaCategorizado },
        };
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { registro });
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(conta.Id, registro.Data)).ReturnsAsync(registro);

        var resultado = await _sut.AplicarRetroativamenteAsync(regra.Id, clienteId, "cliente");

        Assert.Equal(1, resultado.TotalCategorizados);
        Assert.False(itemPendenteQueCasa.PendenteCategorizacao);
        Assert.Equal("Aplicações Financeiras", itemPendenteQueCasa.Categoria);
        Assert.Equal(regra.Id, itemPendenteQueCasa.RegraCategorizacaoId);
        // Item já categorizado manualmente antes não é tocado pela regra.
        Assert.Equal("Outra", itemJaCategorizado.Categoria);
        Assert.Null(itemJaCategorizado.RegraCategorizacaoId);
    }

    [Fact]
    public async Task AplicarRetroativamenteAsync_RegraInativa_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var regra = new RegraCategorizacao { Id = Guid.NewGuid(), ClienteId = clienteId, Ativa = false };
        _regraRepoMock.Setup(r => r.ObterPorIdAsync(regra.Id)).ReturnsAsync(regra);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AplicarRetroativamenteAsync(regra.Id, clienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ReordenarAsync_AtualizaOrdemConformeSequenciaDeIds()
    {
        var clienteId = Guid.NewGuid();
        var r1 = new RegraCategorizacao { Id = Guid.NewGuid(), ClienteId = clienteId, Ordem = 0 };
        var r2 = new RegraCategorizacao { Id = Guid.NewGuid(), ClienteId = clienteId, Ordem = 1 };
        _regraRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegraCategorizacao> { r1, r2 });

        await _sut.ReordenarAsync(clienteId, new List<Guid> { r2.Id, r1.Id }, clienteId, "cliente");

        Assert.Equal(0, r2.Ordem);
        Assert.Equal(1, r1.Ordem);
    }

    [Fact]
    public async Task DesativarAsync_MarcaRegraComoInativa()
    {
        var clienteId = Guid.NewGuid();
        var regra = new RegraCategorizacao { Id = Guid.NewGuid(), ClienteId = clienteId, Ativa = true };
        _regraRepoMock.Setup(r => r.ObterPorIdAsync(regra.Id)).ReturnsAsync(regra);

        await _sut.DesativarAsync(regra.Id, clienteId, "cliente");

        Assert.False(regra.Ativa);
        _regraRepoMock.Verify(r => r.AtualizarAsync(regra), Times.Once);
    }

    [Fact]
    public async Task ExcluirAsync_ClienteAcessandoRegraDeOutroCliente_LancaAcessoNegado()
    {
        var regra = new RegraCategorizacao { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid() };
        _regraRepoMock.Setup(r => r.ObterPorIdAsync(regra.Id)).ReturnsAsync(regra);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ExcluirAsync(regra.Id, Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }
}
