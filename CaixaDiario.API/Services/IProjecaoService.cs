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
    bool IncluirReserva = false,
    // Cartão (Bloco 4, Money Vision): faturas reais (Models/Fatura.cs) do cliente — mesmo
    // agrupamento usado em FaturaCartaoService.CalcularFaturas, pra nunca duplicar a lógica de
    // "qual compra pertence a qual fatura" entre Faturas e Projeção.
    List<Fatura>? FaturasReais = null);

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

    // Bloco 3 da rodada "Visão de Futuro": tela "Meu Futuro" — agrega a mesma simulação de
    // Calcular em 12 meses (Saldo inicial/A receber/A pagar/Saldo final/Precisa receber/Status) +
    // conselho em regras. ajusteReceitaPercent/atrasoRecebimentosDias são o simulador "E se...?".
    MeuFuturoDto CalcularMeuFuturo(
        List<RegistroDiario> registros, List<ContaRecorrente> recorrentes, ProjecaoOpcoes opcoes,
        decimal reservaMinima, decimal ajusteReceitaPercent = 0m, int atrasoRecebimentosDias = 0, int meses = 12);
}
