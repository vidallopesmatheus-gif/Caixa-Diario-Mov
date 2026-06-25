using CaixaDiario.API.DTOs.ContasBancarias;
using CaixaDiario.API.DTOs.Importacao;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/contas-bancarias")]
[Authorize]
public class ContasBancariasController : ControllerBase
{
    private readonly IContaBancariaService _service;
    private readonly IImportacaoService _importacaoService;

    public ContasBancariasController(IContaBancariaService service, IImportacaoService importacaoService)
    {
        _service = service;
        _importacaoService = importacaoService;
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var contas = await _service.ListarPorClienteAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<ContaBancariaDto>> { Dados = contas });
    }

    [HttpGet("detalhe/{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var conta = await _service.ObterPorIdAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaBancariaDto> { Dados = conta });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarContaBancariaDto dto)
    {
        var criada = await _service.CriarAsync(dto, ObterUsuarioId(), ObterPerfil());
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id },
            new ApiResponse<ContaBancariaDto> { Dados = criada });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarContaBancariaDto dto)
    {
        var atualizada = await _service.AtualizarAsync(id, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaBancariaDto> { Dados = atualizada });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Inativar(Guid id)
    {
        await _service.InativarAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    // ── Importação de extrato ──────────────────────────────────────────────────

    [HttpPost("{contaId:guid}/importar-extrato")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> ImportarExtrato(Guid contaId, IFormFile arquivo)
    {
        var transacoes = await _importacaoService.ImportarArquivoAsync(
            contaId, ObterUsuarioId(), ObterPerfil(), arquivo);
        return Ok(new ApiResponse<List<TransacaoImportadaDto>> { Dados = transacoes });
    }

    [HttpGet("{contaId:guid}/transacoes-pendentes")]
    public async Task<IActionResult> ListarPendentes(Guid contaId)
    {
        var transacoes = await _importacaoService.ListarPendentesAsync(
            contaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<TransacaoImportadaDto>> { Dados = transacoes });
    }

    [HttpPost("{contaId:guid}/confirmar-transacoes")]
    public async Task<IActionResult> ConfirmarTransacoes(Guid contaId, [FromBody] ConfirmarTransacoesDto dto)
    {
        await _importacaoService.ConfirmarTransacoesAsync(
            contaId, ObterUsuarioId(), ObterPerfil(), dto);
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
