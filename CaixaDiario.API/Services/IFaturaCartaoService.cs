using CaixaDiario.API.DTOs.FaturasCartao;

namespace CaixaDiario.API.Services;

public interface IFaturaCartaoService
{
    Task<List<FaturaCartaoDto>> ListarFaturasAsync(Guid contaCartaoId, Guid usuarioLogadoId, string perfil);
    // Cartão (Bloco 2): valores de "banco" já usados por este cliente — alimenta o combobox
    // editável da importação ("Nubank, C6, Itaú..."; digitar um nome novo "cria" o emissor).
    Task<List<string>> ListarBancosAsync(Guid contaCartaoId, Guid usuarioLogadoId, string perfil);
    Task<FaturaCartaoDto?> SugerirFaturaAsync(Guid contaCartaoId, decimal valor, DateOnly data, Guid usuarioLogadoId, string perfil);
    Task<PagamentoFaturaDto> VincularPagamentoAsync(VincularPagamentoFaturaDto dto, Guid usuarioLogadoId, string perfil);
    Task<PagamentoFaturaDto> PagarFaturaAsync(Guid contaCartaoId, PagarFaturaDto dto, Guid usuarioLogadoId, string perfil);
    Task DesvincularPagamentoAsync(Guid id, Guid usuarioLogadoId, string perfil);
    // Item 5 (Money Vision "pagamento não abate"): acha e vincula automaticamente uma transferência
    // já existente pro cartão que paga esta fatura (chamado depois de toda importação de fatura
    // real). Retorna true se vinculou. Nunca cria lançamento novo, nunca decide se for ambíguo.
    Task<bool> AutoVincularPagamentoPorTransferenciaAsync(Guid contaCartaoId, Guid faturaId);
    // origemMetaId: só preenchido internamente por MetaService.ConquistarAsync (forma Cartão) —
    // nunca vem de fora pelo DTO público (mesmo motivo de ContaProvisionadaService.CriarAsync).
    Task LancarComprasParceladaAsync(
        Guid contaCartaoId, Guid usuarioLogadoId, string perfil, LancarCompraParceladaDto dto, Guid? origemMetaId = null);
}
