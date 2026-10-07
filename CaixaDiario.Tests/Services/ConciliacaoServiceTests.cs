using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ConciliacaoServiceTests
{
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly ConciliacaoService _sut;

    public ConciliacaoServiceTests()
    {
        _sut = new ConciliacaoService(_registroRepoMock.Object);
    }

    private static RegistroDiario CriarRegistro(Guid clienteId, Guid contaId, DateOnly data) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
        Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
    };

    private void ConfigurarRegistros(Guid clienteId, List<RegistroDiario> registros) =>
        _registroRepoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).ReturnsAsync(registros);

    [Fact]
    public async Task ListarSugestoesAsync_ClienteTentandoVerDeOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var outroUsuario = Guid.NewGuid();
        var de = new DateOnly(2026, 10, 1);
        var ate = new DateOnly(2026, 10, 31);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ListarSugestoesAsync(clienteId, de, ate, outroUsuario, "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ListarSugestoesAsync_TituloPendenteComLancamentoProximoEmValorEData_SugereComScoreAlto()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var vencimento = new DateOnly(2026, 10, 10);

        var registroTitulo = CriarRegistro(clienteId, contaId, vencimento.AddDays(-5));
        registroTitulo.ContasPagar.Add(new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "ENERGISA DISTRIBUIDORA", Valor = 250m,
            DataVencimento = vencimento, Pago = false, Categoria = "Utilidades", ContaBancariaId = contaId,
        });

        var registroLancamento = CriarRegistro(clienteId, contaId, vencimento);
        registroLancamento.Saidas.Add(new ItemFinanceiroSaida
        {
            Id = Guid.NewGuid(), Descricao = "ENERGISA DISTRIBUIDORA LTDA", Valor = 250m, Categoria = "Utilidades",
        });

        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registroTitulo, registroLancamento });

        var resultado = await _sut.ListarSugestoesAsync(clienteId, vencimento.AddDays(-5), vencimento.AddDays(5), clienteId, "cliente");

        var sugestao = Assert.Single(resultado);
        Assert.Equal("Pagar", sugestao.Tipo);
        Assert.Equal(registroLancamento.Saidas[0].Id, sugestao.LancamentoId);
        Assert.True(sugestao.Score >= 90, $"Esperava score alto, obteve {sugestao.Score}");
    }

    [Fact]
    public async Task ListarSugestoesAsync_LancamentoJaVinculadoAOutroTitulo_NaoSugereDeNovo()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var vencimento = new DateOnly(2026, 10, 10);
        var lancamentoId = Guid.NewGuid();

        var registro = CriarRegistro(clienteId, contaId, vencimento);
        registro.Saidas.Add(new ItemFinanceiroSaida { Id = lancamentoId, Descricao = "Fornecedor X", Valor = 100m });
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "Fornecedor X", Valor = 100m, DataVencimento = vencimento,
            Pago = false, ContaBancariaId = contaId,
        });
        // Outro título qualquer já usa esse mesmo lançamento.
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "Fornecedor X", Valor = 100m, DataVencimento = vencimento,
            Pago = true, ContaBancariaId = contaId, LancamentoVinculadoId = lancamentoId,
        });

        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarSugestoesAsync(clienteId, vencimento.AddDays(-1), vencimento.AddDays(1), clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSugestoesAsync_DiferencaDeValorAcimaDe30Porcento_NaoSugere()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var vencimento = new DateOnly(2026, 10, 10);

        var registro = CriarRegistro(clienteId, contaId, vencimento);
        registro.ContasReceber.Add(new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "Cliente Y", Valor = 100m, DataVencimento = vencimento,
            Pago = false, ContaBancariaId = contaId,
        });
        registro.Entradas.Add(new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = "Cliente Y", Valor = 150m });

        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarSugestoesAsync(clienteId, vencimento.AddDays(-1), vencimento.AddDays(1), clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSugestoesAsync_LancamentoDeTransferencia_NuncaEhSugerido()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var vencimento = new DateOnly(2026, 10, 10);

        var registro = CriarRegistro(clienteId, contaId, vencimento);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "Pagamento", Valor = 100m, DataVencimento = vencimento,
            Pago = false, ContaBancariaId = contaId,
        });
        registro.Saidas.Add(new ItemFinanceiroSaida
        {
            Id = Guid.NewGuid(), Descricao = "Pagamento", Valor = 100m, TipoCusto = "Transferencia", TransferenciaId = Guid.NewGuid(),
        });

        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarSugestoesAsync(clienteId, vencimento.AddDays(-1), vencimento.AddDays(1), clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSugestoesAsync_ContaBancariaDiferente_NaoSugere()
    {
        var clienteId = Guid.NewGuid();
        var contaA = Guid.NewGuid();
        var contaB = Guid.NewGuid();
        var vencimento = new DateOnly(2026, 10, 10);

        var registroTitulo = CriarRegistro(clienteId, contaA, vencimento);
        registroTitulo.ContasPagar.Add(new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "Aluguel", Valor = 100m, DataVencimento = vencimento,
            Pago = false, ContaBancariaId = contaA,
        });

        var registroLancamento = CriarRegistro(clienteId, contaB, vencimento);
        registroLancamento.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Aluguel", Valor = 100m });

        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registroTitulo, registroLancamento });

        var resultado = await _sut.ListarSugestoesAsync(clienteId, vencimento.AddDays(-1), vencimento.AddDays(1), clienteId, "cliente");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSugestoesAsync_DoisTitulosDisputandoOMesmoLancamento_SoOMelhorScoreGanha()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var vencimento = new DateOnly(2026, 10, 10);

        var registro = CriarRegistro(clienteId, contaId, vencimento);
        var tituloExato = new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "Fornecedor Alpha", Valor = 100m, DataVencimento = vencimento,
            Pago = false, ContaBancariaId = contaId,
        };
        var tituloDistante = new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "Fornecedor Alpha", Valor = 100m, DataVencimento = vencimento.AddDays(4),
            Pago = false, ContaBancariaId = contaId,
        };
        registro.ContasPagar.Add(tituloExato);
        registro.ContasPagar.Add(tituloDistante);
        registro.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Fornecedor Alpha", Valor = 100m });

        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarSugestoesAsync(clienteId, vencimento.AddDays(-1), vencimento.AddDays(5), clienteId, "cliente");

        var sugestao = Assert.Single(resultado);
        Assert.Equal(tituloExato.Id, sugestao.ContaProvisionadaId);
    }

    [Fact]
    public async Task ListarSugestoesAsync_ComFiltroDeContaBancaria_IgnoraSugestoesDeOutraConta()
    {
        var clienteId = Guid.NewGuid();
        var contaA = Guid.NewGuid();
        var contaB = Guid.NewGuid();
        var vencimento = new DateOnly(2026, 10, 10);

        var registroA = CriarRegistro(clienteId, contaA, vencimento);
        registroA.ContasPagar.Add(new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "Fornecedor A", Valor = 100m, DataVencimento = vencimento,
            Pago = false, ContaBancariaId = contaA,
        });
        registroA.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Fornecedor A", Valor = 100m });

        var registroB = CriarRegistro(clienteId, contaB, vencimento);
        registroB.ContasPagar.Add(new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "Fornecedor B", Valor = 100m, DataVencimento = vencimento,
            Pago = false, ContaBancariaId = contaB,
        });
        registroB.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Fornecedor B", Valor = 100m });

        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registroA, registroB });

        var resultado = await _sut.ListarSugestoesAsync(
            clienteId, vencimento.AddDays(-1), vencimento.AddDays(1), clienteId, "cliente", contaBancariaId: contaA);

        var sugestao = Assert.Single(resultado);
        Assert.Equal("Fornecedor A", sugestao.Descricao);
    }

    [Fact]
    public async Task ListarSugestoesAsync_TituloSemDataDeVencimento_NuncaEhSugerido()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var data = new DateOnly(2026, 10, 10);

        var registro = CriarRegistro(clienteId, contaId, data);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Id = Guid.NewGuid(), Descricao = "Fornecedor Z", Valor = 100m, DataVencimento = null,
            Pago = false, ContaBancariaId = contaId,
        });
        registro.Saidas.Add(new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Fornecedor Z", Valor = 100m });

        ConfigurarRegistros(clienteId, new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarSugestoesAsync(clienteId, data.AddDays(-1), data.AddDays(1), clienteId, "cliente");

        Assert.Empty(resultado);
    }
}
