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

        Assert.Empty(resultado.Itens);
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

        Assert.Empty(resultado.Itens);
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

        var item = Assert.Single(resultado.Itens);
        Assert.Equal("Avulso vinculado", item.Nome);
        Assert.Equal("Diversos", item.Categoria);
        Assert.Equal(100m, item.Previsto);
        Assert.Equal(100m, item.Realizado);
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

        Assert.Empty(resultado.Itens);
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

        var item = Assert.Single(resultado.Itens);
        Assert.Equal("Pagar", item.Tipo);
        Assert.Equal("Utilidades", item.Categoria);
        Assert.Equal(100m, item.Previsto);
        Assert.Equal(130m, item.Realizado);
        Assert.Equal(30m, item.Diferenca);
        Assert.NotNull(item.DiferencaPercentual);
        Assert.Equal(0.30m, item.DiferencaPercentual!.Value, 2);
        // Pago, mas com diferença maior que a tolerância — "Parcial", não "Realizado".
        Assert.Equal("Parcial", item.Status);
    }

    [Fact]
    public async Task ObterAsync_TituloPagoComValorIgualAoPrevisto_StatusRealizado()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Aluguel", Valor = 1000m, ValorRealizado = 1000m, Pago = true,
            DataVencimento = MesAtual, Categoria = "Aluguel", RecorrenciaId = recorrenciaId,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var item = Assert.Single((await _sut.ObterAsync(clienteId, 1, clienteId, "cliente")).Itens);

        Assert.Equal("Realizado", item.Status);
    }

    [Fact]
    public async Task ObterAsync_TituloPendenteVencido_StatusAtrasado()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fornecedor", Valor = 500m, Pago = false,
            DataVencimento = Hoje.AddDays(-3), Categoria = "Insumos", RecorrenciaId = recorrenciaId,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var item = Assert.Single((await _sut.ObterAsync(clienteId, 1, clienteId, "cliente")).Itens);

        Assert.Equal("Atrasado", item.Status);
    }

    [Fact]
    public async Task ObterAsync_TituloPendenteAindaDentroDoPrazo_StatusNaoAconteceu()
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

        var item = Assert.Single((await _sut.ObterAsync(clienteId, 1, clienteId, "cliente")).Itens);

        Assert.Equal(200m, item.Previsto);
        Assert.Equal(0m, item.Realizado);
        Assert.Equal("NaoAconteceu", item.Status);
    }

    [Fact]
    public async Task ObterAsync_MesesForaDoClamp_EhAjustadoParaOLimite()
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRegistros(clienteId, new List<RegistroDiario>());

        var resultado = await _sut.ObterAsync(clienteId, 0, clienteId, "cliente");

        Assert.NotNull(resultado);
        Assert.Empty(resultado.Itens);
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

        Assert.Equal("Aluguel", Assert.Single(resultado.Itens).Categoria);
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

        Assert.Equal("Sem categoria", Assert.Single(resultado.Itens).Categoria);
    }

    // Item 7, ponto 1: por FAVORECIDO, não por categoria — dois favorecidos diferentes na mesma
    // categoria viram duas linhas separadas, cada um com seu próprio previsto/realizado.
    [Fact]
    public async Task ObterAsync_DoisFavorecidosNaMesmaCategoria_ViramItensSeparados()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId1 = Guid.NewGuid();
        var recorrenciaId2 = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fulano de Tal", Valor = 1800m, Pago = false,
            DataVencimento = MesAtual.AddDays(4), Categoria = "Salários/Folha", RecorrenciaId = recorrenciaId1,
        });
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Ciclana Souza", Valor = 2200m, Pago = false,
            DataVencimento = MesAtual.AddDays(4), Categoria = "Salários/Folha", RecorrenciaId = recorrenciaId2,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 1, clienteId, "cliente");

        Assert.Equal(2, resultado.Itens.Count);
        Assert.Contains(resultado.Itens, i => i.Nome == "Fulano de Tal" && i.Previsto == 1800m);
        Assert.Contains(resultado.Itens, i => i.Nome == "Ciclana Souza" && i.Previsto == 2200m);
        Assert.All(resultado.Itens, i => Assert.Equal("Salários/Folha", i.Categoria));
    }

    // Item 7, ponto 3: acumulado dia a dia quando meses=1 ("mês atual") — o furo fica visível ao
    // longo dos dias, não só no total do mês.
    [Fact]
    public async Task ObterAsync_ComMesesIgualA1_AcumuladoEhDiaADia()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        var diaVencimento = new DateOnly(MesAtual.Year, MesAtual.Month, 5);
        registro.ContasReceber.Add(new ContaProvisionada
        {
            Descricao = "Pollye", Valor = 600m, ValorRealizado = 500m, Pago = true, DataBaixa = diaVencimento,
            DataVencimento = diaVencimento, Categoria = "Vendas", RecorrenciaId = recorrenciaId,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 1, clienteId, "cliente");

        Assert.Equal(DateTime.DaysInMonth(MesAtual.Year, MesAtual.Month), resultado.AcumuladoReceber.Count);
        var pontoAntes = resultado.AcumuladoReceber[2]; // dia 3 — antes do vencimento
        Assert.Equal(0m, pontoAntes.PrevistoAcumulado);
        Assert.Equal(0m, pontoAntes.RealizadoAcumulado);
        var pontoDepois = resultado.AcumuladoReceber[^1]; // último dia do mês
        Assert.Equal(600m, pontoDepois.PrevistoAcumulado);
        Assert.Equal(500m, pontoDepois.RealizadoAcumulado);
    }

    // Item 7, ponto 3: com janela de vários meses, o acumulado vira mês a mês (não dia a dia).
    [Fact]
    public async Task ObterAsync_ComMesesMaiorQue1_AcumuladoEhMesAMes()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var registro = CriarRegistro(clienteId);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Aluguel", Valor = 1000m, ValorRealizado = 1000m, Pago = true,
            DataVencimento = MesAtual, Categoria = "Aluguel", RecorrenciaId = recorrenciaId,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ObterAsync(clienteId, 3, clienteId, "cliente");

        Assert.Equal(3, resultado.AcumuladoPagar.Count);
        Assert.Equal($"{MesAtual.Year:D4}-{MesAtual.Month:D2}", resultado.AcumuladoPagar[^1].Data);
        Assert.Equal(1000m, resultado.AcumuladoPagar[^1].PrevistoAcumulado);
    }

    // ── ObterHistoricoItemAsync (ponto 5: "tocar num item mostra o histórico dele") ──────────────

    [Fact]
    public async Task ObterHistoricoItemAsync_ClienteTentandoVerDeOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var outroUsuario = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterHistoricoItemAsync(clienteId, "Pagar:X", 6, outroUsuario, "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ObterHistoricoItemAsync_ChaveVazia_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterHistoricoItemAsync(clienteId, "", 6, clienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ObterHistoricoItemAsync_RetornaSoOsMesesDesseFavorecido()
    {
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var outraRecorrenciaId = Guid.NewGuid();
        var registroAtual = CriarRegistro(clienteId);
        registroAtual.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fulano de Tal", Valor = 1800m, ValorRealizado = 1800m, Pago = true,
            DataVencimento = MesAtual, Categoria = "Salários/Folha", RecorrenciaId = recorrenciaId,
        });
        registroAtual.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Ciclana Souza", Valor = 2200m, Pago = false,
            DataVencimento = MesAtual.AddDays(4), Categoria = "Salários/Folha", RecorrenciaId = outraRecorrenciaId,
        });
        var registroMesPassado = CriarRegistro(clienteId);
        registroMesPassado.Data = MesAtual.AddMonths(-1);
        registroMesPassado.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fulano de Tal", Valor = 1800m, ValorRealizado = 1700m, Pago = true,
            DataVencimento = MesAtual.AddMonths(-1), Categoria = "Salários/Folha", RecorrenciaId = recorrenciaId,
        });
        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registroAtual, registroMesPassado });

        var resultado = await _sut.ObterHistoricoItemAsync(clienteId, "Pagar:FULANO DE TAL", 3, clienteId, "cliente");

        Assert.Equal("Fulano de Tal", resultado.Nome);
        Assert.Equal(2, resultado.Meses.Count); // só os 2 meses com título do Fulano, nunca a Ciclana
        Assert.All(resultado.Meses, m => Assert.Equal(1800m, m.Previsto));
    }
}
