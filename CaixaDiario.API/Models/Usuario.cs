namespace CaixaDiario.API.Models;

public class Usuario
{
    public Guid Id { get; set; }
    public string NomeUsuario { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Loja { get; set; }
    public string Perfil { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public string? UsuarioAtualizacao { get; set; }

    // ── Indicador FIRE (Bloco 7B) ──────────────────────────────────────────
    // Custo de vida mensal informado manualmente pelo cliente/consultor — fonte de verdade quando
    // presente. Nulo = modo automático (média das categorias EhPessoal nos últimos 12 meses
    // fechados) ou, sem dados suficientes, estado vazio pedindo o valor — nunca a despesa total
    // do negócio. Ver ConfiguracaoFinanceiraService e frontend/src/utils/fire.ts.
    public decimal? CustoVidaMensalManual { get; set; }
    // % ao ano usado pra calcular o patrimônio-alvo (ValorAlvo = CustoVida × 12 ÷ Taxa). Padrão de
    // mercado é 4% (regra dos 4%); editável porque é só uma referência, não uma verdade absoluta.
    public decimal TaxaRetiradaFire { get; set; } = 4m;

    public ICollection<RegistroDiario> Registros { get; set; } = new List<RegistroDiario>();
    public List<MetaAnual> MetasAnuais { get; set; } = new();
    public ICollection<ContaRecorrente> ContasRecorrentes { get; set; } = new List<ContaRecorrente>();
    public ICollection<ContaBancaria> ContasBancarias { get; set; } = new List<ContaBancaria>();
}
