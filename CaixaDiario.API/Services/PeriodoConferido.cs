using System.Globalization;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

/// <summary>Corpo de "detalhes" do 409 PERIODO_CONFERIDO — a tela usa pra oferecer "Reabrir".</summary>
public record PeriodoConferidoErroDto(Guid ContaId, string Conta, DateOnly TravadoAte, bool PeloConsultor);

/// <summary>
/// Money Vision ("saldo que já batia voltou a não bater"): depois de um "Conferir saldo" que bateu,
/// nada que mexa no saldo de uma conta muda até a data conferida — só reabrindo. Categoria e
/// classificação continuam livres (o total do dia não muda).
/// </summary>
public static class PeriodoConferido
{
    public static bool Travado(ContaBancaria conta, DateOnly data) => conta.TravadoAte is { } ate && data <= ate;

    public static void Garantir(ContaBancaria conta, DateOnly data)
    {
        if (Travado(conta, data)) throw Erro(conta);
    }

    public static void Garantir(ContaBancaria conta, IEnumerable<DateOnly> datas)
    {
        if (datas.Any(d => Travado(conta, d))) throw Erro(conta);
    }

    public static ApiException Erro(ContaBancaria conta)
    {
        var ate = conta.TravadoAte!.Value;
        var mensagem = conta.TravadoPorConsultor
            ? $"Período da conta {conta.Nome} conferido pelo consultor até {ate:dd/MM/yyyy} — peça para reabrir."
            : $"Período da conta {conta.Nome} conferido até {ate:dd/MM/yyyy} — reabra o período para alterar.";
        return new ApiException(409, CodigoRetorno.PERIODO_CONFERIDO, mensagem,
            detalhes: new PeriodoConferidoErroDto(conta.Id, conta.Nome, ate, conta.TravadoPorConsultor));
    }

    /// <summary>O que mexe no saldo de um dia: lançamentos, valores, descrições e vínculos.</summary>
    public static List<string> Impressao(RegistroDiario? registro)
    {
        if (registro == null || registro.Excluido) return new();
        return registro.Entradas.Select(e => Linha("E", e.Id, e.Valor, e.Descricao, e.TransferenciaId, e.PagamentoFaturaId))
            .Concat(registro.Saidas.Select(s => Linha("S", s.Id, s.Valor, s.Descricao, s.TransferenciaId, s.PagamentoFaturaId)))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public static bool MudouOSaldo(RegistroDiario? antes, RegistroDiario? depois) =>
        !Impressao(antes).SequenceEqual(Impressao(depois));

    private static string Linha(string lado, Guid id, decimal valor, string descricao, Guid? transferenciaId, Guid? pagamentoFaturaId) =>
        string.Join('|', lado, id, valor.ToString("0.00", CultureInfo.InvariantCulture), descricao.Trim(), transferenciaId, pagamentoFaturaId);
}
