using CaixaDiario.API.DTOs.Registros;

namespace CaixaDiario.API.Services;

public interface IContaProvisionadaService
{
    // origemMetaId: só preenchido internamente por MetaService.ConquistarAsync (forma Boleto) —
    // nunca vem de fora pelo DTO público, pra um payload adulterado não conseguir marcar uma conta
    // a pagar qualquer como "criada por uma conquista" (ver ContaProvisionada.OrigemMetaId).
    Task<ContaProvisionadaDto> CriarAsync(CriarContaProvisionadaDto dto, Guid usuarioLogadoId, string perfil, Guid? origemMetaId = null);
    Task<ContaProvisionadaDto> AtualizarAsync(Guid clienteId, Guid id, AtualizarContaProvisionadaDto dto, Guid usuarioLogadoId, string perfil);
    Task ExcluirAsync(Guid clienteId, Guid id, Guid usuarioLogadoId, string perfil);
}
