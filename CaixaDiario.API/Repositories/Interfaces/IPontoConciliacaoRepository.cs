using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IPontoConciliacaoRepository
{
    Task<List<PontoConciliacao>> ListarPorContaAsync(Guid contaBancariaId);
    Task<List<PontoConciliacao>> ListarPorClienteAsync(Guid clienteId);
    // Um ponto por conta + data: grava por cima do que já existia naquele dia.
    Task<PontoConciliacao> SalvarAsync(PontoConciliacao ponto);
}
