namespace CaixaDiario.API.Services;

public interface IRecorrenciaService
{
    Task MaterializarMesAtualAsync(Guid clienteId);
}
