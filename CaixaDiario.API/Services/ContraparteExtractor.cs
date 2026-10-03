using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CaixaDiario.API.Services;

public record ContraparteInfo(string Nome, string? DocumentoParcial, string Chave);

/// <summary>
/// Extrai o nome limpo do favorecido/pagador a partir da descrição bruta do extrato, removendo
/// prefixos de banco ("Pix enviado - ", "Compra no débito - "...), CPF/CNPJ (mascarado ou
/// completo), e ruído de banco/agência/conta. Usada na importação pra persistir Contraparte*
/// no lançamento — ver Models/ItemFinanceiro.cs.
///
/// Reaproveita DescricaoMatcher.ExtrairDocumento (mesma regex já usada pelas regras de
/// categorização) pro CPF/CNPJ completo; o resto (prefixos, nome, banco/agência/conta) é
/// capacidade nova — diferente de frontend/src/utils/descricaoSimilar.ts, que compara o prefixo
/// comum DENTRO DE UM LOTE de importação, aqui cada descrição é tratada isoladamente por
/// padrões de banco conhecidos, pra funcionar igual numa importação de 1 transação só.
/// </summary>
public static class ContraparteExtractor
{
    // Prefixos mais comuns observados em extratos de Nubank, C6, Bradesco e Itaú. Checados
    // ignorando maiúscula/minúscula, só no INÍCIO da descrição (um por vez — o primeiro que bater).
    private static readonly string[] PrefixosConhecidos =
    {
        "transferência recebida pelo pix - ",
        "transferência enviada pelo pix - ",
        "transferência recebida - ",
        "transferência enviada - ",
        "pix recebido - ",
        "pix enviado - ",
        "compra no débito - ",
        "compra no crédito - ",
        "débito de cartão - ",
        "pagamento efetuado - ",
        "pagamento recebido - ",
        "ted recebida - ",
        "ted enviada - ",
        "doc recebido - ",
        "doc enviado - ",
    };

    // CPF/CNPJ já mascarado pelo próprio banco (ex.: "•••.123.456-••") — contém dígitos E
    // marcadores de máscara misturados, por isso não casa com DescricaoMatcher.ExtrairDocumento
    // (que exige só dígitos). Só é removido do nome; nunca vira chave de agrupamento sozinho,
    // porque os dígitos escondidos são desconhecidos.
    private static readonly Regex RegexDocumentoMascarado = new(
        @"[•*xX\d]{2,3}\.[•*xX\d]{3}\.[•*xX\d]{3}(-[•*xX\d]{2}|/[•*xX\d]{4}-[•*xX\d]{2})?",
        RegexOptions.Compiled);

    // Código curto de cliente que alguns bancos antepõem/pospõem ao nome na mesma descrição
    // (ex.: "12.345.678 NOME DA PESSOA" ou "NOME DA PESSOA (12.345.678)") — não tem dígitos
    // suficientes pra ser CPF/CNPJ, então nunca vira documento; só limpa o nome, e as duas formas
    // acima convergem pro mesmo nome normalizado (chave por nome).
    private static readonly Regex RegexCodigoCurto = new(@"\(?\b\d{2}\.\d{3}\.\d{3}\b\)?", RegexOptions.Compiled);

