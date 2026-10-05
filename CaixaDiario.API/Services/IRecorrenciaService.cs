namespace CaixaDiario.API.Services;

public interface IRecorrenciaService
{
    Task MaterializarMesAtualAsync(Guid clienteId);
    // Registra que essa ocorrência específica (não a recorrência inteira) foi excluída pelo
    // cliente — sem isso, a próxima materialização recria ela, porque só enxerga "não existe
    // nos registros atuais" e trata isso como "ainda não foi gerada".
    Task DispensarOcorrenciaAsync(Guid clienteId, Guid recorrenciaId, DateOnly dataVencimento);
}
