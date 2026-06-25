namespace CaixaDiario.API.DTOs.Importacao;

public class ConfirmarTransacoesDto
{
    public List<ConfirmarTransacaoItem> Transacoes { get; set; } = new();
}

public class ConfirmarTransacaoItem
{
    public Guid Id { get; set; }
    public string? Categoria { get; set; }
    public bool Ignorar { get; set; } = false;
}
