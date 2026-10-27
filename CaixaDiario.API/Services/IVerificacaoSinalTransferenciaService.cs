using CaixaDiario.API.DTOs.Transferencias;

namespace CaixaDiario.API.Services;

public interface IVerificacaoSinalTransferenciaService
{
    Task<List<TransferenciaSinalSuspeitoDto>> ListarSinaisSuspeitosAsync();
}
