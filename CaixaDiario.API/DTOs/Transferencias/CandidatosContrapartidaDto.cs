namespace CaixaDiario.API.DTOs.Transferencias;

/// <summary>Lançamentos reais da conta contrapartida que podem ser a outra ponta da transferência.</summary>
public class CandidatosContrapartidaDto
{
    public string ContaContrapartidaNome { get; set; } = string.Empty;
    // Sem candidato e com extrato cobrindo a data: "o extrato do C6 não tem esse recebimento".
    public bool ExtratoCobreData { get; set; }
    public List<CandidatoContrapartidaDto> Candidatos { get; set; } = new();
}

public class CandidatoContrapartidaDto
{
    public Guid Id { get; set; }
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    // Com sinal: entrada positiva, saída negativa.
    public decimal Valor { get; set; }
}
