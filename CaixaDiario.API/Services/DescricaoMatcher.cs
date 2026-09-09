using System.Text.RegularExpressions;

namespace CaixaDiario.API.Services;

/// <summary>
/// Critério de casamento das regras de categorização automática. Porta fiel das duas partes
/// ESTÁVEIS de frontend/src/utils/descricaoSimilar.ts (extrairDocumento e a normalização
/// upper+trim) — a terceira parte de lá (removerPrefixoRecorrente) é relativa ao lote de uma
/// importação específica e não serve como critério persistente entre importações diferentes,
/// por isso não foi portada aqui.
/// </summary>
public static class DescricaoMatcher
{
    private static readonly Regex RegexCnpj = new(@"\d{2}\.?\d{3}\.?\d{3}\/?\d{4}-?\d{2}", RegexOptions.Compiled);
    private static readonly Regex RegexCpf = new(@"\d{3}\.?\d{3}\.?\d{3}-?\d{2}", RegexOptions.Compiled);

    /// <summary>CNPJ/CPF na descrição é o identificador mais confiável do favorecido, quando presente.</summary>
    public static string? ExtrairDocumento(string descricao)
    {
        var cnpj = RegexCnpj.Match(descricao);
        if (cnpj.Success) return "DOC:" + new string(cnpj.Value.Where(char.IsDigit).ToArray());
        var cpf = RegexCpf.Match(descricao);
        if (cpf.Success) return "DOC:" + new string(cpf.Value.Where(char.IsDigit).ToArray());
        return null;
    }

    public static string NormalizarExata(string descricao) => descricao.ToUpperInvariant().Trim();

    /// <summary>Determina o critério de uma nova regra a partir da descrição de referência escolhida pelo usuário.</summary>
    public static (string CriterioTipo, string CriterioValor) DeterminarCriterio(string descricao)
    {
        var doc = ExtrairDocumento(descricao);
        return doc != null ? ("Documento", doc) : ("DescricaoExata", NormalizarExata(descricao));
    }

    public static bool Casa(string criterioTipo, string criterioValor, string descricao) =>
        criterioTipo == "Documento"
            ? ExtrairDocumento(descricao) == criterioValor
            : NormalizarExata(descricao) == criterioValor;
}
