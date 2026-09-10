namespace CaixaDiario.API.Models;

public class RegraCategorizacao
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    // Conta onde a regra se aplica na importação — a mesma de onde ela nasceu.
    public Guid ContaBancariaId { get; set; }
    public string Tipo { get; set; } = string.Empty; // "Entrada" | "Saida"

    // Critério de casamento — "Documento" (CNPJ/CPF extraído da descrição) ou "DescricaoExata"
    // (descrição normalizada igual). Ver DescricaoMatcher — mesma normalização usada no
    // agrupamento por similaridade da tela de categorização (frontend/src/utils/descricaoSimilar.ts).
    public string CriterioTipo { get; set; } = string.Empty;
    public string CriterioValor { get; set; } = string.Empty;
    // Descrição original do lançamento que originou a regra — só pra exibição/contexto.
    public string DescricaoReferencia { get; set; } = string.Empty;

    // Ação — "Categoria" ou "Transferencia"
    public string AcaoTipo { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    // Preenchido só quando AcaoTipo == "Transferencia".
    public Guid? ContaContrapartidaId { get; set; }

    public bool Ativa { get; set; } = true;
    // Precedência manual — menor valor vence quando mais de uma regra casa. Regra nova entra no fim.
    public int Ordem { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public Usuario Cliente { get; set; } = null!;
    public ContaBancaria ContaBancaria { get; set; } = null!;
    public ContaBancaria? ContaContrapartida { get; set; }
}
