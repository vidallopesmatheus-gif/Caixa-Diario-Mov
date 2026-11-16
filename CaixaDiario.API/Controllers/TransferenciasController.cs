using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/transferencias")]
[Authorize]
public class TransferenciasController : ControllerBase
{
    private readonly ITransferenciaService _service;
    private readonly IRevisaoTransferenciasService _revisao;

    public TransferenciasController(ITransferenciaService service, IRevisaoTransferenciasService revisao)
    {
        _service = service;
        _revisao = revisao;
    }

    // Money Vision ("contrapartida falsa"): "Transferências para revisar" — o que se corrige sozinho
    // (contrapartida inventada + lançamento real solto, par duplicado) e o que o usuário decide.
    [HttpGet("{clienteId:guid}/revisar")]
    public async Task<IActionResult> ListarParaRevisar(Guid clienteId)
    {
        var resultado = await _revisao.ListarAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<RevisaoTransferenciasDto> { Dados = resultado });
    }

    [HttpPost("{clienteId:guid}/revisar/corrigir")]
    public async Task<IActionResult> CorrigirContrapartidas(Guid clienteId)
    {
        var resultado = await _revisao.CorrigirDoClienteAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<CorrecaoTransferenciasResultDto> { Dados = resultado });
    }

    // "Não foi para essa conta" — apaga a contrapartida inventada; o lançamento real volta a pendente.
    [HttpPost("{id:guid}/descartar-contrapartida")]
    public async Task<IActionResult> DescartarContrapartida(Guid id)
    {
        await _revisao.DescartarContrapartidaAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    // "Foi para essa conta mesmo" — o extrato não tem, mas o usuário confirma a contrapartida.
    [HttpPost("{id:guid}/confirmar-contrapartida")]
    public async Task<IActionResult> ConfirmarContrapartida(Guid id)
    {
        await _revisao.ConfirmarContrapartidaAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var transferencias = await _service.ListarPorClienteAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<TransferenciaDto>> { Dados = transferencias });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DesfazerClassificacao(Guid id)
    {
        await _service.DesfazerClassificacaoAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpPost("converter-lancamento")]
    public async Task<IActionResult> ConverterLancamento([FromBody] ConverterLancamentoEmTransferenciaDto dto)
    {
        var criada = await _service.ConverterLancamentoAsync(dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<TransferenciaDto> { Dados = criada });
    }

    // Money Vision ("contrapartida falsa"): antes de converter, a tela mostra com qual lançamento
    // real da outra conta vai parear — ou avisa que o extrato dela não tem esse valor na data.
    [HttpGet("candidatos-contrapartida")]
    public async Task<IActionResult> ListarCandidatosContrapartida(
        [FromQuery] Guid contaId, [FromQuery] Guid lancamentoId, [FromQuery] DateOnly data, [FromQuery] Guid contaContrapartidaId)
    {
        var resultado = await _service.ListarCandidatosContrapartidaAsync(contaId, lancamentoId, data, contaContrapartidaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<CandidatosContrapartidaDto> { Dados = resultado });
    }

    // Bloco 4, item 4: "Transferências sem par" — sugestões da importação ainda não confirmadas.
    [HttpGet("{clienteId:guid}/sugestoes-sem-par")]
    public async Task<IActionResult> ListarSugestoesSemPar(Guid clienteId)
    {
        var sugestoes = await _service.ListarSugestoesSemParAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<SugestaoTransferenciaSemParDto>> { Dados = sugestoes });
    }

    [HttpPost("descartar-sugestao")]
    public async Task<IActionResult> DescartarSugestao([FromBody] DescartarSugestaoTransferenciaDto dto)
    {
        await _service.DescartarSugestaoAsync(dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