    // "BCO C6 S.A. (0336) Agência: 1 Conta: 1234567-1" e variações — ruído de fim de descrição,
    // nunca faz parte do nome do favorecido.
    private static readonly Regex RegexBancoAgenciaConta = new(
        @"-?\s*(BCO|BANCO)\.?\s+[A-ZÀ-Ú0-9][A-ZÀ-Ú0-9 .]*?(S/?A\.?)?\s*(\(\d+\))?\s*((Ag[eê]ncia|Ag\.?)\s*:?\s*\d+)?\s*((Conta|CC)\.?\s*:?\s*[\d-]+)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ContraparteInfo Extrair(string descricaoOriginal)
    {
        var texto = (descricaoOriginal ?? string.Empty).Trim();

        foreach (var prefixo in PrefixosConhecidos)
        {
            if (texto.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            {
                texto = texto[prefixo.Length..].TrimStart();
                break;
            }
        }

        string? documentoCompleto = null;
        string? documentoParcial = null;

        var doc = DescricaoMatcher.ExtrairDocumento(texto);
        if (doc != null)
        {
            documentoCompleto = doc["DOC:".Length..];
            texto = RemoverDocumentoCompleto(texto, documentoCompleto);
            documentoParcial = Mascarar(documentoCompleto);
        }
        else
        {
            // Só conta como "documento mascarado pelo banco" se tiver de fato um caractere de
            // máscara — senão um código curto só-dígitos (ver RegexCodigoCurto) bateria aqui também,
            // sendo tratado como documento quando não é (8 dígitos não é CPF nem CNPJ).
            var mascaradoMatch = RegexDocumentoMascarado.Match(texto);
            if (mascaradoMatch.Success && mascaradoMatch.Value.Any(c => c is '•' or '*' or 'x' or 'X'))
            {
                documentoParcial = mascaradoMatch.Value;
                texto = texto.Remove(mascaradoMatch.Index, mascaradoMatch.Length);
            }
        }

        texto = RegexCodigoCurto.Replace(texto, " ");
        texto = RegexBancoAgenciaConta.Replace(texto, " ");
        var nome = LimparSeparadores(texto);
        if (nome.Length == 0) nome = LimparSeparadores(descricaoOriginal ?? string.Empty);

        var chave = documentoCompleto != null ? $"DOC:{documentoCompleto}" : $"NOME:{NormalizarNome(nome)}";

        return new ContraparteInfo(nome, documentoParcial, chave);
    }

    private static string RemoverDocumentoCompleto(string texto, string documentoDigits)
    {
        // CNPJ (14) formatado ou não; CPF (11) formatado ou não — a mesma ordem de prioridade de
        // DescricaoMatcher.ExtrairDocumento (CNPJ primeiro).
        var padroes = documentoDigits.Length == 14
            ? new[] { @"\d{2}\.?\d{3}\.?\d{3}\/?\d{4}-?\d{2}" }
            : new[] { @"\d{3}\.?\d{3}\.?\d{3}-?\d{2}" };

        foreach (var padrao in padroes)
        {
            var match = Regex.Match(texto, padrao);
            if (match.Success) return texto.Remove(match.Index, match.Length);
        }
        return texto;
    }

    /// <summary>Mantém só os dois blocos do meio visíveis — nunca o documento completo (ver Bloco 5B).</summary>
    public static string Mascarar(string documentoDigits)
    {
        if (documentoDigits.Length == 14)
            return $"••.{documentoDigits[2..5]}.{documentoDigits[5..8]}/{documentoDigits[8..12]}-••";
        if (documentoDigits.Length == 11)
            return $"•••.{documentoDigits[3..6]}.{documentoDigits[6..9]}-••";
        return documentoDigits;
    }

    private static string LimparSeparadores(string texto)
    {
        var limpo = texto.Trim().Trim('-', '–', ':', '/', '(', ')', ' ');
        limpo = Regex.Replace(limpo, @"\s{2,}", " ");
        // Sobrou separador solto no meio (ex.: "NOME  -  " virou "NOME -" depois do trim das pontas).
        limpo = Regex.Replace(limpo, @"\s*[-–]\s*$", "").Trim();
        return limpo;
    }

    /// <summary>Maiúsculas, sem acento, sem pontuação, espaços colapsados — chave de agrupamento por nome.</summary>
    private static string NormalizarNome(string nome)
    {
        var semAcento = RemoverAcentos(nome.ToUpperInvariant());
        var soLetrasEDigitos = Regex.Replace(semAcento, @"[^A-Z0-9 ]", " ");
        return Regex.Replace(soLetrasEDigitos, @"\s{2,}", " ").Trim();
    }

    private static string RemoverAcentos(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalizado)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
