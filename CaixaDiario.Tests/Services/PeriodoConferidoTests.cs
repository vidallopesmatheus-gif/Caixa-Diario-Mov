using CaixaDiario.API.Data;
using CaixaDiario.API.DTOs.ConciliacaoSaldo;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CaixaDiario.Tests.Services;

// Money Vision ("travar o período até o saldo conferido"): conferido e batendo com o banco, nada que
// mexa no saldo muda até a data sem reabrir — mesmo numa chamada direta à API.
public class PeriodoConferidoRepositorioTests : IDisposable
{
    private static readonly DateOnly Conferido = new(2026, 11, 7);
    private readonly AppDbContext _context = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly RegistroRepository _sut;
    private readonly ContaBancaria _nubank;

    public PeriodoConferidoRepositorioTests()
    {
        _sut = new RegistroRepository(_context);
        var clienteId = Guid.NewGuid();
        _nubank = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Nubank", TravadoAte = Conferido, TravaSaldoBanco = 86.40m };
        _context.ContasBancarias.Add(_nubank);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private RegistroDiario Dia(DateOnly data, decimal valor) => new()
    {
        Id = Guid.NewGuid(), ClienteId = _nubank.ClienteId, ContaBancariaId = _nubank.Id, Data = data,
        Entradas = new() { new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = "Pix", Valor = valor, Categoria = "Vendas" } },
    };

    private async Task<RegistroDiario> GravadoSemRastreioAsync(RegistroDiario dia)
    {
        _context.RegistrosDiarios.Add(dia);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return await _context.RegistrosDiarios.FirstAsync(r => r.Id == dia.Id);
    }

    [Fact]
    public async Task AtualizarAsync_ValorDeUmDiaConferido_Lanca409ENaoGrava()
    {
        var dia = await GravadoSemRastreioAsync(Dia(new DateOnly(2026, 11, 5), 100m));
        dia.Entradas[0].Valor = 150m;
        dia.Entradas = new List<ItemFinanceiro>(dia.Entradas);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AtualizarAsync(dia));

        Assert.Equal((409, CodigoRetorno.PERIODO_CONFERIDO), (ex.StatusCode, ex.Codigo));
        Assert.Equal(100m, (await _context.RegistrosDiarios.AsNoTracking().FirstAsync(r => r.Id == dia.Id)).Entradas[0].Valor);
    }

    [Fact]
    public async Task AtualizarAsync_SoACategoriaDeUmDiaConferido_GravaSemMudarOSaldo()
    {
        var dia = await GravadoSemRastreioAsync(Dia(new DateOnly(2026, 11, 5), 100m));
        dia.Entradas[0].Categoria = "Outras receitas";
        dia.Entradas = new List<ItemFinanceiro>(dia.Entradas);

        await _sut.AtualizarAsync(dia);

        var gravado = await _context.RegistrosDiarios.AsNoTracking().FirstAsync(r => r.Id == dia.Id);
        Assert.Equal(("Outras receitas", 100m), (gravado.Entradas[0].Categoria, gravado.Entradas[0].Valor));
    }

    [Fact]
    public async Task AdicionarAsync_LancamentoNovoNoPeriodoConferido_Lanca409()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AdicionarAsync(Dia(new DateOnly(2026, 11, 3), 50m)));

        Assert.Equal(409, ex.StatusCode);
        Assert.False(await _context.RegistrosDiarios.AnyAsync());
    }

    [Fact]
    public async Task AdicionarAsync_DiaDepoisDaTrava_Grava()
    {
        await _sut.AdicionarAsync(Dia(Conferido.AddDays(1), 50m));

        Assert.Equal(1, await _context.RegistrosDiarios.CountAsync());
    }

    [Fact]
    public async Task AtualizarAsync_ExcluirODiaConferido_Lanca409()
    {
        var dia = await GravadoSemRastreioAsync(Dia(new DateOnly(2026, 11, 5), 100m));
        dia.Excluido = true;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AtualizarAsync(dia));

        Assert.Equal(409, ex.StatusCode);
    }
}

public class PeriodoConferidoServiceTests
{
    private static readonly Guid Cliente = Guid.NewGuid();
    private readonly Mock<IContaBancariaRepository> _contaRepo = new();
    private readonly Mock<IRegistroRepository> _registroRepo = new();
    private readonly Mock<IConferenciaSaldoRepository> _conferenciaRepo = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly List<ConferenciaSaldo> _historico = new();
    private readonly List<RegistroDiario> _registros = new();
    private readonly PeriodoConferidoService _sut;
    private readonly ContaBancaria _conta = new() { Id = Guid.NewGuid(), ClienteId = Cliente, Nome = "Nubank" };

    public PeriodoConferidoServiceTests()
    {
        _contaRepo.Setup(r => r.ObterPorIdAsync(_conta.Id)).ReturnsAsync(_conta);
        _contaRepo.Setup(r => r.AtualizarAsync(It.IsAny<ContaBancaria>())).ReturnsAsync((ContaBancaria c) => c);
        _registroRepo.Setup(r => r.ListarPorContaAsync(_conta.Id)).ReturnsAsync(() => _registros.ToList());
        _conferenciaRepo.Setup(r => r.AdicionarAsync(It.IsAny<ConferenciaSaldo>())).Callback<ConferenciaSaldo>(c => _historico.Insert(0, c)).Returns(Task.CompletedTask);
        _conferenciaRepo.Setup(r => r.ListarPorContaAsync(_conta.Id)).ReturnsAsync(() => _historico.ToList());
        _sut = new PeriodoConferidoService(_contaRepo.Object, _registroRepo.Object, _conferenciaRepo.Object, _audit.Object);
    }

