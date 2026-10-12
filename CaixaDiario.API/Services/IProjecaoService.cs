using CaixaDiario.API.DTOs.Projecao;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

// Bloco 2 da rodada "Visão de Futuro": agrupa os parâmetros novos (cenário, reserva de
// investimento, dados pra considerar faturas de cartão) num único objeto — Calcular/
// CalcularTrajetoria já tinham 4 parâmetros antes disso, e cada um dos itens abaixo se aplica
// igualmente aos dois métodos.
public record ProjecaoOpcoes(
    List<ContaBancaria> Contas,
    List<PagamentoFatura> PagamentosFatura,
    // "Provavel" (default) = médias históricas sem ajuste. "Conservador" = aplica margem de
    // segurança nos valores que são ESTIMATIVA (recorrências projetadas, despesa fixa não
    // cadastrada) — nunca nos valores já confirmados (provisionados, atrasados, fatura de
    // cartão), que são compromissos reais, não previsão.
    string Cenario = "Provavel",
    // Por padrão, contas Tipo == "Investimento" ficam de fora do saldo (mesmo critério já usado
    // em Dashboard/Indicadores — reserva não é dinheiro disponível pro dia a dia). true inclui a
    // reserva na projeção, pra quem quiser simular "e se eu usasse tudo".
    bool IncluirReserva = false);

public interface IProjecaoService
{
    ProjecaoDto Calcular(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        int dias,
        Guid? contaBancariaId,
        ProjecaoOpcoes opcoes);

    TrajetoriaDto CalcularTrajetoria(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        int mesesPassado,
        int mesesFuturo,
        Guid? contaBancariaId,
        ProjecaoOpcoes opcoes);

    // Item 3 da rodada "Visão de Futuro": mesma ideia de CalcularDespesaFixaNaoCadastrada, só que
    // quebrada por categoria (pra exibir na tela com o botão "Transformar em conta a pagar") em
    // vez de um único valor agregado usado internamente na simulação dia a dia.
    List<DespesaEstimadaCategoriaDto> CalcularDespesasEstimadasPorCategoria(
        List<RegistroDiario> registros, List<ContaRecorrente> recorrentes, DateOnly hoje);
}
