using CaixaDiario.API.DTOs.ContasBancarias;

namespace CaixaDiario.API.Services;

public interface IContaBancariaService
{
    Task<List<ContaBancariaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> ObterPorIdAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> CriarAsync(CriarContaBancariaDto dto, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> AtualizarAsync(Guid id, AtualizarContaBancariaDto dto, Guid usuarioLogadoId, string perfil);
    Task<ExclusaoContaBancariaResultDto> ExcluirOuInativarAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task<List<LancamentoExtratoDto>> ObterExtratoAsync(Guid contaId, Guid usuarioLogadoId, string perfil, DateOnly? de, DateOnly? ate);
    Task<PendenciasContaDto> ObterPendenciasAsync(Guid contaId, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> RegistrarRendimentoAsync(Guid contaId, RegistrarRendimentoDto dto, Guid usuarioLogadoId, string perfil);
    // Item 4 (Money Vision "saldo não fecha com o extrato"): acerta o saldo da conta com o banco
    // através de UM lançamento visível, nunca trocando o Inicio em silêncio.
    Task<ContaBancariaDto> AjustarSaldoAsync(Guid contaId, AjustarSaldoDto dto, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> VincularMetaAsync(Guid contaId, Guid metaId, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> DesvincularMetaAsync(Guid contaId, Guid metaId, Guid usuarioLogadoId, string perfil);

    // Money Vision — Bloco 5, item 3 da Parte 3: gráfico "saldo mês a mês" da conta de investimento.
    Task<List<SaldoMensalDto>> ObterHistoricoSaldoMensalAsync(Guid contaId, Guid usuarioLogadoId, string perfil, int meses = 12);
}
