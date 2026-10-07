using CaixaDiario.API.DTOs.Relatorios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Fase 1.5: relatório "Previsto × Realizado" — só títulos ligados a uma recorrência (RecorrenciaId
// != null) entram, porque só eles têm um "previsto" de verdade (o Valor materializado já é o valor
// do extrato recalculado — ver RecorrenciaService.CalcularValorPrevisto / Fase 1.6). Títulos
// avulsos (criados direto na tela, sem recorrência) não têm uma expectativa prévia pra comparar.
public class PrevistoRealizadoService : IPrevistoRealizadoService
{
    private const int MesesMinimo = 1;
    private const int MesesMaximo = 24;
    private const decimal LimiarVariacaoAlta = 0.10m;

    private readonly IRegistroRepository _registroRepo;
    private readonly IContaRecorrenteRepository _contaRecorrenteRepo;

    public PrevistoRealizadoService(IRegistroRepository registroRepo, IContaRecorrenteRepository contaRecorrenteRepo)
    {
        _registroRepo = registroRepo;
        _contaRecorrenteRepo = contaRecorrenteRepo;
    }

    public async Task<PrevistoRealizadoDto> ObterAsync(Guid clienteId, int meses, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        meses = Math.Clamp(meses, MesesMinimo, MesesMaximo);

        var hoje = DataLocalHelper.Hoje();
        var mesAtual = new DateOnly(hoje.Year, hoje.Month, 1);
        var mesesJanela = Enumerable.Range(0, meses)
            .Select(i => mesAtual.AddMonths(-(meses - 1) + i))
            .ToList();
        var primeiroMes = mesesJanela[0];
        var ultimoMesExclusivo = mesAtual.AddMonths(1);

        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        // Categoria do título (ContaProvisionada.Categoria) pode nunca ter sido preenchida — nesse
        // caso cai pra categoria cadastrada na própria ContaRecorrente, nunca direto pra "Sem
        // categoria" (ListarTodasAsync porque uma recorrência desativada ainda precisa aparecer
        // aqui pros meses em que já tinha títulos materializados).
        var categoriaPorRecorrencia = (await _contaRecorrenteRepo.ListarTodasAsync())
            .Where(r => r.ClienteId == clienteId)
            .ToDictionary(r => r.Id, r => r.Categoria);

        var itens = registros
            .SelectMany(r => r.ContasReceber.Select(c => (Item: c, Tipo: "Receber"))
                .Concat(r.ContasPagar.Select(c => (Item: c, Tipo: "Pagar"))))
            .Where(x => x.Item.RecorrenciaId.HasValue && x.Item.DataVencimento.HasValue
                && x.Item.DataVencimento.Value >= primeiroMes && x.Item.DataVencimento.Value < ultimoMesExclusivo)
            .ToList();

        string CategoriaDoItem(ContaProvisionada item) =>
            item.Categoria
            ?? (categoriaPorRecorrencia.TryGetValue(item.RecorrenciaId!.Value, out var catRecorrencia) ? catRecorrencia : null)
            ?? "Sem categoria";

        var grupos = itens.GroupBy(x => (x.Tipo, Categoria: CategoriaDoItem(x.Item)));

        var linhas = new List<LinhaPrevistoRealizadoDto>();
        foreach (var grupo in grupos)
        {
            var linha = new LinhaPrevistoRealizadoDto { Categoria = grupo.Key.Categoria, Tipo = grupo.Key.Tipo };
            var pontos = new List<PontoPrevistoRealizadoDto>();

            foreach (var mes in mesesJanela)
            {
                var doMes = grupo.Where(x => x.Item.DataVencimento!.Value.Year == mes.Year && x.Item.DataVencimento!.Value.Month == mes.Month).ToList();
                var previsto = doMes.Sum(x => x.Item.Valor);
                var realizado = doMes.Where(x => x.Item.Pago).Sum(x => x.Item.ValorRealizado ?? x.Item.Valor);

                decimal? variacao = previsto != 0 ? (realizado - previsto) / previsto : null;

                pontos.Add(new PontoPrevistoRealizadoDto
                {
                    Mes = $"{mes.Year:D4}-{mes.Month:D2}",
                    Previsto = previsto,
                    Realizado = realizado,
                    VariacaoPercentual = variacao,
                    VariacaoAlta = variacao.HasValue && Math.Abs(variacao.Value) > LimiarVariacaoAlta,
                });
            }

            // "3 meses seguidos" precisa da sequência COMPLETA (sem buracos) pra comparar meses de
            // verdade consecutivos — só depois disso os meses sem previsto nem realizado são ocultados.
            MarcarSubidasConsecutivas(pontos);
            linha.Meses = pontos.Where(p => p.Previsto != 0 || p.Realizado != 0).ToList();
            linhas.Add(linha);
        }

        return new PrevistoRealizadoDto { Linhas = linhas.OrderBy(l => l.Tipo).ThenBy(l => l.Categoria).ToList() };
    }

    // Marca o 3º mês de uma sequência de 3 altas consecutivas no Realizado (ex.: mar < abr < mai
    // => marca maio). Compara só Realizado — é o que de fato subiu, não o que era esperado.
    private static void MarcarSubidasConsecutivas(List<PontoPrevistoRealizadoDto> pontos)
    {
        for (var i = 2; i < pontos.Count; i++)
        {
            if (pontos[i - 2].Realizado < pontos[i - 1].Realizado && pontos[i - 1].Realizado < pontos[i].Realizado)
                pontos[i].SubiuTresMesesSeguidos = true;
        }
    }
}
