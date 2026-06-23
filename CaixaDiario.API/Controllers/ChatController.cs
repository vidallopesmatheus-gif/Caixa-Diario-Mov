using CaixaDiario.API.DTOs.Chat;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService) => _chatService = chatService;

    [HttpPost]
    public async Task<IActionResult> Responder([FromBody] ChatRequestDto dto)
    {
        var resultado = await _chatService.ResponderAsync(dto);
        return Ok(new ApiResponse<ChatResponseDto> { Dados = resultado });
    }
}
