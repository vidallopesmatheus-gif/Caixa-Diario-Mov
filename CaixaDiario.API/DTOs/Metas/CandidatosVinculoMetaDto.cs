namespace CaixaDiario.API.DTOs.Metas;

/// <summary>"Conquistei → Já lancei essa compra": o que pode ser vinculado à meta.</summary>
public class CandidatosVinculoMetaDto
{
    public List<ParcelamentoCandidatoDto> Parcelamentos { get; set; } = new();
    public List<LancamentoCandidatoDto> Lancamentos { get; set; } = new();
}

public class ParcelamentoCandidatoDto
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Banco { get; set; }
    public string ContaNome { get; set; } = string.Empty;
    public decimal ValorParcela { get; set; }
    public int TotalParcelas { get; set; }
    public int ParcelasPagas { get; set; }
    public decimal ValorTotal { get; set; }
    public Guid? MetaId { get; set; }
}

public class LancamentoCandidatoDto
{
    public Guid Id { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string ContaNome { get; set; } = string.Empty;
    public bool ContaEhCartao { get; set; }
    public string? Banco { get; set; }
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public Guid? ParcelamentoId { get; set; }
    public int? NumeroParcela { get; set; }
    public int? TotalParcelas { get; set; }
    public Guid? MetaVinculadaId { get; set; }
}
