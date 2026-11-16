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

// Money Vision ("transferência cria contrapartida falsa"): Nubank −400 em 14/10 marcado como
// transferência pro C6 — procura o recebimento real no C6 antes de inventar um.
public class TransferenciaContrapartidaTests
{
    private static readonly DateOnly Dia = new(2026, 10, 14);
    private readonly Guid _clienteId = Guid.NewGuid();
    private readonly ContaBancaria _nubank;
    private readonly ContaBancaria _c6;
    private readonly Mock<ITransferenciaRepository> _transferenciaRepo = new();
    private readonly Mock<IContaBancariaRepository> _contaRepo = new();
    private readonly Mock<IRegistroRepository> _registroRepo = new();
    private readonly Mock<ITransacaoImportadaRepository> _importRepo = new();
    private readonly Mock<IPeriodoExtratoRepository> _periodoRepo = new();
    private readonly RegistroDiario _diaNubank;
    private readonly ItemFinanceiroSaida _envio;
    private readonly List<RegistroDiario> _registrosC6 = new();
    private readonly TransferenciaService _sut;

    public TransferenciaContrapartidaTests()
    {
        _nubank = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = _clienteId, Nome = "Nubank", Tipo = "ContaCorrente", Ativa = true };
        _c6 = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = _clienteId, Nome = "C6 Bank", Tipo = "ContaCorrente", Ativa = true };
        _envio = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Transferência enviada pelo Pix - Matheus", Valor = 400m, FitId = "N1" };
        _diaNubank = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = _clienteId, ContaBancariaId = _nubank.Id, Data = Dia, Inicio = 1000m, SaldoFinal = 600m,
            Entradas = new(), Saidas = new() { _envio }, ContasReceber = new(), ContasPagar = new(),
        };

        _contaRepo.Setup(r => r.ObterPorIdAsync(_nubank.Id)).ReturnsAsync(_nubank);
        _contaRepo.Setup(r => r.ObterPorIdAsync(_c6.Id)).ReturnsAsync(_c6);
        _registroRepo.Setup(r => r.ObterPorContaEDataAsync(_nubank.Id, Dia)).ReturnsAsync(_diaNubank);
        _registroRepo.Setup(r => r.ObterPorContaEDataAsync(_c6.Id, It.IsAny<DateOnly>()))
            .ReturnsAsync((Guid _, DateOnly d) => _registrosC6.FirstOrDefault(r => r.Data == d));
        _registroRepo.Setup(r => r.ListarPorContaAsync(_c6.Id)).ReturnsAsync(() => _registrosC6.ToList());
        _registroRepo.Setup(r => r.ListarPorContaAsync(_nubank.Id)).ReturnsAsync(() => new List<RegistroDiario> { _diaNubank });
        _registroRepo.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _registroRepo.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => { if (r.ContaBancariaId == _c6.Id) _registrosC6.Add(r); })
            .ReturnsAsync((RegistroDiario r) => r);
        _transferenciaRepo.Setup(r => r.AdicionarAsync(It.IsAny<Transferencia>())).ReturnsAsync((Transferencia t) => t);
        _importRepo.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<TransacaoImportada>());
        _periodoRepo.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<PeriodoExtrato>());

        var contexto = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _sut = new TransferenciaService(_transferenciaRepo.Object, _contaRepo.Object, _registroRepo.Object, new Mock<IAuditService>().Object,
            contexto, _importRepo.Object, _periodoRepo.Object);
    }

    private ConverterLancamentoEmTransferenciaDto Dto(bool criarMesmoAssim = false) => new()
    {
        ContaId = _nubank.Id, LancamentoId = _envio.Id, Data = Dia, Tipo = "Saida", ContaContrapartidaId = _c6.Id, CriarMesmoAssim = criarMesmoAssim,
    };

    private ItemFinanceiro RecebimentoNoC6(DateOnly data, decimal valor = 400m)
    {
        var item = new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = "Pix recebido de Matheus", Valor = valor, FitId = Guid.NewGuid().ToString() };
        var registro = _registrosC6.FirstOrDefault(r => r.Data == data);
        if (registro == null)
        {
            registro = new RegistroDiario
            {
                Id = Guid.NewGuid(), ClienteId = _clienteId, ContaBancariaId = _c6.Id, Data = data,
                Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            };
            _registrosC6.Add(registro);
        }
        registro.Entradas.Add(item);
        return item;
    }

    private void ExtratoDoC6Cobre(DateOnly de, DateOnly ate) =>
        _periodoRepo.Setup(r => r.ListarPorContaAsync(_c6.Id)).ReturnsAsync(new List<PeriodoExtrato> { new() { ContaBancariaId = _c6.Id, De = de, Ate = ate } });

    [Fact]
    public async Task ConverterLancamentoAsync_OutroLadoRealJaImportado_PareiaSemCriarLancamento()
    {
        var recebimento = RecebimentoNoC6(Dia.AddDays(1));

        await _sut.ConverterLancamentoAsync(Dto(), _clienteId, "cliente");

        Assert.Equal(_envio.TransferenciaId, recebimento.TransferenciaId);
        Assert.NotNull(recebimento.TransferenciaId);
        Assert.Single(_registrosC6.SelectMany(r => r.Entradas));
        _registroRepo.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task ConverterLancamentoAsync_ExtratoDoDestinoCobreADataSemORecebimento_AvisaENaoMexeEmNada()
    {
        ExtratoDoC6Cobre(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 8));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConverterLancamentoAsync(Dto(), _clienteId, "cliente"));

        Assert.Equal((409, CodigoRetorno.CONTRAPARTIDA_NAO_ENCONTRADA), (ex.StatusCode, ex.Codigo));
        Assert.Equal("O extrato do C6 Bank não tem esse recebimento em 14/10. Tem certeza que foi para o C6 Bank?", ex.Message);
        Assert.Null(_envio.TransferenciaId);
        Assert.Empty(_registrosC6);
        _registroRepo.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task ConverterLancamentoAsync_UsuarioConfirmaCriarMesmoAssim_CriaContrapartidaProvisoriaComDescricaoPropria()
    {
        ExtratoDoC6Cobre(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 8));

        await _sut.ConverterLancamentoAsync(Dto(criarMesmoAssim: true), _clienteId, "cliente");

        var criada = Assert.Single(_registrosC6.SelectMany(r => r.Entradas));
        Assert.Equal(("↔ Transferência de Nubank", true, 400m), (criada.Descricao, criada.Provisoria, criada.Valor));
    }

    [Fact]
    public async Task ConverterLancamentoAsync_DestinoSemExtratoNaData_CriaContrapartidaProvisoria()
    {
        await _sut.ConverterLancamentoAsync(Dto(), _clienteId, "cliente");

        var criada = Assert.Single(_registrosC6.SelectMany(r => r.Entradas));
        Assert.True(criada.Provisoria);
        Assert.Equal("↔ Transferência de Nubank", criada.Descricao);
    }

    [Fact]
    public async Task ConverterLancamentoAsync_DoisRecebimentosIguaisNoDestino_PedeParaEscolher()
    {
        RecebimentoNoC6(Dia);
        RecebimentoNoC6(Dia.AddDays(2));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ConverterLancamentoAsync(Dto(), _clienteId, "cliente"));

        Assert.Equal(CodigoRetorno.CONTRAPARTIDA_AMBIGUA, ex.Codigo);
        Assert.Null(_envio.TransferenciaId);
    }

    [Fact]
    public async Task ListarCandidatosContrapartidaAsync_MostraOsRecebimentosEOQueOExtratoCobre()
    {
        var recebimento = RecebimentoNoC6(Dia.AddDays(-1));
        RecebimentoNoC6(Dia, valor: 401m);
        ExtratoDoC6Cobre(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 8));

        var resultado = await _sut.ListarCandidatosContrapartidaAsync(_nubank.Id, _envio.Id, Dia, _c6.Id, _clienteId, "cliente");

        Assert.Equal(recebimento.Id, Assert.Single(resultado.Candidatos).Id);
        Assert.True(resultado.ExtratoCobreData);
    }

    [Fact]
    public async Task DesfazerClassificacaoAsync_ParComLancamentoRealDeOutroDia_AchaAPontaPeloVinculo()
    {
        var recebimento = RecebimentoNoC6(Dia.AddDays(1));
        var transferencia = await _sut.ConverterLancamentoAsync(Dto(), _clienteId, "cliente");
        _transferenciaRepo.Setup(r => r.ObterPorIdAsync(transferencia.Id))
            .ReturnsAsync(new Transferencia { Id = transferencia.Id, ClienteId = _clienteId, ContaOrigemId = _nubank.Id, ContaDestinoId = _c6.Id, Data = Dia, Valor = 400m });

        await _sut.DesfazerClassificacaoAsync(transferencia.Id, _clienteId, "cliente");

        Assert.Null(recebimento.TransferenciaId);
        Assert.Null(_envio.TransferenciaId);
    }
}
