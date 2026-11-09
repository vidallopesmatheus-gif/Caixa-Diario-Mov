using CaixaDiario.API.DTOs.Admin;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Money Vision: "saldo das contas não fecha com o extrato" — migração de reparo pra produção.
// Reencadeia TODAS as contas pela função única (SaldoEncadeamento): Inicio = saldo final do dia
// anterior, SaldoFinal = Inicio + efeito real do dia. Mesmo padrão dry-run-por-padrão do
// MigracaoRecorrenciasService/LimpezaTransferenciasService: sem confirmar=true, só relatório.
public class RecalculoSaldoService : IRecalculoSaldoService
{
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;

    public RecalculoSaldoService(IContaBancariaRepository contaRepo, IRegistroRepository registroRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
    }

    public async Task<RecalculoSaldoResultDto> RecalcularTodasAsContasAsync(bool confirmar)
    {
        var resultado = new RecalculoSaldoResultDto { Confirmado = confirmar };
        var contas = await _contaRepo.ListarTodasAsync();

        foreach (var conta in contas)
        {
            var registros = (await _registroRepo.ListarPorContaAsync(conta.Id))
                .Where(r => !r.Excluido)
                .OrderBy(r => r.Data)
                .ToList();
            if (registros.Count == 0) continue;

            var saldoAtualAntes = registros[^1].SaldoFinal;
            var calculados = SaldoEncadeamento.Calcular(conta.SaldoInicial, registros);
            var primeiraDivergente = registros
                .Select((r, i) => (r, c: calculados[i]))
                .FirstOrDefault(x => x.r.Inicio != x.c.Inicio || x.r.SaldoFinal != x.c.Final);
            if (primeiraDivergente.r == null) continue;

            if (confirmar)
                foreach (var registro in SaldoEncadeamento.Encadear(conta.SaldoInicial, registros))
                    await _registroRepo.AtualizarAsync(registro);

            resultado.Itens.Add(new DivergenciaSaldoContaDto
            {
                ClienteId = conta.ClienteId,
                ContaBancariaId = conta.Id,
                ContaBancariaNome = conta.Nome,
                PrimeiraDataDivergente = primeiraDivergente.r.Data,
                SaldoAtualAntes = saldoAtualAntes,
                SaldoAtualDepois = calculados[^1].Final,
            });
        }

        resultado.ContasComDivergencia = resultado.Itens.Count;
        return resultado;
    }

    // Money Vision ("ajuste mexeu no DRE"): todo lançamento que é ajuste de saldo (categoria ou
    // descrição "Ajuste de saldo", ou o tipo legado "AjusteSaldo") passa pro tipo de sistema
    // AjusteConciliacao — fica fora do DRE/indicadores e para de ser reclassificável. O saldo não
    // muda (é o mesmo lançamento, só a classificação).
    public async Task<MigracaoAjustesConciliacaoResultDto> MigrarAjustesConciliacaoAsync(bool confirmar)
    {
        var resultado = new MigracaoAjustesConciliacaoResultDto { Confirmado = confirmar };

        foreach (var conta in await _contaRepo.ListarTodasAsync())
        {
            foreach (var registro in (await _registroRepo.ListarPorContaAsync(conta.Id)).Where(r => !r.Excluido))
            {
                var mudou = false;
                foreach (var e in registro.Entradas.Where(e => PrecisaConverter(e.TipoCusto, e.Categoria, e.Descricao)).ToList())
                {
                    resultado.Itens.Add(Item(conta, registro, e.Descricao, e.Valor, e.TipoCusto, e.Categoria));
                    mudou = true;
                    if (!confirmar) continue;
                    e.TipoCusto = LancamentoFiltro.TipoAjusteConciliacao;
                    e.Categoria = LancamentoFiltro.CategoriaAjusteSaldo;
                    e.PendenteCategorizacao = false;
                    e.CategoriaSugerida = false;
                }
                foreach (var s in registro.Saidas.Where(s => PrecisaConverter(s.TipoCusto, s.Categoria, s.Descricao)).ToList())
                {
                    resultado.Itens.Add(Item(conta, registro, s.Descricao, -s.Valor, s.TipoCusto, s.Categoria));
                    mudou = true;
                    if (!confirmar) continue;
                    s.TipoCusto = LancamentoFiltro.TipoAjusteConciliacao;
                    s.Categoria = LancamentoFiltro.CategoriaAjusteSaldo;
                    s.PendenteCategorizacao = false;
                    s.CategoriaSugerida = false;
                }

                if (!mudou || !confirmar) continue;
                registro.Entradas = new List<Models.ItemFinanceiro>(registro.Entradas); // força o EF a detectar a mudança (jsonb)
                registro.Saidas = new List<Models.ItemFinanceiroSaida>(registro.Saidas);
                registro.SalvoEm = DateTime.UtcNow;
                await _registroRepo.AtualizarAsync(registro);
            }
        }

        resultado.LancamentosConvertidos = resultado.Itens.Count;
        return resultado;
    }

    private static bool PrecisaConverter(string? tipoCusto, string? categoria, string? descricao) =>
        tipoCusto != LancamentoFiltro.TipoAjusteConciliacao && LancamentoFiltro.PareceAjusteConciliacao(tipoCusto, categoria, descricao);

    private static AjusteConvertidoDto Item(
        Models.ContaBancaria conta, Models.RegistroDiario r, string descricao, decimal valor, string? tipoCusto, string? categoria) => new()
    {
        ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Data = r.Data, Descricao = descricao, Valor = valor,
        TipoCustoAnterior = tipoCusto, CategoriaAnterior = categoria,
    };
}
