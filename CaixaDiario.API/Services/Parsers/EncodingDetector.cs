using System.Text;
using System.Text.RegularExpressions;

namespace CaixaDiario.API.Services.Parsers;

/// <summary>
/// Extratos bancários brasileiros não têm um encoding único: alguns declaram CHARSET/ENCODING
/// no cabeçalho OFX, outros vêm em UTF-8 puro sem BOM, outros ainda em Latin-1/Windows-1252
/// legado. Forçar um encoding fixo faz "débito" virar "dÃ©bito" (bytes UTF-8 lidos como
/// Latin-1) sempre que o arquivo real não bater com a suposição.
/// </summary>
public static class EncodingDetector
{
    /// Usa o CHARSET/ENCODING declarado no cabeçalho (se houver); na ausência de declaração,
    /// decide pelo conteúdo. O cabeçalho OFX 1.x (linhas "CHAVE:VALOR" antes de "&lt;OFX&gt;") e a
    /// declaração XML do OFX 2.x são sempre ASCII puro, então é seguro ler essa prévia como ASCII
    /// só para localizar as tags, antes de decodificar o arquivo inteiro com o encoding certo.
    public static Encoding DetectarOfx(byte[] bytes)
    {
        var tamanhoPrevia = Math.Min(bytes.Length, 512);
        var previa = Encoding.ASCII.GetString(bytes, 0, tamanhoPrevia);

        var mCharset = Regex.Match(previa, @"CHARSET:\s*([\w-]+)", RegexOptions.IgnoreCase);
        if (mCharset.Success)
        {
            var resolvido = ResolverPorNome(mCharset.Groups[1].Value);
            if (resolvido != null) return resolvido;
        }

        var mXmlEncoding = Regex.Match(previa, @"encoding\s*=\s*[""']([\w-]+)[""']", RegexOptions.IgnoreCase);
        if (mXmlEncoding.Success)
        {
            var resolvido = ResolverPorNome(mXmlEncoding.Groups[1].Value);
            if (resolvido != null) return resolvido;
        }

        return DetectarPorConteudo(bytes);
    }

    /// Sem cabeçalho declarando o encoding (caso do CSV): tenta decodificar como UTF-8 estrito;
    /// se os bytes não formarem UTF-8 válido, é porque o arquivo é Latin-1/Windows-1252 (padrão
    /// legado). BOM UTF-8, se presente, tem prioridade.
    public static Encoding DetectarPorConteudo(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false);

        try
        {
            new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            // Latin-1 (ISO-8859-1) é um superset de byte-a-byte suficiente para os caracteres
            // acentuados usados em português — evita depender do pacote System.Text.Encoding.CodePages
            // (não referenciado no projeto) só para diferenciar de Windows-1252.
            return Encoding.Latin1;
        }
    }

    private static Encoding? ResolverPorNome(string nome)
    {
        var upper = nome.ToUpperInvariant();
        if (upper.Contains("1252") || upper.Contains("8859")) return Encoding.Latin1;
        if (upper.Contains("UTF-8") || upper.Contains("UTF8")) return new UTF8Encoding(false);
        if (upper is "NONE" or "USASCII") return null; // não é uma declaração de charset real
        try { return Encoding.GetEncoding(nome); }
        catch { return null; }
    }
}
