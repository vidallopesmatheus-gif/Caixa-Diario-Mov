using CaixaDiario.API.DTOs.Relatorios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Item 7 da rodada de ajustes Money Vision: "Previsto × Realizado" por ITEM (favorecido), não mais
// por categoria — por padrão só títulos ligados a uma recorrência (RecorrenciaId != null) entram,
// porque só eles têm um "previsto" de verdade (o Valor materializado já é o valor do extrato
// recalculado — ver RecorrenciaService.CalcularValorPrevisto). Títulos avulsos vinculados a um
// lançamento existente também entram quando incluirAvulsosVinculados=true.
public class PrevistoRealizadoService : IPrevistoRealizadoService
{
    private const int MesesMinimo = 1;
    private const int MesesMaximo = 24;
    // Acima disso, "Realizado" (bateu com o previsto) vira "Parcial" (pago, mas com diferença).
    private const decimal LimiarValorIgual = 0.02m;

    private readonly IRegistroRepository _registroRepo;
    private readonly IContaRecorrenteRepository _contaRecorrenteRepo;

    public PrevistoRealizadoService(IRegistroRepository registroRepo, IContaRecorrenteRepository contaRecorrenteRepo)
    {
        _registroRepo = registroRepo;
        _contaRecorrenteRepo = contaRecorrenteRepo;
    }

    public async Task<PrevistoRealizadoDto> ObterAsync(
        Guid clienteId, int meses, Guid usuarioLogadoId, string perfil, bool incluirAvulsosVinculados = false)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        meses = Math.Clamp(meses, MesesMinimo, MesesMaximo);

        var hoje = DataLocalHelper.Hoje();
        var mesAtual = new DateOnly(hoje.Year, hoje.Month, 1);
        var mesesJanela = Enumerable.Range(0, meses).Select(i => mesAtual.AddMonths(-(meses - 1) + i)).ToList();
        var primeiroMes = mesesJanela[0];
        var ultimoMesExclusivo = mesAtual.AddMonths(1);

        var itens = await ObterItensBaseAsync(clienteId, primeiroMes, ultimoMesExclusivo, incluirAvulsosVinculados);

        // Lista de itens (ponto 1): sempre do mês mais recente da janela — é o que responde "eu
        // previa receber 600 e recebi 500" pro período que o usuário está olhando agora. Quando a
        // janela é maior que 1 mês (filtro 3/6 meses), o item some os meses da janela inteira, pra
        // não esconder um furo que já passou do mês atual.
        var itensNaJanela = GroupByFavorecido(itens)
            .Select(g => MontarItem(g, hoje))
            .OrderByDescending(i => Math.Abs(i.Diferenca))
            .ToList();

        var acumuladoReceber = MontarAcumulado(itens.Where(x => x.Tipo == "Receber").ToList(), mesesJanela, meses == 1);
        var acumuladoPagar = MontarAcumulado(itens.Where(x => x.Tipo == "Pagar").ToList(), mesesJanela, meses == 1);

