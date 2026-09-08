using CaixaDiario.API.DTOs.Transferencias;

namespace CaixaDiario.API.Services;

public interface ITransferenciaService
{
    Task<List<TransferenciaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    // Desfaz a classificação como Transferência: reverte os dois lados (categoria/tipo/vínculo)
    // ao estado "sem categoria, pendente" e apaga só o registro de Transferencia — nunca apaga os
    // lançamentos em si, porque eles são dinheiro que realmente se moveu no banco.
    Task DesfazerClassificacaoAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task<TransferenciaDto> ConverterLancamentoAsync(ConverterLancamentoEmTransferenciaDto dto, Guid usuarioLogadoId, string perfil);
}
