using CaixaDiario.API.DTOs.Registros;

namespace CaixaDiario.API.DTOs.ContasBancarias;

public class PendenciasContaDto
{
    public List<ContaProvisionadaDto> Recebiveis { get; set; } = new();
    public List<ContaProvisionadaDto> Pagamentos { get; set; } = new();
}
