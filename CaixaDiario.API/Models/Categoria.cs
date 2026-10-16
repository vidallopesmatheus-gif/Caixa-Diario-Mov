namespace CaixaDiario.API.Models;

public class Categoria
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    // "Receita" | "CustoVariavel" | "CustoFixo" | "DespesaNaoOperacional" | "Investimento" | "Financiamento" | "RetiradaSocios"
    // Derivado automaticamente do Bloco do Grupo escolhido (ver Blocos.TipoPadrao) — não é mais
    // escolhido diretamente na criação da categoria.
    public string Tipo { get; set; } = string.Empty;
    // "Entrada" | "Saida" | "Ambos" — rodada de ajustes Money Vision (item 8): em que lado do
    // combobox de lançamento a categoria aparece. Tem um padrão derivado do Bloco do Grupo (ver
    // CategoriaService.TipoMovimentoPadrao) aplicado na criação, mas o usuário pode sobrescrever —
    // diferente de Tipo acima, que é sempre automático.
    public string TipoMovimento { get; set; } = string.Empty;
    public Guid GrupoId { get; set; }
    public int Ordem { get; set; }
    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; }

    // Correção de integridade (Parte 1): categoria passa a ser por cliente — null + Origem="Padrao"
    // é o "molde" copiado pra cada cliente NOVO na criação (ver ClienteService/UsuarioService);
    // clientes já existentes recebem a cópia via migração de dados, uma vez só. Dali em diante a
    // lista de categorias de um cliente é só dele — editar/desativar nunca afeta outro cliente
    // nem o próprio molde padrão. Único (ClienteId, Nome) — nunca (Nome) isolado.
    public Guid? ClienteId { get; set; }
    // "Padrao" (ClienteId == null, molde) | "Cliente" (pertence a um cliente específico).
    public string Origem { get; set; } = "Padrao";
    public Guid? CriadoPor { get; set; }

    public Grupo Grupo { get; set; } = null!;
}
