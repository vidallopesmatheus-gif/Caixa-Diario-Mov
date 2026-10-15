using CaixaDiario.API.DTOs.Projecao;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/projecao")]
[Authorize]
public class ProjecaoController : ControllerBase
{
    private readonly IProjecaoService _projecaoService;
    private readonly IRegistroRepository _registroRepo;
    private readonly IContaRecorrenteRepository _recorrenteRepo;
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IPagamentoFaturaRepository _pagamentoFaturaRepo;
    private readonly IFaturaRepository _faturaRepo;

    public ProjecaoController(
        IProjecaoService projecaoService,
        IRegistroRepository registroRepo,
        IContaRecorrenteRepository recorrenteRepo,
        IContaBancariaRepository contaRepo,
        IPagamentoFaturaRepository pagamentoFaturaRepo,
        IFaturaRepository faturaRepo)
    {
        _projecaoService     = projecaoService;
        _registroRepo        = registroRepo;
        _recorrenteRepo       = recorrenteRepo;
        _contaRepo            = contaRepo;
        _pagamentoFaturaRepo  = pagamentoFaturaRepo;
        _faturaRepo           = faturaRepo;
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil()  => User.FindFirst("perfil")!.Value;

    // Bloco 2 da rodada "Visão de Futuro": monta as opções (contas do cliente + pagamentos de
    // fatura, pro item 4; cenário e reserva, pros itens 2/5) uma vez só, reaproveitada pelos dois
    // endpoints de projeção abaixo.
    private async Task<ProjecaoOpcoes> MontarOpcoesAsync(Guid clienteId, string cenario, bool incluirReserva)
    {
        var contas = await _contaRepo.ListarPorClienteAsync(clienteId);
        var cartoes = contas.Where(c => c.Tipo == "CartaoCredito").ToList();
        var pagamentosFatura = new List<PagamentoFatura>();
        var faturasReais = new List<Fatura>();
        foreach (var cartao in cartoes)
        {
            pagamentosFatura.AddRange(await _pagamentoFaturaRepo.ListarPorContaCartaoAsync(cartao.Id));
            faturasReais.AddRange(await _faturaRepo.ListarPorContaAsync(cartao.Id));
        }

        var cenarioValido = cenario == "Conservador" ? "Conservador" : "Provavel";
        return new ProjecaoOpcoes(contas, pagamentosFatura, cenarioValido, incluirReserva, faturasReais);
    }

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> ObterProjecao(
        Guid clienteId,
        [FromQuery] int dias = 30,
        [FromQuery] Guid? contaBancariaId = null,
        [FromQuery] string cenario = "Provavel",
        [FromQuery] bool incluirReserva = false)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (dias is not (30 or 60 or 90))
            dias = 30;

        var registros   = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _recorrenteRepo.ListarAtivasPorClienteAsync(clienteId);
        var opcoes = await MontarOpcoesAsync(clienteId, cenario, incluirReserva);

        var resultado = _projecaoService.Calcular(registros, recorrentes, dias, contaBancariaId, opcoes);
        return Ok(new ApiResponse<ProjecaoDto> { Dados = resultado });
    }

    [HttpGet("{clienteId:guid}/trajetoria")]
    public async Task<IActionResult> ObterTrajetoria(
        Guid clienteId,
        [FromQuery] int mesesPassado = 6,
        [FromQuery] int mesesFuturo = 6,
        [FromQuery] Guid? contaBancariaId = null,
        [FromQuery] string cenario = "Provavel",
        [FromQuery] bool incluirReserva = false)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        mesesPassado = Math.Clamp(mesesPassado, 1, 12);
        mesesFuturo = Math.Clamp(mesesFuturo, 1, 12);

        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _recorrenteRepo.ListarAtivasPorClienteAsync(clienteId);
        var opcoes = await MontarOpcoesAsync(clienteId, cenario, incluirReserva);

        var resultado = _projecaoService.CalcularTrajetoria(registros, recorrentes, mesesPassado, mesesFuturo, contaBancariaId, opcoes);
        return Ok(new ApiResponse<TrajetoriaDto> { Dados = resultado });
    }

    // Item 3 da rodada "Visão de Futuro": despesas fixas estimadas por categoria (não cadastradas
    // como recorrência), pra tela mostrar "Transformar em conta a pagar" por categoria.
    [HttpGet("{clienteId:guid}/despesas-estimadas")]
    public async Task<IActionResult> ObterDespesasEstimadas(Guid clienteId)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _recorrenteRepo.ListarAtivasPorClienteAsync(clienteId);
        var resultado = _projecaoService.CalcularDespesasEstimadasPorCategoria(registros, recorrentes, DataLocalHelper.Hoje());
        return Ok(new ApiResponse<List<DespesaEstimadaCategoriaDto>> { Dados = resultado });
    }

    // Bloco 3 da rodada "Visão de Futuro": tela "Meu Futuro" — 12 meses + conselho em regras.
    // ajusteReceitaPercent/atrasoRecebimentosDias são o simulador "E se...?" (0 = sem ajuste).
    [HttpGet("{clienteId:guid}/meu-futuro")]
    public async Task<IActionResult> ObterMeuFuturo(
        Guid clienteId,
        [FromQuery] decimal reservaMinima = 0m,
        [FromQuery] string cenario = "Provavel",
        [FromQuery] bool incluirReserva = false,
        [FromQuery] decimal ajusteReceitaPercent = 0m,
        [FromQuery] int atrasoRecebimentosDias = 0)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _recorrenteRepo.ListarAtivasPorClienteAsync(clienteId);
        var opcoes = await MontarOpcoesAsync(clienteId, cenario, incluirReserva);

        var resultado = _projecaoService.CalcularMeuFuturo(
            registros, recorrentes, opcoes, reservaMinima, ajusteReceitaPercent, atrasoRecebimentosDias);
        return Ok(new ApiResponse<MeuFuturoDto> { Dados = resultado });
    }
}
