using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class PrevistoRealizadoServiceTests
{
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IContaRecorrenteRepository> _contaRecorrenteRepoMock = new();
    private readonly PrevistoRealizadoService _sut;
    private static readonly DateOnly Hoje = DataLocalHelper.Hoje();
    private static readonly DateOnly MesAtual = new(Hoje.Year, Hoje.Month, 1);

    public PrevistoRealizadoServiceTests()
    {
        _contaRecorrenteRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaRecorrente>());
        _sut = new PrevistoRealizadoService(_registroRepoMock.Object, _contaRecorrenteRepoMock.Object);
    }

    private static RegistroDiario CriarRegistro(Guid clienteId) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Data = MesAtual,
        Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
    };

    private void ConfigurarRegistros(Guid clienteId, List<RegistroDiario> registros) =>
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);

    [Fact]
    public async Task ObterAsync_ClienteTentandoVerDeOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var outroUsuario = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterAsync(clienteId, 6, outroUsuario, "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ObterAsync_TituloSemRecorrencia_NaoEntraNoRelatorio()
    {
        var clienteId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada { Descricao = "Avulso", Valor = 100m, DataVencimento = MesAtual, Categoria = "Diversos" });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 3, clienteId, "cliente");

        Assert.Empty(resultado.Linhas);
    }

    [Fact]
    public async Task ObterAsync_TituloAvulsoPagoPorVinculo_NaoEntraPorPadrao()
    {
        var clienteId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Avulso vinculado", Valor = 100m, ValorRealizado = 100m, Pago = true,
            DataVencimento = MesAtual, Categoria = "Diversos", LancamentoVinculadoId = Guid.NewGuid(),
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 3, clienteId, "cliente");

        Assert.Empty(resultado.Linhas);
    }

    [Fact]
    public async Task ObterAsync_TituloAvulsoPagoPorVinculo_EntraQuandoIncluirAvulsosVinculados()
    {
        var clienteId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Avulso vinculado", Valor = 100m, ValorRealizado = 100m, Pago = true,
            DataVencimento = MesAtual, Categoria = "Diversos", LancamentoVinculadoId = Guid.NewGuid(),
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 3, clienteId, "cliente", incluirAvulsosVinculados: true);

        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal("Diversos", linha.Categoria);
        var ponto = Assert.Single(linha.Meses);
        Assert.Equal(100m, ponto.Previsto);
        Assert.Equal(100m, ponto.Realizado);
    }

    [Fact]
    public async Task ObterAsync_TituloAvulsoSemVinculo_NaoEntraMesmoComIncluirAvulsosVinculados()
    {
        var clienteId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Avulso solto", Valor = 100m, DataVencimento = MesAtual, Categoria = "Diversos",
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 3, clienteId, "cliente", incluirAvulsosVinculados: true);

        Assert.Empty(resultado.Linhas);
    }

    [Fact]
    public async Task ObterAsync_TituloRecorrentePagoComValorRealizadoDiferente_UsaValorRealizadoComoRealizado()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Energia", Valor = 100m, ValorRealizado = 130m, Pago = true,
            DataVencimento = MesAtual, Categoria = "Utilidades", RecorrenciaId = recorrenciaId,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 1, clienteId, "cliente");

        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal("Pagar", linha.Tipo);
        Assert.Equal("Utilidades", linha.Categoria);
        var ponto = Assert.Single(linha.Meses);
        Assert.Equal(100m, ponto.Previsto);
        Assert.Equal(130m, ponto.Realizado);
        Assert.NotNull(ponto.VariacaoPercentual);
        Assert.Equal(0.30m, ponto.VariacaoPercentual!.Value, 2);
        Assert.True(ponto.VariacaoAlta);
    }

    [Fact]
    public async Task ObterAsync_TituloRecorrentePendente_RealizadoFicaZeroENaoContaComoVariacao()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasReceber.Add(new ContaProvisionada
        {
            Descricao = "Mensalidade", Valor = 200m, Pago = false,
            DataVencimento = MesAtual.AddDays(20), Categoria = "Receita", RecorrenciaId = recorrenciaId,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 1, clienteId, "cliente");

        var ponto = Assert.Single(Assert.Single(resultado.Linhas).Meses);
        Assert.Equal(200m, ponto.Previsto);
        Assert.Equal(0m, ponto.Realizado);
        Assert.True(ponto.VariacaoAlta);
    }

    [Fact]
    public async Task ObterAsync_RealizadoSobeTresMesesSeguidos_MarcaSoOTerceiroMes()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);

        void AdicionarPago(int mesesAtras, decimal valor)
        {
            var dataVenc = MesAtual.AddMonths(-mesesAtras);
            registro.ContasPagar.Add(new ContaProvisionada
            {
                Descricao = "Fornecedor", Valor = valor, ValorRealizado = valor, Pago = true,
                DataVencimento = dataVenc, Categoria = "Insumos", RecorrenciaId = recorrenciaId,
            });
        }

        AdicionarPago(3, 100m);
        AdicionarPago(2, 150m);
        AdicionarPago(1, 200m);
        AdicionarPago(0, 300m);
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 4, clienteId, "cliente");

        var pontos = Assert.Single(resultado.Linhas).Meses;
        Assert.Equal(4, pontos.Count);
        Assert.False(pontos[0].SubiuTresMesesSeguidos);
        Assert.False(pontos[1].SubiuTresMesesSeguidos);
        Assert.True(pontos[2].SubiuTresMesesSeguidos);
        Assert.True(pontos[3].SubiuTresMesesSeguidos);
    }

    [Fact]
    public async Task ObterAsync_MesesForaDoClamp_EhAjustadoParaOLimite()
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRegistros(clienteId, new List<RegistroDiario>());

        var resultado = await _sut.ObterAsync(clienteId, 0, clienteId, "cliente");

        Assert.NotNull(resultado);
        Assert.Empty(resultado.Linhas);
    }

    [Fact]
    public async Task ObterAsync_MesSemPrevistoNemRealizado_NaoApareceNaLista()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        // Só um título, no mês mais recente — os outros 2 meses da janela não têm nada.
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Aluguel", Valor = 100m, DataVencimento = MesAtual, Categoria = "Aluguel", RecorrenciaId = recorrenciaId,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 3, clienteId, "cliente");

        var ponto = Assert.Single(Assert.Single(resultado.Linhas).Meses);
        Assert.Equal($"{MesAtual.Year:D4}-{MesAtual.Month:D2}", ponto.Mes);
    }

    [Fact]
    public async Task ObterAsync_TituloSemCategoriaPropria_UsaCategoriaDaContaRecorrente()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Aluguel", Valor = 100m, DataVencimento = MesAtual, Categoria = null, RecorrenciaId = recorrenciaId,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });
        _contaRecorrenteRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaRecorrente>
        {
            new() { Id = recorrenciaId, ClienteId = clienteId, Descricao = "Aluguel", Valor = 100m, Categoria = "Aluguel", ContaBancariaId = Guid.NewGuid(), CriadoEm = DateTime.UtcNow },
        });

        var resultado = await _sut.ObterAsync(clienteId, 1, clienteId, "cliente");

        Assert.Equal("Aluguel", Assert.Single(resultado.Linhas).Categoria);
    }

    [Fact]
    public async Task ObterAsync_SemCategoriaPropriaENaRecorrencia_CaiEmSemCategoria()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Aluguel", Valor = 100m, DataVencimento = MesAtual, Categoria = null, RecorrenciaId = recorrenciaId,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });
        _contaRecorrenteRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaRecorrente>
        {
            new() { Id = recorrenciaId, ClienteId = clienteId, Descricao = "Aluguel", Valor = 100m, Categoria = null, ContaBancariaId = Guid.NewGuid(), CriadoEm = DateTime.UtcNow },
        });

        var resultado = await _sut.ObterAsync(clienteId, 1, clienteId, "cliente");

        Assert.Equal("Sem categoria", Assert.Single(resultado.Linhas).Categoria);
    }
}
