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

    // Bloco 3 da rodada "Visão de Futuro": preferências da tela "Meu Futuro" — nunca usadas em
    // nenhum cálculo financeiro em si, só pra customizar o alerta/linguagem daquela tela
    // (ver ProjecaoController/PreferenciasFuturoController). Null = ainda não configurado.
    public decimal? ReservaMinima { get; set; }
    // "PF" | "PJ" — null trata como "PJ" (linguagem padrão do app, já orientada a negócio).
    public string? TipoPessoa { get; set; }

    public ICollection<RegistroDiario> Registros { get; set; } = new List<RegistroDiario>();
    public List<MetaAnual> MetasAnuais { get; set; } = new();
    public ICollection<ContaRecorrente> ContasRecorrentes { get; set; } = new List<ContaRecorrente>();
    public ICollection<ContaBancaria> ContasBancarias { get; set; } = new List<ContaBancaria>();
}
