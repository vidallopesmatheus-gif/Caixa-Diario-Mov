namespace CaixaDiario.API.Models;

/// <summary>
/// Nível intermediário da hierarquia do DRE: Bloco → Grupo → Categoria. O Grupo é criado e
/// editado pelo usuário em Configurações → Plano de Contas; o Bloco é fixo na estrutura do
/// demonstrativo (ver Blocos.Validos) e decide onde o grupo aparece no DRE e qual Tipo as
/// categorias dele recebem (ver Blocos.TipoPadrao).
/// </summary>
public class Grupo
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Bloco { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
}
