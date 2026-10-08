using CaixaDiario.API.DTOs.ContasBancarias;

namespace CaixaDiario.API.Services;

public interface IDuplicataExtratoService
{
    Task<List<DuplicataProvavelDto>> ListarProvaveisAsync(Guid contaBancariaId, Guid usuarioLogadoId, string perfil);

    // Item 3.2: "Manter os dois" — o par não aparece mais em ListarProvaveisAsync depois disso.
    Task ManterOsDoisAsync(Guid contaBancariaId, Guid lancamentoAId, Guid lancamentoBId, Guid usuarioLogadoId, string perfil);
}
