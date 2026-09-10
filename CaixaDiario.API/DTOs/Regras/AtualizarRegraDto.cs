using System.ComponentModel.DataAnnotations;

namespace CaixaDiario.API.DTOs.Regras;

// ContaBancariaId e Tipo não são editáveis — mudar a conta/tipo de uma regra existente é raro o
// bastante pra valer mais excluir e criar de novo do que arriscar confundir o que ela já aplicou.
public class AtualizarRegraDto
{
    [Required] public string DescricaoReferencia { get; set; } = string.Empty;
    [Required] public string AcaoTipo { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public Guid? ContaContrapartidaId { get; set; }
    public bool Ativa { get; set; } = true;
}
