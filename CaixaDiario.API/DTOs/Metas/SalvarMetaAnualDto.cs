using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Metas;

public class SalvarMetaAnualDto
{
    // Identifica qual objetivo (modo "metodo") atualizar — como agora um cliente pode ter vários
    // simultâneos, (ClienteId, Ano) deixou de bastar pra achar "a" meta a atualizar nesse modo.
    // Sem Id, sempre cria um objetivo novo. Ignorado no modo "simples" (identidade continua Ano).
    public Guid? Id { get; set; }
    [Required] public Guid ClienteId { get; set; }
    [Required, Range(2000, 2100)] public int Ano { get; set; }
    [Range(0, double.MaxValue)] public decimal MetaReceita { get; set; }
    [Range(0, double.MaxValue)] public decimal MetaLucro { get; set; }
    [Range(1, 12)] public int MesInicio { get; set; } = 1;
    [Range(1, 12)] public int PeriodoMeses { get; set; } = 12;
    public string? Sonho { get; set; }
    public string ModoMeta { get; set; } = "simples";
    [Range(0, double.MaxValue)] public decimal ValorSonho { get; set; }
    [Range(0, 100)] public int PrazoAnos { get; set; }
    [Range(0, 100)] public decimal TaxaRetorno { get; set; }
    [Range(0, double.MaxValue)] public decimal TotalInvestido { get; set; }
    [Range(0, 100)] public decimal? MargemPJ { get; set; }
    public string? IconeSonho { get; set; }
    // Data real escolhida pelo usuário pra atingir o objetivo — obrigatória no modo "metodo".
    public DateOnly? DataAlvo { get; set; }
}