        return new PrevistoRealizadoDto { Itens = itensNaJanela, AcumuladoReceber = acumuladoReceber, AcumuladoPagar = acumuladoPagar };
    }

    public async Task<HistoricoItemPrevistoRealizadoDto> ObterHistoricoItemAsync(
        Guid clienteId, string chave, int meses, Guid usuarioLogadoId, string perfil, bool incluirAvulsosVinculados = false)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        if (string.IsNullOrWhiteSpace(chave))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Chave é obrigatória.", "chave");
        meses = Math.Clamp(meses, MesesMinimo, MesesMaximo);

        var hoje = DataLocalHelper.Hoje();
        var mesAtual = new DateOnly(hoje.Year, hoje.Month, 1);
        var mesesJanela = Enumerable.Range(0, meses).Select(i => mesAtual.AddMonths(-(meses - 1) + i)).ToList();
        var primeiroMes = mesesJanela[0];
        var ultimoMesExclusivo = mesAtual.AddMonths(1);

        var itens = await ObterItensBaseAsync(clienteId, primeiroMes, ultimoMesExclusivo, incluirAvulsosVinculados);
        var doFavorecido = itens.Where(x => x.Chave == chave).ToList();

        var pontos = mesesJanela.Select(mes =>
        {
            var doMes = doFavorecido.Where(x => x.Item.DataVencimento!.Value.Year == mes.Year && x.Item.DataVencimento!.Value.Month == mes.Month).ToList();
            var previsto = doMes.Sum(x => x.Item.Valor);
            var realizado = doMes.Where(x => x.Item.Pago).Sum(x => x.Item.ValorRealizado ?? x.Item.Valor);
            return new PontoPrevistoRealizadoDto
            {
                Mes = $"{mes.Year:D4}-{mes.Month:D2}",
                Previsto = previsto,
                Realizado = realizado,
                VariacaoPercentual = previsto != 0 ? (realizado - previsto) / previsto : null,
            };
        }).Where(p => p.Previsto != 0 || p.Realizado != 0).ToList();

        var nome = doFavorecido.OrderByDescending(x => x.Item.DataVencimento).FirstOrDefault()?.Item.Descricao ?? "";
        return new HistoricoItemPrevistoRealizadoDto { Nome = nome, Meses = pontos };
    }

    private async Task<List<ItemBase>> ObterItensBaseAsync(
        Guid clienteId, DateOnly primeiroMes, DateOnly ultimoMesExclusivo, bool incluirAvulsosVinculados)
    {
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        // Categoria do título (ContaProvisionada.Categoria) pode nunca ter sido preenchida — nesse
        // caso cai pra categoria cadastrada na própria ContaRecorrente, nunca direto pra "Sem
        // categoria" (ListarTodasAsync porque uma recorrência desativada ainda precisa aparecer
        // aqui pros meses em que já tinha títulos materializados).
        var categoriaPorRecorrencia = (await _contaRecorrenteRepo.ListarTodasAsync())
            .Where(r => r.ClienteId == clienteId)
            .ToDictionary(r => r.Id, r => r.Categoria);

        string CategoriaDoItem(ContaProvisionada item) =>
            item.Categoria
            ?? (item.RecorrenciaId.HasValue && categoriaPorRecorrencia.TryGetValue(item.RecorrenciaId.Value, out var catRecorrencia) ? catRecorrencia : null)
            ?? "Sem categoria";

        return registros
            .SelectMany(r => r.ContasReceber.Select(c => (Item: c, Tipo: "Receber"))
                .Concat(r.ContasPagar.Select(c => (Item: c, Tipo: "Pagar"))))
            .Where(x => x.Item.DataVencimento.HasValue
                && x.Item.DataVencimento.Value >= primeiroMes && x.Item.DataVencimento.Value < ultimoMesExclusivo)
            .Where(x => x.Item.RecorrenciaId.HasValue
                || (incluirAvulsosVinculados && x.Item.LancamentoVinculadoId.HasValue))
            .Select(x => new ItemBase(x.Item, x.Tipo, $"{x.Tipo}:{DescricaoMatcher.NormalizarExata(x.Item.Descricao)}", CategoriaDoItem(x.Item)))
            .ToList();
    }

    private record ItemBase(ContaProvisionada Item, string Tipo, string Chave, string Categoria);

    private static IEnumerable<IGrouping<string, ItemBase>> GroupByFavorecido(List<ItemBase> itens) =>
        itens.GroupBy(x => x.Chave);

    private static ItemPrevistoRealizadoDto MontarItem(IGrouping<string, ItemBase> grupo, DateOnly hoje)
    {
        var ultimo = grupo.OrderByDescending(x => x.Item.DataVencimento).First();
        var previsto = grupo.Sum(x => x.Item.Valor);
        var realizado = grupo.Where(x => x.Item.Pago).Sum(x => x.Item.ValorRealizado ?? x.Item.Valor);
        var diferenca = realizado - previsto;
        var pago = grupo.Any(x => x.Item.Pago);
        var vencimento = ultimo.Item.DataVencimento;

        string status;
        if (pago)
            status = previsto != 0 && Math.Abs(diferenca) / previsto <= LimiarValorIgual ? "Realizado" : "Parcial";
        else if (vencimento.HasValue && vencimento.Value < hoje)
            status = "Atrasado";
        else
            status = "NaoAconteceu";

        return new ItemPrevistoRealizadoDto
        {
            Chave = ultimo.Chave,
            Nome = ultimo.Item.Descricao,
            Tipo = ultimo.Tipo,
            Categoria = ultimo.Categoria,
            Previsto = previsto,
            Realizado = realizado,
            Diferenca = diferenca,
            DiferencaPercentual = previsto != 0 ? diferenca / previsto : null,
            Status = status,
            DataVencimento = vencimento,
        };
    }

    // Acumulado dia a dia (meses=1, "mês atual") ou mês a mês (3/6/12 meses) — é o que desenha as
    // duas linhas do gráfico (previsto tracejado × realizado sólido) e deixa o "furo" visível.
    private static List<PontoAcumuladoPrevistoRealizadoDto> MontarAcumulado(
        List<ItemBase> itensDoTipo, List<DateOnly> mesesJanela, bool porDia)
    {
        var pontos = new List<PontoAcumuladoPrevistoRealizadoDto>();
        decimal prevAcc = 0, realAcc = 0;

        if (porDia)
        {
            var mes = mesesJanela[0];
            var diasNoMes = DateTime.DaysInMonth(mes.Year, mes.Month);
            for (var d = 1; d <= diasNoMes; d++)
            {
                var data = new DateOnly(mes.Year, mes.Month, d);
                prevAcc += itensDoTipo.Where(x => x.Item.DataVencimento == data).Sum(x => x.Item.Valor);
                realAcc += itensDoTipo.Where(x => x.Item.Pago && x.Item.DataBaixa == data).Sum(x => x.Item.ValorRealizado ?? x.Item.Valor);
                pontos.Add(new PontoAcumuladoPrevistoRealizadoDto { Data = data.ToString("yyyy-MM-dd"), PrevistoAcumulado = prevAcc, RealizadoAcumulado = realAcc });
            }
            return pontos;
        }

        foreach (var mes in mesesJanela)
        {
            var doMes = itensDoTipo.Where(x => x.Item.DataVencimento!.Value.Year == mes.Year && x.Item.DataVencimento!.Value.Month == mes.Month).ToList();
            prevAcc += doMes.Sum(x => x.Item.Valor);
            realAcc += doMes.Where(x => x.Item.Pago).Sum(x => x.Item.ValorRealizado ?? x.Item.Valor);
            pontos.Add(new PontoAcumuladoPrevistoRealizadoDto { Data = $"{mes.Year:D4}-{mes.Month:D2}", PrevistoAcumulado = prevAcc, RealizadoAcumulado = realAcc });
        }
        return pontos;
    }

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }
}
