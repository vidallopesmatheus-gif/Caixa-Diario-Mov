using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface IParcelamentoDeteccaoService
{
    /// <summary>
    /// Resolve (ou cria) o Parcelamento correspondente a uma parcela real detectada na importação
    /// de uma fatura, remove a prevista que essa parcela real substitui (se houver) e gera as
    /// previstas que ainda faltam em Contas a Pagar. Marca o Parcelamento como "Quitado" quando a
    /// parcela recém-chegada é a última (numero == total).
    /// </summary>
    Task<Parcelamento> ProcessarParcelaDetectadaAsync(
        ContaBancaria contaCartao, DateOnly dataVencimentoFatura,
        string comerciante, string? categoria, int numero, int total, decimal valorParcela);
}