    private RegistroDiario Dia(DateOnly data, params (Guid Id, string Descricao, decimal Valor)[] entradas)
    {
        var dia = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Cliente, ContaBancariaId = _conta.Id, Data = data,
            Entradas = entradas.Select(e => new ItemFinanceiro { Id = e.Id, Descricao = e.Descricao, Valor = e.Valor }).ToList(),
        };
        _registros.Add(dia);
        return dia;
    }

    [Fact]
    public async Task TravarAsync_SaldoConferidoEm0711_TravaAte0711ERegistraAConferencia()
    {
        await _sut.TravarAsync(_conta, new DateOnly(2026, 11, 7), 86.40m, Cliente, "cliente");

        Assert.Equal((new DateOnly(2026, 11, 7), 86.40m, false), (_conta.TravadoAte, _conta.TravaSaldoBanco, _conta.TravadoPorConsultor));
        Assert.Equal(ConferenciaSaldo.TipoConferencia, Assert.Single(_historico).Tipo);
    }

    [Fact]
    public async Task TravarAsync_ConferirUmaDataAnterior_NaoEncurtaATrava()
    {
        _conta.TravadoAte = new DateOnly(2026, 11, 7);

        await _sut.TravarAsync(_conta, new DateOnly(2026, 10, 31), 50m, Cliente, "cliente");

        Assert.Equal(new DateOnly(2026, 11, 7), _conta.TravadoAte);
    }

    [Fact]
    public async Task ReabrirAsync_APartirDe0311ComMotivo_TravaAte0211EAuditaQuemQuandoEMotivo()
    {
        _conta.TravadoAte = new DateOnly(2026, 11, 7);

        var trava = await _sut.ReabrirAsync(_conta.Id,
            new ReabrirPeriodoDto { APartirDe = new DateOnly(2026, 11, 3), Motivo = "banco lançou estorno com data de 03/11" }, Cliente, "cliente");

        Assert.Equal(new DateOnly(2026, 11, 2), trava.TravadoAte);
        var reabertura = Assert.Single(_historico);
        Assert.Equal((ConferenciaSaldo.TipoReabertura, new DateOnly(2026, 11, 3), "banco lançou estorno com data de 03/11", Cliente),
            (reabertura.Tipo, reabertura.Data!.Value, reabertura.Motivo, reabertura.UsuarioId));
        _audit.Verify(a => a.LogAsync(Cliente, Cliente, "PeriodoConferido", "Edicao", _conta.Id.ToString(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ReabrirAsync_ReabrirTudo_TiraATrava()
    {
        _conta.TravadoAte = new DateOnly(2026, 11, 7);

        var trava = await _sut.ReabrirAsync(_conta.Id, new ReabrirPeriodoDto(), Cliente, "cliente");

        Assert.Null(trava.TravadoAte);
    }

    [Fact]
    public async Task ReabrirAsync_ClienteComPeriodoConferidoPeloConsultor_Lanca403()
    {
        _conta.TravadoAte = new DateOnly(2026, 11, 7);
        _conta.TravadoPorConsultor = true;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ReabrirAsync(_conta.Id, new ReabrirPeriodoDto { APartirDe = new DateOnly(2026, 11, 3) }, Cliente, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(new DateOnly(2026, 11, 7), _conta.TravadoAte);
    }

    [Fact]
    public async Task ReabrirAsync_DataDepoisDaTrava_Lanca400()
    {
        _conta.TravadoAte = new DateOnly(2026, 11, 7);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ReabrirAsync(_conta.Id, new ReabrirPeriodoDto { APartirDe = new DateOnly(2026, 11, 10) }, Cliente, "cliente"));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task MudancasDesdeConferenciaAsync_DepoisDeReabrir_ListaCriadoEditadoEExcluido()
    {
        var (editado, excluido) = (Guid.NewGuid(), Guid.NewGuid());
        var dia = Dia(new DateOnly(2026, 11, 3), (editado, "Pix Ana", 100m), (excluido, "Pix Bia", 40m));
        await _sut.TravarAsync(_conta, new DateOnly(2026, 11, 7), 140m, Cliente, "cliente");
        dia.Entradas = new()
        {
            new ItemFinanceiro { Id = editado, Descricao = "Pix Ana", Valor = 120m },
            new ItemFinanceiro { Id = Guid.NewGuid(), Descricao = "Estorno", Valor = 10m },
        };

        var mudancas = await _sut.MudancasDesdeConferenciaAsync(_conta, _registros);

        Assert.Equal(new[] { ("Criado", "Estorno"), ("Editado", "Pix Ana"), ("Excluido", "Pix Bia") },
            mudancas.Select(m => (m.Tipo, m.Descricao)).OrderBy(m => m.Tipo));
        Assert.Equal((100m, 120m), (mudancas.Single(m => m.Tipo == "Editado").ValorAntes!.Value, mudancas.Single(m => m.Tipo == "Editado").ValorDepois!.Value));
    }
}
