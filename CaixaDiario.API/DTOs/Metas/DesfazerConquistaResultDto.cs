namespace CaixaDiario.API.DTOs.Metas;

/// <summary>Fase 5.5: resultado de "Desfazer conquista" — o que foi removido de verdade e o que
/// ficou (porque já foi pago/vinculado ao extrato e remover destruiria dinheiro real).</summary>
public class DesfazerConquistaResultDto
{
    public List<string> Removidos { get; set; } = new();
    public List<string> Mantidos { get; set; } = new();
}
