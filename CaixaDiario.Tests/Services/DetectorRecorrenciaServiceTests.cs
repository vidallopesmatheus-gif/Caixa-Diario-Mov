using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class DetectorRecorrenciaServiceTests
{
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IContaRecorrenteRepository> _recorrenteRepoMock = new();
    private readonly Mock<ISugestaoRecorrenciaIgnoradaRepository> _ignoradaRepoMock = new();
    private readonly DetectorRecorrenciaService _sut;
    private static readonly DateOnly Hoje = DataLocalHelper.Hoje();

    public DetectorRecorrenciaServiceTests()
    {
        _sut = new DetectorRecorrenciaService(_registroRepoMock.Object, _recorrenteRepoMock.Object, _ignoradaRepoMock.Object);
        _recorrenteRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<ContaRecorrente>());
        _ignoradaRepoMock.Setup(r => r.ListarPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<SugestaoRecorrenciaIgnorada>());
    }

    private static RegistroDiario CriarRegistro(DateOnly data, Guid? contaBancariaId = null) => new()
    {
        Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = data, ContaBancariaId = contaBancariaId,
        Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
    };

    private void ConfigurarRegistros(Guid clienteId, List<RegistroDiario> registros) =>
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);

    [Fact]
    public async Task ListarSugestoesAsync_DescricaoRepetidaEm3MesesComValorParecido_Sugere()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>();
        for (var i = 0; i < 3; i++)
        {
            var reg = CriarRegistro(Hoje.AddMonths(-i));
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Internet Fibra", Categoria = "Energia/Água/Internet", Valor = 100m, TipoCusto = "CustoFixo" });
            registros.Add(reg);
        }
        ConfigurarRegistros(clienteId, registros);

        var resultado = await _sut.ListarSugestoesAsync(clienteId, clienteId, "cliente");

        var sugestao = Assert.Single(resultado);
        Assert.Equal("Pagar", sugestao.Tipo);
        Assert.Equal("Internet Fibra", sugestao.Descricao);
        Assert.Equal("Energia/Água/Internet", sugestao.Categoria);
        Assert.Equal(100m, sugestao.ValorMedio);
        Assert.Equal(3, sugestao.QuantidadeOcorrencias);
        Assert.Equal("Pagar:INTERNET FIBRA", sugestao.Chave);
    }

    [Fact]
    public async Task ListarSugestoesAsync_EntradaRepetidaEm3Meses_DetectaComoReceber()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>();
        for (var i = 0; i < 3; i++)
        {
            var reg = CriarRegistro(Hoje.AddMonths(-i));
            reg.Entradas.Add(new ItemFinanceiro { Descricao = "Cliente Mensalista", Categoria = "Vendas", Valor = 500m, TipoCusto = "Receita" });
            registros.Add(reg);
        }
        ConfigurarRegistros(clienteId, registros);

        var resultado = await _sut.ListarSugestoesAsync(clienteId, clienteId, "cliente");

        var sugestao = Assert.Single(resultado);
        Assert.Equal("Receber", sugestao.Tipo);
    }

    [Fact]
    public async Task ListarSugestoesAsync_ComApenas2MesesDistintos_NaoSugere()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>();
        for (var i = 0; i < 2; i++)
        {
            var reg = CriarRegistro(Hoje.AddMonths(-i));
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Internet Fibra", Categoria = "Energia/Água/Internet", Valor = 100m, TipoCusto = "CustoFixo" });
            registros.Add(reg);
        }
        ConfigurarRegistros(clienteId, registros);

        var resultado = await _sut.ListarSugestoesAsync(clienteId, clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSugestoesAsync_ComValoresMuitoDiferentes_NaoSugere()
    {
        var clienteId = Guid.NewGuid();
        var valores = new[] { 100m, 100m, 1000m };
        var registros = new List<RegistroDiario>();
        for (var i = 0; i < 3; i++)
        {
            var reg = CriarRegistro(Hoje.AddMonths(-i));
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Fornecedor X", Categoria = "Insumos/Mercadoria", Valor = valores[i], TipoCusto = "CustoFixo" });
            registros.Add(reg);
        }
        ConfigurarRegistros(clienteId, registros);

        var resultado = await _sut.ListarSugestoesAsync(clienteId, clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSugestoesAsync_SemCategoria_NaoEntraComoCandidato()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>();
        for (var i = 0; i < 3; i++)
        {
            var reg = CriarRegistro(Hoje.AddMonths(-i));
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Sem categoria", Categoria = "", Valor = 100m, TipoCusto = "CustoFixo" });
            registros.Add(reg);
        }
        ConfigurarRegistros(clienteId, registros);

        var resultado = await _sut.ListarSugestoesAsync(clienteId, clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSugestoesAsync_ComRecorrenciaJaCadastrada_NaoSugereDeNovo()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>();
        for (var i = 0; i < 3; i++)
        {
            var reg = CriarRegistro(Hoje.AddMonths(-i));
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Internet Fibra", Categoria = "Energia/Água/Internet", Valor = 100m, TipoCusto = "CustoFixo" });
            registros.Add(reg);
        }
        ConfigurarRegistros(clienteId, registros);
        _recorrenteRepoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, Descricao = "internet fibra", Tipo = "Pagar", Valor = 100m, Ativo = true, DataInicio = Hoje },
        });

        var resultado = await _sut.ListarSugestoesAsync(clienteId, clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSugestoesAsync_ComChaveJaIgnorada_NaoSugereDeNovo()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>();
        for (var i = 0; i < 3; i++)
        {
            var reg = CriarRegistro(Hoje.AddMonths(-i));
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Internet Fibra", Categoria = "Energia/Água/Internet", Valor = 100m, TipoCusto = "CustoFixo" });
            registros.Add(reg);
        }
        ConfigurarRegistros(clienteId, registros);
        _ignoradaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<SugestaoRecorrenciaIgnorada>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, ChaveNormalizada = "Pagar:INTERNET FIBRA", CriadoEm = DateTime.UtcNow },
        });

        var resultado = await _sut.ListarSugestoesAsync(clienteId, clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSugestoesAsync_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ListarSugestoesAsync(Guid.NewGuid(), Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task IgnorarAsync_PersisteAChaveNormalizada()
    {
        var clienteId = Guid.NewGuid();
        SugestaoRecorrenciaIgnorada? persistida = null;
        _ignoradaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<SugestaoRecorrenciaIgnorada>()))
            .Callback<SugestaoRecorrenciaIgnorada>(i => persistida = i)
            .Returns(Task.CompletedTask);

        await _sut.IgnorarAsync(clienteId, "Pagar:INTERNET FIBRA", clienteId, "cliente");

        Assert.NotNull(persistida);
        Assert.Equal(clienteId, persistida!.ClienteId);
        Assert.Equal("Pagar:INTERNET FIBRA", persistida.ChaveNormalizada);
    }

    [Fact]
    public async Task IgnorarAsync_ChaveVazia_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.IgnorarAsync(clienteId, "", clienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task IgnorarAsync_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.IgnorarAsync(Guid.NewGuid(), "Pagar:X", Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }
}
