using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class CategoriaServiceTests
{
    private readonly Mock<ICategoriaRepository> _repoMock = new();
    private readonly Mock<IGrupoRepository> _grupoRepoMock = new();
    private readonly CategoriaService _sut;
    private readonly Guid _clienteId = Guid.NewGuid();

    public CategoriaServiceTests()
    {
        _repoMock.Setup(r => r.ListarTodasPorClienteAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Categoria>());
        _sut = new CategoriaService(_repoMock.Object, _grupoRepoMock.Object);
    }

    private static Grupo Grupo(string nome, string bloco) => new() { Id = Guid.NewGuid(), Nome = nome, Bloco = bloco, Ativo = true };

    [Fact]
    public async Task CriarAsync_DerivaTipoDoBlocoDoGrupo_NaoDoInputDoUsuario()
    {
        var grupo = Grupo("Imobilizado", Blocos.AtividadesDeInvestimento);
        _grupoRepoMock.Setup(r => r.ObterPorIdAsync(grupo.Id)).ReturnsAsync(grupo);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Categoria>())).ReturnsAsync((Categoria c) => c);

        var criada = await _sut.CriarAsync(_clienteId, new CriarCategoriaDto { Nome = "Equipamentos", GrupoId = grupo.Id }, _clienteId, "cliente");

        Assert.Equal("Investimento", criada.Tipo);
        Assert.Equal(grupo.Id, criada.GrupoId);
        Assert.Equal(Blocos.AtividadesDeInvestimento, criada.Bloco);
    }

    [Fact]
    public async Task CriarAsync_ComGrupoInexistente_LancaNaoEncontrado()
    {
        var grupoId = Guid.NewGuid();
        _grupoRepoMock.Setup(r => r.ObterPorIdAsync(grupoId)).ReturnsAsync((Grupo?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(_clienteId, new CriarCategoriaDto { Nome = "X", GrupoId = grupoId }, _clienteId, "cliente"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task CriarAsync_ComNomeDuplicadoNoMesmoCliente_LancaConflito()
    {
        var grupo = Grupo("Custos Diretos", Blocos.CustosOperacionais);
        _grupoRepoMock.Setup(r => r.ObterPorIdAsync(grupo.Id)).ReturnsAsync(grupo);
        _repoMock.Setup(r => r.ObterPorNomeAsync(_clienteId, "Insumos"))
            .ReturnsAsync(new Categoria { Id = Guid.NewGuid(), Nome = "Insumos", GrupoId = grupo.Id, Grupo = grupo, ClienteId = _clienteId });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(_clienteId, new CriarCategoriaDto { Nome = "Insumos", GrupoId = grupo.Id }, _clienteId, "cliente"));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CriarAsync_ComUsuarioDeOutroCliente_LancaAcessoNegado()
    {
        var outroClienteId = Guid.NewGuid();
        var grupo = Grupo("Custos Diretos", Blocos.CustosOperacionais);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(_clienteId, new CriarCategoriaDto { Nome = "X", GrupoId = grupo.Id }, outroClienteId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task AtualizarAsync_MudaDeGrupo_AtualizaTipoJunto()
    {
        var grupoAntigo = Grupo("Custos Diretos", Blocos.CustosOperacionais);
        var grupoNovo = Grupo("Despesas com Marketing", Blocos.DespesasOperacionais);
        var categoria = new Categoria
        {
            Id = Guid.NewGuid(), Nome = "Publicidade", Tipo = "CustoVariavel",
            GrupoId = grupoAntigo.Id, Grupo = grupoAntigo, Ativa = true, ClienteId = _clienteId,
        };
        _repoMock.Setup(r => r.ObterPorIdAsync(categoria.Id)).ReturnsAsync(categoria);
        _grupoRepoMock.Setup(r => r.ObterPorIdAsync(grupoNovo.Id)).ReturnsAsync(grupoNovo);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Categoria>())).ReturnsAsync((Categoria c) => c);

        var atualizada = await _sut.AtualizarAsync(
            _clienteId, categoria.Id, new AtualizarCategoriaDto { Nome = "Publicidade", GrupoId = grupoNovo.Id, Ativa = true }, _clienteId, "cliente");

        Assert.Equal("CustoFixo", atualizada.Tipo);
        Assert.Equal(grupoNovo.Id, atualizada.GrupoId);
    }

    [Fact]
    public async Task AtualizarAsync_CategoriaDeOutroCliente_LancaAcessoNegado()
    {
        var outroClienteId = Guid.NewGuid();
        var grupo = Grupo("Custos Diretos", Blocos.CustosOperacionais);
        var categoria = new Categoria { Id = Guid.NewGuid(), Nome = "Publicidade", GrupoId = grupo.Id, Grupo = grupo, ClienteId = outroClienteId };
        _repoMock.Setup(r => r.ObterPorIdAsync(categoria.Id)).ReturnsAsync(categoria);
        _grupoRepoMock.Setup(r => r.ObterPorIdAsync(grupo.Id)).ReturnsAsync(grupo);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(_clienteId, categoria.Id, new AtualizarCategoriaDto { Nome = "Publicidade", GrupoId = grupo.Id, Ativa = true }, _clienteId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ListarAgrupadasAsync_InvestimentoEFinanciamento_AparecemNosDoisLados()
    {
        var grupoInvest = Grupo("Imobilizado", Blocos.AtividadesDeInvestimento);
        var grupoReceita = Grupo("Vendas e Serviços", Blocos.ReceitasOperacionais);
        var grupoDespesa = Grupo("Despesas com Ocupação", Blocos.DespesasOperacionais);
        var ativas = new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Vendas", Tipo = "Receita", Grupo = grupoReceita, Ativa = true, ClienteId = _clienteId },
            new() { Id = Guid.NewGuid(), Nome = "Aluguel", Tipo = "CustoFixo", Grupo = grupoDespesa, Ativa = true, ClienteId = _clienteId },
            new() { Id = Guid.NewGuid(), Nome = "Equipamentos", Tipo = "Investimento", Grupo = grupoInvest, Ativa = true, ClienteId = _clienteId },
        };
        _repoMock.Setup(r => r.ListarAtivasPorClienteAsync(_clienteId)).ReturnsAsync(ativas);

        var agrupadas = await _sut.ListarAgrupadasAsync(_clienteId);

        Assert.Contains(agrupadas.Entradas, c => c.Nome == "Vendas");
        Assert.Contains(agrupadas.Entradas, c => c.Nome == "Equipamentos");
        Assert.DoesNotContain(agrupadas.Entradas, c => c.Nome == "Aluguel");

        Assert.Contains(agrupadas.Saidas, c => c.Nome == "Aluguel");
        Assert.Contains(agrupadas.Saidas, c => c.Nome == "Equipamentos");
        Assert.DoesNotContain(agrupadas.Saidas, c => c.Nome == "Vendas");
    }

    [Fact]
    public async Task ListarAgrupadasAsync_DeducoesDaReceita_AparecemNosDoisLadosENaoSeConfundemComDespesa()
    {
        // Deduções da Receita e Despesas Operacionais mapeiam pro mesmo Tipo ("CustoFixo" — ver
        // Blocos.TipoPadrao), então a distinção entrada/saída não pode depender só do Tipo aqui.
        var grupoDevolucao = Grupo("Devolução e Estorno", Blocos.DeducoesDaReceita);
        var grupoDespesa = Grupo("Despesas com Ocupação", Blocos.DespesasOperacionais);
        var ativas = new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Devoluções", Tipo = "CustoFixo", Grupo = grupoDevolucao, Ativa = true, ClienteId = _clienteId },
            new() { Id = Guid.NewGuid(), Nome = "Aluguel", Tipo = "CustoFixo", Grupo = grupoDespesa, Ativa = true, ClienteId = _clienteId },
        };
        _repoMock.Setup(r => r.ListarAtivasPorClienteAsync(_clienteId)).ReturnsAsync(ativas);

        var agrupadas = await _sut.ListarAgrupadasAsync(_clienteId);

        Assert.Contains(agrupadas.Entradas, c => c.Nome == "Devoluções");
        Assert.DoesNotContain(agrupadas.Entradas, c => c.Nome == "Aluguel");

        Assert.Contains(agrupadas.Saidas, c => c.Nome == "Devoluções");
        Assert.Contains(agrupadas.Saidas, c => c.Nome == "Aluguel");
    }

    [Fact]
    public async Task ListarParaGerenciarAsync_PreencheEhEntradaEEhSaidaPorCategoria()
    {
        var grupoReceita = Grupo("Vendas e Serviços", Blocos.ReceitasOperacionais);
        var grupoDevolucao = Grupo("Devolução e Estorno", Blocos.DeducoesDaReceita);
        var grupoDespesa = Grupo("Despesas com Ocupação", Blocos.DespesasOperacionais);
        var grupoInvest = Grupo("Imobilizado", Blocos.AtividadesDeInvestimento);
        var todas = new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nome = "Vendas", Tipo = "Receita", Grupo = grupoReceita, Ativa = true, ClienteId = _clienteId },
            new() { Id = Guid.NewGuid(), Nome = "Devoluções", Tipo = "CustoFixo", Grupo = grupoDevolucao, Ativa = true, ClienteId = _clienteId },
            new() { Id = Guid.NewGuid(), Nome = "Aluguel", Tipo = "CustoFixo", Grupo = grupoDespesa, Ativa = true, ClienteId = _clienteId },
            new() { Id = Guid.NewGuid(), Nome = "Equipamentos", Tipo = "Investimento", Grupo = grupoInvest, Ativa = true, ClienteId = _clienteId },
        };
        _repoMock.Setup(r => r.ListarTodasPorClienteAsync(_clienteId)).ReturnsAsync(todas);

        var resultado = await _sut.ListarParaGerenciarAsync(_clienteId, _clienteId, "cliente");

        var vendas = resultado.Single(c => c.Nome == "Vendas");
        Assert.True(vendas.EhEntrada);
        Assert.False(vendas.EhSaida);

        var devolucoes = resultado.Single(c => c.Nome == "Devoluções");
        Assert.True(devolucoes.EhEntrada);
        Assert.True(devolucoes.EhSaida);

        var aluguel = resultado.Single(c => c.Nome == "Aluguel");
        Assert.False(aluguel.EhEntrada);
        Assert.True(aluguel.EhSaida);

        var equipamentos = resultado.Single(c => c.Nome == "Equipamentos");
        Assert.True(equipamentos.EhEntrada);
        Assert.True(equipamentos.EhSaida);
    }

    [Fact]
    public async Task ListarParaGerenciarAsync_ComUsuarioDeOutroCliente_LancaAcessoNegado()
    {
        var outroClienteId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ListarParaGerenciarAsync(_clienteId, outroClienteId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }
}
